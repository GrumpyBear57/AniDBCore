using System.Threading.Tasks;

namespace AniDBCore.Commands {
    public interface ICommand {
        Task<ICommandResult> Send();
    }
}