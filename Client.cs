using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AniDBCore.Commands;
using AniDBCore.Commands.Auth;
using AniDBCore.Commands.Misc;
using AniDBCore.Events;
using AniDBCore.Utils;

namespace AniDBCore {
    internal static class Client {
        private const int LocalPort = 4257;

        public static bool Cache;
        public static bool Connected { get; private set; }

        private static readonly Dictionary<CommandTag, ICommandResult> CommandsWithResponse =
            new Dictionary<CommandTag, ICommandResult>();

        private static readonly Dictionary<CommandTag, Command> CommandsWaitingForResponse =
            new Dictionary<CommandTag, Command>();

        private static readonly Queue<Command> CommandQueue = new Queue<Command>();
        private static readonly Session Session;

        private static int _timeOutsSinceLastResponse;
        private static CommandTag _keepAliveTag;
        private static UdpClient _connection;
        private static bool _rateLimited;

        private static object rlLock = new object();

        private static void ReceiverLoop() {
            IPEndPoint any = new IPEndPoint(IPAddress.Any, LocalPort);
            // Always receive data
            while (Connected) {
                // TODO we'll have to change this when we start handling things like notifications.
                // Current thoughts is to have the `WaitForResponse` method determine if it's a response or a notification
                // then handle it appropriately

                APIResponse response = WaitForResponse(ref any);
                Command command = ParseResponse(response);
                if (command == null)
                    continue;
                ICommandResult result = PreReturnResult(response, command);

                CommandsWithResponse.Add(command.Tag, result);
            }

            // Clean up any loose commands that never got a reply
            foreach (KeyValuePair<CommandTag, Command> kvp in CommandsWaitingForResponse) {
                ICommandResult result =
                    Activator.CreateInstance(kvp.Value.ResultType, ReturnCode.ConnectionClosed) as ICommandResult;
                CommandsWithResponse.Add(kvp.Key, result);
            }
        }

        private static APIResponse WaitForResponse(ref IPEndPoint any) {
            // this should probably get renamed since a notification isn't a response to a request, but we don't handle that yet
            byte[] rawData = _connection.Receive(ref any);
            string dataString = rawData.DecodeBytesToContent();
            _rateLimited = false;

            //TEMP
            Console.WriteLine($"{DateTime.Now:hh:mm:ss} - Got data: {dataString}");

            return new APIResponse(dataString);
        }

        private static Command ParseResponse(APIResponse response) {
            // Make sure there was a tag on the response
            if (response.Tag == null) {
                Console.WriteLine($"No tag ({response})");
                return null;
            }

            // Find command using tag
            Command command = CommandsWaitingForResponse[response.Tag];

            // Don't need to proceed if this is the response to our KeepAlive ping
            if (response.Tag == _keepAliveTag) {
                // TODO do NAT stuff here instead of where we sent this ping, since we have the port the API sees
                AniDB.InvokeKeepAlivePong();
                return null;
            }

            if (command != null)
                return command;

            // We should always have a command here, since we set a tag on every command we send
            // Raise an error saying we're missing a command
            AniDB.InvokeClientError(
                new ClientErrorArgs($"Command is null; tag is probably not set on reply\nData: {response}")
            );

            return null;
        }

        private static ICommandResult PreReturnResult(APIResponse response, Command command) {
            // Handle all cases where we need to handle something in this class, otherwise just return a response to the queue
            switch (response.ReturnCode) {
                case ReturnCode.LoginAccepted:
                case ReturnCode.LoginAcceptedNewVersion: {
                    Session.StartSession(response.Data[0]);

                    return new AuthResult(response.ReturnCode);
                }
                case ReturnCode.EncryptionEnabled: {
                    string salt = response.Data[0];
                    Session.EnableEncryption(StaticUtils.MD5Hash(Session.ApiKey + salt));
                    
                    return new EncryptResult(response.ReturnCode);
                }
                case ReturnCode.ServerBusy:
                case ReturnCode.OutOfService: {
                    // TODO make WriterLoop() wait until API is back (should we delay different amounts of time for Busy vs Out of Service?)
                    return new CommandResult(response.ReturnCode);
                }
                case ReturnCode.Timeout: {
                    // TODO add the command back to the queue to be sent again
                    return new CommandResult(response.ReturnCode);
                }
                default:
                    return Activator.CreateInstance(command.ResultType, response.ReturnCode, response.Data[0]) as
                        ICommandResult;
            }
        }

