using System.Collections.Generic;
using AniDBCore.Utils;

namespace AniDBCore.Commands.Auth {
    public class AuthCommand : Command {
        private static readonly IReadOnlyDictionary<string, DataType> OptionalParams =
            new Dictionary<string, DataType> {
                {
                    "nat", DataType.Boolean
                }, {
                    "comp", DataType.Boolean
                }, {
                    "enc", DataType.String
                }, {
                    "mtu", DataType.Int4
                }, {
                    "imgserver", DataType.Boolean
                }
            };

        public AuthCommand(string username, string password) : base("AUTH", false, typeof(AuthResult),
                                                                    OptionalParams) {
            Parameters.Add("user", username);
            Parameters.Add("pass", password);
            Parameters.Add("protover", "3");
            Parameters.Add("client", AniDB.ClientName);
            Parameters.Add("clientver", AniDB.ClientVersion);
        }
    }
}