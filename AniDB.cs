using System.Threading.Tasks;
using AniDBCore.Commands;
using AniDBCore.Commands.Auth;

namespace AniDBCore {
    public class AniDB {
        public AniDB(string host, int port) {
            Client.Connect(host, port);
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