        private static void WriterLoop() {
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

                // Send any queued commands, otherwise keep connection alive (ping if last command was sent more than 5m ago)
                if (CommandQueue.Count != 0) {
                    SendCommand(CommandQueue.Dequeue());
                    lastCommandSentTime = DateTime.Now;
                } else
                    KeepConnectionAlive(lastCommandSentTime);

                // Sleep for 2.1 seconds just to make sure API don't get mad (limit is 1 per 2 seconds)
                Thread.Sleep(2100); // TODO this needs to wait longer when we receive a response code that tells us to
            }
        }

        private static ICommandResult WaitForResponse(CommandTag tag) {
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

            AniDB.InvokeCommandResultReceived(new CommandResultReceivedArgs());
            tag.Release();
            return CommandsWithResponse[tag];
        }

        private static void KeepConnectionAlive(DateTime lastCommandSentTime) {
            // Check if last command we sent was more than 5 minutes ago so we can keep connection alive (for NAT)
            if (lastCommandSentTime < DateTime.Now.AddMinutes(-5) && lastCommandSentTime != DateTime.MinValue) {
                PingCommand pingCommand = new PingCommand();
                bool parameterSet = pingCommand.SetOptionalParameter("nat", "1", out string error);
                if (parameterSet == false) {
                    AniDB.InvokeClientError(new ClientErrorArgs($"Setting parameter failed ({error})"));
                    return;
                }

                _keepAliveTag = pingCommand.Tag;

                Task<ICommandResult> _ = pingCommand.Send();
                AniDB.InvokeKeepAlivePing();
            }

            Thread.Sleep(150); // small timeout
        }

        private static void SendCommand(Command command) {
            if (ShouldSendCommand(command) == false)
                return;

            string commandString = $"{command.CommandBase} {command.GetParameters()}".Trim();
            if (command.RequiresSession)
                commandString += $"&{Session.SessionKey}";

            // TEMP
            Console.WriteLine("Sending data: " + commandString);

            AniDB.InvokeCommandSent(new CommandSentArgs(command));

            byte[] bytes = Encoding.ASCII.GetBytes(commandString);
            _connection.Send(bytes, bytes.Length);
            CommandsWaitingForResponse.Add(command.Tag, command);
        }

        private static bool ShouldSendCommand(Command command) {
            // Don't login again
            if (Session.LoggedIn && command.CommandBase == "AUTH")
                return false;

            // Don't encrypt again
            if (Session.EncryptionEnabled && command.CommandBase == "ENCRYPT")
                return false;

            // Don't set connection to be encrypted when api key is not set
            if (string.IsNullOrEmpty(Session.ApiKey) && command.CommandBase == "ENCRYPT")
                return false;

            // Don't encrypt if a session is already in progress (maybe close the current session and start a new one?)
            if (Session.LoggedIn && command.CommandBase == "ENCRYPT")
                return false;

            // Don't logout if not logged in
            if (Session.LoggedIn == false && command.CommandBase == "LOGOUT")
                return false;

            return true;
        }

        public static bool Connect(string host, int port) {
            if (Connected)
                return false;

            _connection = new UdpClient(new IPEndPoint(IPAddress.Any, LocalPort));
            _connection.Connect(host, port);
            Connected = true;
            Task.Run(ReceiverLoop);
            Task.Run(WriterLoop);

            return true;
        }

        public static void Disconnect() {
            if (Connected == false)
                return;

            // Logout first
            SendCommand(new LogoutCommand());
            Session.EndSession();

            _connection.Close();
            Connected = false;
            // Tasks should clean themselves up since we're only doing while Connected
        }

        public static Task<ICommandResult> QueueCommand(Command command) {
            if (Connected == false)
                AniDB.InvokeClientError(
                    new ClientErrorArgs(
                        $"Connection must be initialized before queueing commands! {command.CommandBase}"));

            CommandQueue.Enqueue(command);
            return Task.Run(() => WaitForResponse(command.Tag));
        }
    }
}