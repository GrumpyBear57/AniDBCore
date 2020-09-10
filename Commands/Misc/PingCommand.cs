using System.Collections.Generic;
using AniDBCore.Utils;

namespace AniDBCore.Commands.Misc {
    public class PingCommand : Command {
        private static readonly IReadOnlyDictionary<string, DataType> OptionalParams =
            new Dictionary<string, DataType> {
                {
                    "nat", DataType.Boolean
                }
            };

        public PingCommand() : base("PING", false, typeof(PingResult), OptionalParams) {
        }
    }
}