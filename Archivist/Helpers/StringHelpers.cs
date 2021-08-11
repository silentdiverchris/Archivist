using System.Collections.Generic;

namespace Archivist.Helpers
{
    internal static class StringHelpers
    {
        internal static string ConcatenateToDelimitedList(this IEnumerable<string> strings, string delimiter = ",", string quote = "'")
        {
            return quote + string.Join(delimiter, strings) + quote;
        }

        internal static bool IsDigits(this string text)
        {
            foreach (var c in text)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A decidedly primitive implementation, not intended to work for everything, to be
        /// fixed each time a new case it doesn't handle comes up
        /// </summary>
        /// <param name="str"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        internal static string Pluralise(this string str, int number, string addSuffixIfNotEmpty = null)
        {
            if (string.IsNullOrEmpty(str))
            {
                return "";
            }
            else
            {
                if (number == 1)
                {
                    return str + addSuffixIfNotEmpty;
                }
                else
                {
                    if (str.EndsWith("ry"))
                    {
                        return str[0..^1] + "ies" + addSuffixIfNotEmpty;
                    }
                    else
                    {
                        return str + "s" + addSuffixIfNotEmpty;
                    }
                }
            }
        }

        internal static string TruncateWithEllipsis(this string text, int length, bool strictLength = false)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.Trim();

            if (string.IsNullOrEmpty(text) || text.Length < length)
            {
                return text;
            }

            if (strictLength)
            {
                return text.Substring(0, length - 3).TrimEnd() + "...";
            }
            else
            {
                return text.Substring(0, length).TrimEnd() + "...";
            }
        }
    }
}
