using System.Threading.Tasks;

namespace AniDBCore.Commands.Auth {
    public class AuthCommand : Command {
        public AuthCommand(string username, string password) : base("AUTH", typeof(AuthResult)) {
            Parameters.Add("user", username);
            Parameters.Add("pass", password);
            Parameters.Add("protover", "3");
            Parameters.Add("client", AniDB.ClientName);
            Parameters.Add("clientver", AniDB.ClientVersion);
        }

        public override async Task<ICommandResult> Send() {
            return await Client.QueueCommand(this);
        }
    }
}