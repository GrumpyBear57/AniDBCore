namespace AniDBCore.Commands.Misc {
    public class PingResult : CommandResult {
        public PingResult(ReturnCode returnCode, string data) : base(returnCode) {
        }
    }
}