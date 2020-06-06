namespace AniDBCore.Commands {
    public class CommandResult : ICommandResult {
        internal CommandResult(ReturnCode returnCode) {
            ReturnCode = returnCode;
        }

        public ReturnCode ReturnCode { get; protected set; }
    }
}