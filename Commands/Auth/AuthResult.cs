namespace AniDBCore.Commands.Auth {
    public class AuthResult : CommandResult {
        public AuthResult(ReturnCode returnCode, string data) : base(returnCode) {
        }
    }
}