using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AniDBCore.Commands {
    public class CommandTag {
        private static readonly ConcurrentDictionary<string, CommandTag> TagsInUse =
            new ConcurrentDictionary<string, CommandTag>();

        private const string ValidTagChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private static readonly Random Random = new Random();

        private readonly string _tag;

        public CommandTag() {
            string tag;
            bool done;
            do {
                tag = GenerateTag(); // Generate
                done = TagsInUse.TryAdd(tag, this); // Reserve tag
            } while (done == false);

            _tag = tag;
        }

        private static string GenerateTag() {
            string tag = "_";
            for (int i = 0; i < 4; i++)
                tag += ValidTagChars[Random.Next(ValidTagChars.Length)];

            // Ensure tag is valid
            if (ValidTag(tag) == false)
                throw new Exception("Generated invalid tag?");

            return tag;
        }

        public static bool ValidTag(string tag) {
            return Regex.IsMatch(tag, "^_[0-9a-zA-Z]{4}$");
        }

        public static bool TryGetTag(string tagStr, out CommandTag tag) {
            if (TagsInUse.ContainsKey(tagStr)) {
                tag = TagsInUse[tagStr];
                return true;
            }

            tag = null;
            return false;
        }

        public void Release() {
            if (TagsInUse.TryRemove(_tag, out CommandTag _) == false)
                // Maybe I can loop this until it succeeds instead of bailing. Meh
                throw new Exception("Failed to release Tag!");
        }

        public override string ToString() {
            return _tag;
        }
    }
}