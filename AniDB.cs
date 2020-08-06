using System;
using System.Threading.Tasks;
using AniDBCore.Commands;
using AniDBCore.Commands.Auth;
using AniDBCore.Commands.Misc;

namespace AniDBCore {
    public class AniDB {
        public const string ClientName = "AniDBCore";
        public const string ClientVersion = "1";

        public AniDB(string host, int port, bool cache = true) {
            Client.Connect(host, port);
            Client.Cache = cache;

            PingCommand command = new PingCommand();
            bool setParameter = command.SetOptionalParameter("nat", "1", out string error);
            if (setParameter == false)
                throw new Exception($"Failed to set parameter ({error})");
            Task<ICommandResult> result = command.Send();
            result.Wait(2500);
            if (result.Result.ReturnCode == ReturnCode.RequestTimedOut)
                throw new Exception("Failed to connect");
        }

        public void Disconnect() {
            Client.Disconnect();
        }

        public async Task<ICommandResult> SendCommand(ICommand command) {
            return await command.Send();
        }

        public async Task<ICommandResult> Auth(string username, string password) {
            return await SendCommand(new AuthCommand(username, password));
        }
    }
}