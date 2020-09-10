using System;
using System.Threading.Tasks;
using AniDBCore.Commands;
using AniDBCore.Commands.Auth;
using AniDBCore.Commands.Misc;
using AniDBCore.Events;
using AniDBCore.Utils;

namespace AniDBCore {
    public static class AniDB {
        public const string ClientName = "AniDBCore";
        public const string ClientVersion = "1";

        public static event EventHandler<ClientErrorArgs> ClientError;
        public static event EventHandler<ServerErrorArgs> ServerError;
        public static event EventHandler<CommandSentArgs> CommandSent;
        public static event EventHandler<CommandResultReceivedArgs> CommandResultReceived;
        public static event EventHandler KeepAlivePing;
        public static event EventHandler KeepAlivePong;

        public static bool Connect(string host, int port, bool cache = true) {
            bool clientConnected = Client.Connect(host, port);
            if (clientConnected == false)
                return false;

            Client.Cache = cache;

            PingCommand command = new PingCommand();
            bool setParameter = command.SetOptionalParameter("nat", "1", out string error);
            if (setParameter == false)
                throw new Exception($"Failed to set parameter ({error})");
            Task<ICommandResult> result = command.Send();
            result.Wait(2500);
            return result.Result.ReturnCode == ReturnCode.Pong;
        }

        public static void Disconnect() {
            Client.Disconnect();
        }

        public static async Task<ICommandResult> SendCommand(ICommand command) {
            return await command.Send();
        }

        public static async Task<ICommandResult> Auth(string username, string password, bool encryption = false,
                                                      string apiKey = "") {
            if (encryption) {
                Session.SetApiKey(apiKey);
                ICommandResult result = await SendCommand(new EncryptCommand(username));
                if (result.ReturnCode != ReturnCode.EncryptionEnabled)
                    return result;
            }

            return await SendCommand(new AuthCommand(username, password));
        }

        public static async Task<ICommandResult> Logout() {
            return await SendCommand(new LogoutCommand());
        }

        // Dunno if sender should really be null for the event invocations, but I can't use this 'cause static class, so....

        internal static void InvokeClientError(ClientErrorArgs args) {
            ClientError?.Invoke(null, args);
        }

        internal static void InvokeServerError(ServerErrorArgs args) {
            ServerError?.Invoke(null, args);
        }

        internal static void InvokeCommandSent(CommandSentArgs args) {
            CommandSent?.Invoke(null, args);
        }

        internal static void InvokeCommandResultReceived(CommandResultReceivedArgs args) {
            CommandResultReceived?.Invoke(null, args);
        }

        internal static void InvokeKeepAlivePing() {
            KeepAlivePing?.Invoke(null, EventArgs.Empty);
        }

        internal static void InvokeKeepAlivePong() {
            KeepAlivePong?.Invoke(null, EventArgs.Empty);
        }
    }
}