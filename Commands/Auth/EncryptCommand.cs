using System.Threading.Tasks;

namespace AniDBCore.Commands.Auth {
    public class EncryptCommand : Command {
        public EncryptCommand(string username) : base("ENCRYPT", false, typeof(EncryptResult), null) {
            Parameters.Add("user", username);
            Parameters.Add("type", "1"); // Type 1 = AES (the only implemented one ATM)
        }

        public override async Task<ICommandResult> Send() {
            return await Client.QueueCommand(this);
        }
    }
}