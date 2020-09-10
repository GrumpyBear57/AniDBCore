using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AniDBCore.Utils {
    public static class StaticUtils {
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

        public static bool IsParameterValid(string name, string value,
                                            IReadOnlyDictionary<string, DataType> parameterDefinitions,
                                            ref string error) {
            if (parameterDefinitions.ContainsKey(name) == false) {
                error = $"Invalid parameter name: {name}";
                return false;
            }

            switch (parameterDefinitions[name]) {
                case DataType.Boolean:
                    if (value != "0" && value != "1") {
                        error = $"Parameter '{name}' expects a boolean (0 or 1) value, got '{value}'";
                        return false;
                    }

                    break;
                case DataType.String:
                    // TODO Limit to 1400 bytes? Might depend on MTU? 
                    break;
                case DataType.Int2:
                    if (short.TryParse(value, out short _) == false) {
                        error = $"Parameter '{name}' expects a 2-byte Integer, got '{value}'";
                        return false;
                    }

                    break;
                case DataType.Int4:
                    if (int.TryParse(value, out int _) == false) {
                        error = $"Parameter '{name}' expects a 4-byte Integer, got '{value}'";
                        return false;
                    }

                    break;
                case DataType.HexString:
                    // TODO (from wiki) -- A hex representation of a decimal value, two characters per byte.
                    //                     If multiple bytes are represented, byte one is the first two characters
                    //                     of the string, byte two the next two, and so on. 
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return true;
        }

        public static string MD5Hash(string value) {
            using MD5 md5 = MD5.Create();

            byte[] bytes = Encoding.ASCII.GetBytes(value);
            byte[] hash = md5.ComputeHash(bytes);

            // TODO Find out if this is supposed to be converted straight back to ASCII, or if it's hex
            // For now, hex
            StringBuilder output = new StringBuilder(hash.Length * 2);
            foreach (byte b in bytes) {
                output.Append(b.ToString("X2"));
            }

            return output.ToString();
        }
    }
}