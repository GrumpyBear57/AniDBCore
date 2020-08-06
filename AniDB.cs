using System;
using System.Threading.Tasks;
using AniDBCore.Commands;
using AniDBCore.Commands.Auth;
using AniDBCore.Commands.Misc;

namespace AniDBCore {
    public static class AniDB {
        public const string ClientName = "AniDBCore";
        public const string ClientVersion = "1";

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
                Client.SetApiKey(apiKey);
                ICommandResult result = await SendCommand(new EncryptCommand(username));
                if (result.ReturnCode != ReturnCode.EncryptionEnabled)
                    return result;
            }

            return await SendCommand(new AuthCommand(username, password));
        }

        public static async Task<ICommandResult> Logout() {
            return await SendCommand(new LogoutCommand());
        }
    }
}