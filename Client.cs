using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AniDBCore.Commands;
using AniDBCore.Commands.Auth;
using AniDBCore.Commands.Misc;
using AniDBCore.Utils;

namespace AniDBCore {
    internal static class Client {
        public static bool Cache;
        public static bool Connected { get; private set; }
        public static bool Encryption { get; private set; }

        private static readonly Dictionary<string, ICommandResult> CommandsWithResponse =
            new Dictionary<string, ICommandResult>();

        private static readonly Dictionary<string, Command> CommandsWaitingForResponse =
            new Dictionary<string, Command>();

        private static readonly Queue<Command> CommandQueue = new Queue<Command>();
        private static int _timeOutsSinceLastResponse;
        private static UdpClient _connection;
        private const int LocalPort = 4257;
        private static string _encryptionKey;
        private static string _sessionKey;
        private static bool _rateLimited;
        private static string _apiKey;

        private static object rlLock = new object();

        private static void ReceiveData() {
            IPEndPoint any = new IPEndPoint(IPAddress.Any, LocalPort);
            // Always receive data
            while (Connected) {
                byte[] rawData = _connection.Receive(ref any);
                string dataString = rawData.DecodeBytesToContent();
                _rateLimited = false;

                Console.WriteLine($"{DateTime.Now:hh:mm:ss} - Got data: {dataString}");

                List<string> data = dataString.Split(' ').ToList();
                Command command = null;

                // Check for tag. If there is no tag present, that means the packet is either a server error, or a notification
                if (StaticUtils.ValidTag(data[0])) {
                    // Find command using tag
                    command = CommandsWaitingForResponse[data[0]];
                    if (command.Tag != data[0])
                        throw new Exception(); // something is really fucky. We should never get here. I hope.

                    // Remove tag from the result so that we parse the return code next
                    data.RemoveAt(0);
                }

                // Check for server error (handles any error in the 6xx range, just in case
                if (int.TryParse(data[0], out int returnCodeInt) && returnCodeInt >= 600 && returnCodeInt <= 699 &&
                    returnCodeInt != 601 && returnCodeInt != 602) {
                    // Wiki says to report to Ommina, but he says this probably won't ever happen, so ¯\_(ツ)_/¯

                    if (command != null)
                        // From the sounds of it, it's not likely that we have a tag for a 6xx error, but if we do, send it back to the caller
                        CommandsWithResponse.Add(command.Tag, new CommandResult(ReturnCode.InternalServerError));

                    continue;
                }

                if (command == null) {
                    // Something is wrong, we should have a command since we always set a tag.
                    // The only time this won't be the case is when we receive a notification, but that isn't implemented yet (TODO)
                    throw new Exception(
                        $"Command is null; tag is probably not set on reply\nData: {dataString}"); // TODO handle this better
                }

                // Get the return code
                if (Enum.TryParse(data[0], out ReturnCode returnCode)) {
                    // Remove the return code since we've already parsed it into our enum
                    data.RemoveAt(0);

                    // Handle all cases where we need to handle something in this class, otherwise just return a response to the queue
                    switch (returnCode) {
                        case ReturnCode.LoginAccepted:
                        case ReturnCode.LoginAcceptedNewVersion: {
                            _sessionKey = data[0];

                            CommandsWithResponse.Add(command.Tag, new AuthResult(returnCode, data[0]));
                            break;
                        }
                        case ReturnCode.EncryptionEnabled: {
                            Encryption = true;
                            string salt = data[0];
                            _encryptionKey = StaticUtils.MD5Hash(_apiKey + salt);
                            break;
                        }
                        case ReturnCode.ServerBusy:
                        case ReturnCode.OutOfService: {
                            // TODO make SendData() wait until API is back (should we delay different amounts of time for Busy vs Out of Service?)
                            break;
                        }
                        case ReturnCode.Timeout: {
                            // TODO add the command back to the queue to be sent again
                            break;
                        }
                        default:
                            ICommandResult result =
                                Activator.CreateInstance(command.ResultType, returnCode, data[0]) as ICommandResult;
                            CommandsWithResponse.Add(command.Tag, result);
                            break;
                    }
                } else {
                    // Couldn't get the return code? TODO handle
                    Console.WriteLine("no return code?");
                }
            }

            // Clean up any loose commands that never got a reply
            foreach (KeyValuePair<string, Command> kvp in CommandsWaitingForResponse) {
                ICommandResult result =
                    Activator.CreateInstance(kvp.Value.ResultType, ReturnCode.ConnectionClosed) as ICommandResult;
                CommandsWithResponse.Add(kvp.Key, result);
            }
        }

