using System.Threading.Tasks;

namespace AniDBCore.Commands.Auth {
    public class LogoutCommand : Command {
        public LogoutCommand() : base("LOGOUT", false, typeof(LogoutResult), null) {
        }

        public override async Task<ICommandResult> Send() {
            return await Client.QueueCommand(this);
        }
    }
}