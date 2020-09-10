using System;
using System.Collections.Generic;
using System.Linq;
using AniDBCore.Events;

namespace AniDBCore.Commands {
    internal readonly struct APIResponse {
        public readonly CommandTag Tag;
        public readonly ReturnCode ReturnCode;
        public readonly IReadOnlyList<string> Data;

        public readonly string Raw;

        public APIResponse(string response) {
            Raw = response;
            List<string> data = response.Split(' ').ToList();

            // Get the tag (if present)
            if (CommandTag.TryGetTag(data[0], out Tag)) {
                // If we had a tag, then remove the tag element
                // don't want to remove it if there's not a tag since it would be removing the return code then
                data.RemoveAt(0);
            }

            // parse the return code into an int
            if (int.TryParse(data[0], out int returnCodeInt) == false) {
                // This should really be a int TODO handle better
                throw new Exception($"Couldn't parse {data[0]} into an integer (should be return code)");
            }

            // Wiki says possible server error codes are 600-699
            if (returnCodeInt >= 600 && returnCodeInt <= 699) {
                if (returnCodeInt != 601 && returnCodeInt != 602) {
                    // We would report this to Ommina, but he said these would almost never happen, so ¯\_(ツ)_/¯
                    // (wiki says report anything other than 601 and 602 to him)
                }

                // Not entirely sure what I should do here, from the sounds of it, it's unlikely I'll get a tag on a 6xx
                // error response, so it's not like I could match the response to a command that was sent, meaning that 
                // whatever command caused the 6xx is going to show as timed out instead of server error. 
                //   
                // For now this is hte best I can think of to ensure that there's *some* way to report we got a 6xx
                AniDB.InvokeServerError(new ServerErrorArgs(response));
            }

            // Get the return code
            if (!Enum.TryParse(data[0], out ReturnCode)) {
                // Couldn't get the return code? TODO handle
                Console.WriteLine($"no return code? raw: {data[0]}");
            }

            data.RemoveAt(0);

            Data = data;
        }

        public override string ToString() {
            return Raw;
        }
    }
}