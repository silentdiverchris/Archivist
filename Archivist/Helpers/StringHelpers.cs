﻿using System.Collections.Generic;
using System.IO;

namespace Archivist.Helpers
{
    internal static class StringHelpers
    {
        internal static bool IsExistingFileName(this string? value)
        {
            return !string.IsNullOrEmpty(value) && File.Exists(value);
        }

        internal static bool NoSuchFileName(this string? value)
        {
            return string.IsNullOrEmpty(value) || !File.Exists(value);
        }

        /// <summary>
        /// Return the value, or the alternativeValue if it's empty, or the ifBothEmpty if both are empty
        /// </summary>
        /// <param name="value"></param>
        /// <param name="alternativeValue"></param>
        /// <param name="ifBothEmpty"></param>
        /// <returns></returns>
        internal static string IfEmpty(this string? value, string? alternativeValue, string ifBothEmpty = "")
        {
            return value.NotEmpty()
                ? value!
                : alternativeValue.NotEmpty()
                    ? alternativeValue!
                    : ifBothEmpty;
        }

        /// <summary>
        /// Doesn't add much but a lot more readable in use
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static bool NotEmpty(this string? value)
        {
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Doesn't add much but a lot more readable in use
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static bool IsEmpty(this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Add the specified prefix if the string is not empty
        /// </summary>
        /// <param name="text"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        internal static string PrefixIfNotEmpty(this string? text, string prefix = " ")
        {
            return $"{(string.IsNullOrEmpty(text) ? null : prefix)}{text}";
        }

        /// <summary>
        /// Add the specified suffix if the string is not empty
        /// </summary>
        /// <param name="text"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        internal static string SuffixIfNotEmpty(this string? text, string suffix = " ")
        {
            return $"{text}{(string.IsNullOrEmpty(text) ? null : suffix)}";
        }

        /// <summary>
        /// Make a presentable list from an enumerable of strings 
        /// </summary>
        /// <param name="strings"></param>
        /// <param name="delimiter"></param>
        /// <param name="quote"></param>
        /// <returns></returns>
        internal static string ConcatenateToDelimitedList(this IEnumerable<string> strings, string delimiter = ";", string quote = "'")
        {
            return quote + string.Join(delimiter, strings) + quote;
        }

        /// <summary>
        /// Is the string comprised solely of digits
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
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
        /// A primitive implementation, not intended to work for everything, to be
        /// fixed each time a new case it doesn't handle comes up.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="number"></param>
        /// <param name="addSuffixIfNotEmpty"></param>
        /// <returns></returns>
        internal static string Pluralise(this string str, int number, string? addSuffixIfNotEmpty = null)
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

        /// <summary>
        /// Cut off the string at a certain length, adding the ellipsis parameter if something is removed.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="length">Cut off at this length</param>
        /// <param name="strictLength">Don't allow the ellipsis to make the string longer than the max length</param>
        /// <param name="ellipsis">Add this to the resulting string</param>
        /// <returns></returns>
        internal static string TruncateWithEllipsis(this string text, int length, bool strictLength = false, string ellipsis = "...")
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
                return text.Substring(0, length - 3).TrimEnd() + ellipsis;
            }
            else
            {
                return text.Substring(0, length).TrimEnd() + ellipsis;
            }
        }
    }
}
