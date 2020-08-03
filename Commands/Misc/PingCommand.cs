using System.Threading.Tasks;

namespace AniDBCore.Commands.Misc {
    public class PingCommand : Command {
        public PingCommand() : base("PING", typeof(PingResult)) {
            Parameters.Add("nat", "1");
        }

        public override async Task<ICommandResult> Send() {
            return await Client.QueueCommand(this);
        }
    }
}