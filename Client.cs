using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AniDBCore.Commands;
using AniDBCore.Commands.Auth;

namespace AniDBCore {
    internal static class Client {
        public static bool Connected { get; private set; }

        private static readonly Dictionary<string, ICommandResult> CommandsWithResponse =
            new Dictionary<string, ICommandResult>();

        private static readonly Dictionary<string, Command> CommandsWaitingForResponse =
            new Dictionary<string, Command>();

        private static readonly Queue<Command> CommandQueue = new Queue<Command>();
        private static int _timeOutsSinceLastResponse;
        private static UdpClient _connection;
        private static string _sessionKey;
        private static bool _rateLimited;

        private static object rlLock = new object();

        private static void ReceiveData() {
            IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
            // Always receive data
            while (Connected) {
                byte[] rawData = _connection.Receive(ref any);
                string dataString = Encoding.ASCII.GetString(rawData);
                _rateLimited = false;

                List<string> data = dataString.Split(' ').ToList();
                Command command = null;

                // Check for tag. If there is no tag present, that means the packet is either a server error, or a notification
                if (Regex.IsMatch(data[0], "/^_[0-9a-zA-Z]{4}$/i")) {
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
                    // TODO Report to Ommina

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
                if (Enum.TryParse(data[0], out ReturnCode returnCode) == false) {
                    // Handle all cases where we need to handle something in this class, otherwise just return a response to the queue
                    switch (returnCode) {
                        case ReturnCode.LoginAccepted:
                        case ReturnCode.LoginAcceptedNewVersion: {
                            _sessionKey = data[1];

                            CommandsWithResponse.Add(command.Tag, new AuthResult(returnCode));
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
                                Activator.CreateInstance(command.ResultType, returnCode) as ICommandResult;
                            CommandsWithResponse.Add(command.Tag, result);
                            break;
                    }
                } else {
                    // Couldn't get the return code? TODO handle
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
            while (Connected) {
                lock (rlLock)
                    if (_rateLimited)
                        wait = true;

                if (wait) {
                    Thread.Sleep(2000); // sleep and hope we aren't rate limited after we're done
                    continue;
                }

                Command command = CommandQueue.Dequeue();
                // TODO Send command
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

        public static void Connect(string host, int port) {
            if (Connected)
                return;

            _connection = new UdpClient(host, port);
            Connected = true;
            Task.Run(ReceiveData);
            Task.Run(SendData);
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
            return Task.Run(() => WaitForResponse(command.Tag));
        }
    }
}