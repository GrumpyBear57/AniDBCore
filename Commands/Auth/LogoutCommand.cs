namespace AniDBCore.Commands.Auth {
    public class LogoutCommand : Command {
        public LogoutCommand() : base("LOGOUT", false, typeof(LogoutResult), null) {
        }
    }
}