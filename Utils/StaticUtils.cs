using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace AniDBCore.Utils {
    public static class StaticUtils {
        // Dictionary instead of HashSet because there is no concurrent HashSet implementation provided.
        private static readonly ConcurrentDictionary<string, bool> TagsInUse = new ConcurrentDictionary<string, bool>();
        private const string ValidTagChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private static readonly Random Random = new Random();

        public static string EncodeContent(this string content) {
            content = content.Replace("&", "&amp;");
            content = content.Replace("\r\n", "<br />");
            content = content.Replace("\r", "<br />"); // No idea if carriage return is ever on it's own, shrug
            content = content.Replace("\n", "<br />");

            return content;
        }

        public static string DecodeBytesToContent(this byte[] data) {
            string content = Encoding.ASCII.GetString(data);

            content = content.Replace("<br />", Environment.NewLine);
            content = content.Replace('`', '\'');

            return content;
        }

        public static string GenerateTag() {
            string tag;
            bool done;

            do {
                tag = "_";
                for (int i = 0; i < 4; i++) {
                    tag += ValidTagChars[Random.Next(ValidTagChars.Length)];
                }

                // Ensure tag is valid
                if (ValidTag(tag) == false)
                    throw new Exception("Generated invalid tag?");

                // Reserve tag
                done = TagsInUse.TryAdd(tag, false);
            } while (done == false);

            return tag;
        }

        public static bool ValidTag(string tag) {
            return Regex.IsMatch(tag, "^_[0-9a-zA-Z]{4}$");
        }

        public static void ReleaseTag(string tag) {
            if (TagsInUse.TryRemove(tag, out bool _) == false)
                // Maybe I can loop this until it succeeds instead of bailing. Meh
                throw new Exception("Failed to release Tag!");
        }
    }
}