        private static void SendData() {
            // local bool so we don't lock for 2 seconds. If the value changes between the time we lock, and the time we start sleeping, oh well, it won't break anything, just delay it.
            bool wait = false;
            DateTime lastCommandSentTime = DateTime.MinValue;
            while (Connected) {
                lock (rlLock)
                    if (_rateLimited)
                        wait = true;

                if (wait) {
                    // TODO exponential backoff
                    Thread.Sleep(2000); // sleep and hope we aren't rate limited after we're done
                    continue;
                }

                // Check if queue is empty and continue if it is 
                if (CommandQueue.Count == 0) {
                    // Check if last command we sent was more than 5 minutes ago so we can keep connection alive (for NAT)
                    if (lastCommandSentTime < DateTime.Now.AddMinutes(-5) && lastCommandSentTime != DateTime.MinValue) {
                        // blindly send a ping to keep connection alive. Is this the best? Maybe not, but should work.
                        PingCommand pingCommand = new PingCommand();
                        bool parameterSet = pingCommand.SetOptionalParameter("nat", "1", out string error);
                        if (parameterSet == false)
                            throw new Exception($"setting parameter failed ({error})");
                        Task<ICommandResult> _ = pingCommand.Send();
                        //TODO NAT stuff
                    }

                    Thread.Sleep(150); // small timeout
                    continue;
                }

                Command command = CommandQueue.Dequeue();
                lastCommandSentTime = DateTime.Now;
                string commandString = $"{command.CommandBase} {command.GetParameters()}";
                if (command.RequiresSession)
                    commandString += $"&{_sessionKey}";

                Console.WriteLine("Sending data: " + commandString);

                byte[] bytes = Encoding.ASCII.GetBytes(commandString.Trim());
                _connection.Send(bytes, bytes.Length);
                CommandsWaitingForResponse.Add(command.Tag, command);

                // Sleep for 2.1 seconds just to make sure API don't get mad (limit is 1 per 2 seconds)
                Thread.Sleep(2100); // TODO this needs to wait longer when we receive a response code that tells us to
            }
        }

        private static ICommandResult WaitForResponse(string tag) {
            int waitTime = 0;
            while (CommandsWithResponse.ContainsKey(tag) == false) {
                if (waitTime > 20_000) {
                    // Note: I *think* that this should fix itself it it encounters a race condition.
                    lock (rlLock) {
                        _timeOutsSinceLastResponse++;
                        if (_timeOutsSinceLastResponse > 5)
                            // we're probably rate limited
                            _rateLimited = true;
                    }

                    return new CommandResult(ReturnCode.RequestTimedOut);
                }

                Thread.Sleep(50);
                waitTime += 50;
            }

            lock (rlLock) {
                _timeOutsSinceLastResponse = 0;
            }

            return CommandsWithResponse[tag];
        }

        public static bool Connect(string host, int port) {
            if (Connected)
                return false;

            _connection = new UdpClient(new IPEndPoint(IPAddress.Any, LocalPort));
            _connection.Connect(host, port);
            Connected = true;
            Task.Run(ReceiveData);
            Task.Run(SendData);

            return true;
        }

        public static void Disconnect() {
            if (Connected == false)
                return;

            _connection.Close();
            Connected = false;
            // Tasks should clean themselves up since we're only doing while Connected
        }

        public static Task<ICommandResult> QueueCommand(Command command) {
            if (Connected == false)
                throw new Exception("Connection must be initialized before queueing commands!");

            CommandQueue.Enqueue(command);
            return Task.Run(() => {
                StaticUtils.ReleaseTag(command.Tag);
                return WaitForResponse(command.Tag);
            });
        }

        public static void SetApiKey(string apiKey) {
            // TODO make sure this is valid API key format
            
            if (string.IsNullOrEmpty(_apiKey) == false)
                throw new Exception("API Key cannot be set more than once");

            _apiKey = apiKey;
        }
    }
}