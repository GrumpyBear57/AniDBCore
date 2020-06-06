using System.Threading.Tasks;

namespace AniDBCore.Commands.Auth {
    public class AuthCommand : Command {
        public AuthCommand(string username, string password) : base(typeof(AuthResult)) {
            Parameters.Add("username", username);
            Parameters.Add("password", password);
        }

        public override async Task<ICommandResult> Send() {
            return await Client.QueueCommand(this);
        }
    }
}