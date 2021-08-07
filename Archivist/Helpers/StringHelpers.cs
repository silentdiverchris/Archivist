using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Archivist.Helpers
{
    internal static class StringHelpers
    {
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

        internal static string FormatJsonAsHtml(this string json)
        {
            dynamic parsedJson = JsonConvert.DeserializeObject(json);

            string str = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);

            return $"<pre><code>{str}</pre/></code>";
        }

        //internal static string FormatJson2(this string json, string indent = "  ")
        //{
        //    var indentation = 0;
        //    var quoteCount = 0;
        //    var escapeCount = 0;

        //    var result =
        //        from ch in json ?? string.Empty
        //        let escaped = (ch == '\\' ? escapeCount++ : escapeCount > 0 ? escapeCount-- : escapeCount) > 0
        //        let quotes = ch == '"' && !escaped ? quoteCount++ : quoteCount
        //        let unquoted = quotes % 2 == 0
        //        let colon = ch == ':' && unquoted ? ": " : null
        //        let nospace = char.IsWhiteSpace(ch) && unquoted ? string.Empty : null
        //        let lineBreak = ch == ',' && unquoted ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(indent, indentation)) : null
        //        let openChar = (ch == '{' || ch == '[') && unquoted ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(indent, ++indentation)) : ch.ToString()
        //        let closeChar = (ch == '}' || ch == ']') && unquoted ? Environment.NewLine + string.Concat(Enumerable.Repeat(indent, --indentation)) + ch : ch.ToString()
        //        select colon ?? nospace ?? lineBreak ?? (
        //            openChar.Length > 1 ? openChar : closeChar
        //        );

        //    return string.Concat(result);
        //}

        //internal static string FormatJson(this string json)
        //{
        //    string INDENT_STRING = "    ";
        //    int indentation = 0;
        //    int quoteCount = 0;
        //    IEnumerable<string> result =
        //        from ch in json
        //        let quotes = ch == '"' ? quoteCount++ : quoteCount
        //        let lineBreak = ch == ',' && quotes % 2 == 0 ? ch + Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, indentation)) : null
        //        let openChar = ch == '{' || ch == '[' ? ch + Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, ++indentation)) : ch.ToString()
        //        let closeChar = ch == '}' || ch == ']' ? Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, --indentation)) + ch : ch.ToString()
        //        select lineBreak == null
        //                    ? openChar.Length > 1
        //                        ? openChar
        //                        : closeChar
        //                    : lineBreak;

        //    return string.Concat(result);
        //}

        //internal static string PrettyJson(this string json)
        //{
        //    dynamic parsedJson = JsonConvert.DeserializeObject(json);
        //    string str = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
        //    return str;
        //    //return JsonConvert.SerializeObject(json, Formatting.Indented);

        //    //var options = new JsonSerializerOptions()
        //    //{
        //    //    WriteIndented = true
        //    //};

        //    //var jsonElement = JsonSerializer.Deserialize<JsonElement>(unPrettyJson);

        //    //return JsonSerializer.Serialize(jsonElement, options);
        //}

        internal static string FromTo(object from, object to)
        {
            if (from is null && to is null)
            {
                return "";
            }
            else if (from is null && to is not null)
            {
                return $"to {to}";
            }
            else if (from is not null && to is null)
            {
                return $"From {from}";
            }
            else
            {
                return $"{from} to {to}";
            }
        }

        internal static bool ContainsScripting(this string str, string fieldName, out string errorMessage)
        {
            errorMessage = null;

            if (str.Contains("<") || str.Contains(">") || str.Contains("/") || str.Contains("[") || str.Contains("]"))
            {
                errorMessage = $"{fieldName} cannot contain characters like <>/[].";
                return true;
            }

            if (str.ToUpper().Contains("HTTP"))
            {
                errorMessage = $"{fieldName} cannot contain strings like HTTP.";
                return true;
            }

            return false;
        }

        /// <summary>
        /// The theory is the make safe strings used in calls to Html.Raw(), we can't remove < and > etc as we do
        /// want html formatting in there so we will just remove script declarations, for now anyway.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        internal static string RemoveScriptingFromHtml(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;


            List<string> disallowedStrings = new() { "SCRIPT ", "script " };

            foreach (string s in disallowedStrings)
            {
                int idx = str.IndexOf(s);

                while (idx >= 0)
                {
                    // Could use a stringbuilder for faster string slicing but clean
                    // text won't need any slicing so why bother for the tiny number of
                    // texts that need furtling.

                    str = str.Substring(0, idx) + str[(idx + s.Length)..];
                    idx = str.IndexOf(s);
                }
            }

            return str;
        }

        internal static int? ToNullableInt(this string s)
        {
            if (int.TryParse(s, out int i)) return i;

            return null;
        }

        internal static bool IsOnlyOneNonNull(string value1 = null, string value2 = null, string value3 = null, string value4 = null, string value5 = null)
        {
            List<string> lst = new() { value1, value2, value3, value4, value5 };

            return (lst.Where(_ => _ != null).Count() == 1);
        }

        internal static bool IsAtLeastOneNonNull(string value1 = null, string value2 = null, string value3 = null, string value4 = null, string value5 = null, string value6 = null)
        {
            List<string> lst = new() { value1, value2, value3, value4, value5, value6 };

            return (lst.Where(_ => _ != null).Any());
        }

        private static readonly string[] _obscenities =
        {
            "arsehole", "asshole", "bullshit", "anal", "anus", "arse", "ballsack", "balls", "bastard", "bitch", "biatch", "bloody", "blowjob", "blow job", "bollock", "bollok", "boner", "boob", "bugger",
            "bum", "butt", "buttplug", "clitoris", "cock", "coon", "crap", "cunt", "damn", "dick", "dildo", "feck", "fellate", "fellatio", "felching",
            "fuck", "fucking", "fudgepacker", "fudge packer", "flange", "goddamn", "jizz", "knobend", "labia", "lmao",
            "lmfao", "muff", "nigger", "nigga", "penis", "piss", "poop", "prick", "pube", "pussy", "queer", "scrotum", "shit", "sh1t", "slut",
            "smegma", "spunk", "sucks", "sucked", "sucker", "tosser", "turd", "twat", "vagina", "wank", "whore", "wtf", "motherfucker", "mofos", "wanker", "tosspot"
        };

        internal static string ZeroToNone(this int number)
        {
            return number == 0 ? "none" : number.ToString();
        }

        internal static string ZeroToNo(this int number)
        {
            return number == 0 ? "No" : number.ToString();
        }

        internal static void GetFirstFamilyNameFromFullName(this string fullName, out string firstName, out string familyName)
        {
            firstName = "";
            familyName = "";

            if (string.IsNullOrEmpty(fullName))
            {
                return;
            }

            int idx = fullName.IndexOf(" ");

            if (idx >= 0)
            {
                firstName = fullName.Substring(0, idx);
                familyName = fullName[(idx + 1)..];
            }
            else
            {
                firstName = fullName;
            }

            return;
        }

        internal static bool AnyHaveContent(string string1 = null, string string2 = null, string string3 = null, string string4 = null, string string5 = null, string string6 = null, string string7 = null)
        {
            return
                !string.IsNullOrEmpty(string1) ||
                !string.IsNullOrEmpty(string2) ||
                !string.IsNullOrEmpty(string3) ||
                !string.IsNullOrEmpty(string4) ||
                !string.IsNullOrEmpty(string5) ||
                !string.IsNullOrEmpty(string6) ||
                !string.IsNullOrEmpty(string7);
        }

        internal static string GeneratePopupMessage(string text, string linkUrl = null, string linkText = null)
        {
            string popupText = "<div class='popup-text'>" + text + "</div>";

            if (linkUrl != null && linkText != null)
            {
                popupText += $"<div class='popup-link'><a href='{linkUrl}'>{linkText}</a></div>";
            }

            return popupText;
        }

        internal static string GeneratePopupMessage(List<string> paragraphs)
        {
            StringBuilder sb = new(paragraphs.Sum(_ => _.Length) + (10 * paragraphs.Count));

            foreach (var para in paragraphs)
            {
                sb.Append($"<p>{para}</p>");
            }

            return sb.ToString();
        }

        //internal static string Breakable(this string text)
        //{
        //    return text;
        //    //if (text.Contains(" "))
        //    //{
        //    //    string result = "";
        //    //    string[] parts = text.Split(' ');
        //    //    foreach (string part in parts)
        //    //    {
        //    //        result += $"{part}<span>&#8203;</span>";
        //    //    }
        //    //    return result;
        //    //}
        //    //else
        //    //{
        //    //    return text;
        //    //}
        //}

        //internal static string RemoveBreakable(this string text)
        //{
        //    if (text.Contains("<span>&#8203;</span>"))
        //    {
        //        return text.Replace("<span>&#8203;</span>", "");
        //    }
        //    else
        //    {
        //        return text;
        //    }
        //}

        internal static bool HasContent(this string text)
        {
            return !string.IsNullOrEmpty(text);
        }

        internal static bool ContainsContactDetails(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return false;
        }

        internal static string EncodeSpaces(this string text)
        {
            return text.Replace(" ", "%20");
        }

        internal static bool NoContent(this string text)
        {
            return string.IsNullOrEmpty(text);
        }

        internal static string InsertWhiteSpaceBefore(this string text, string separator, int fromMinLength)
        {
            if (text.Length < fromMinLength)
                return text;

            int idx = text.IndexOf(separator);

            if (idx > 0)
            {
                string sepText = text.Substring(0, idx) + " " + text[idx..];
                return sepText;
            }
            else
            {
                return text;
            }
        }

        internal static string TakeFirstSentence(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            int idx = text.IndexOf("\r");

            if (idx > 0)
            {
                string firstSentence = text.Substring(0, idx - 1);
                return firstSentence;
            }
            else
            {
                return text;
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

        internal static string ReplaceObscenities(string text, char withCharacter = '*')
        {
            if (ContainsObscenity(text, out List<string> obscenities))
            {
                string cleanText = text;

                foreach (string ob in obscenities.Distinct())
                {
                    cleanText = cleanText.Replace(ob, new string(withCharacter, ob.Length), true, System.Globalization.CultureInfo.CurrentCulture);
                }

                return cleanText;
            }
            else
            {
                return text;
            }
        }

        internal static bool ContainsObscenity(string text, out List<string> obscenities)
        {
            obscenities = new List<string>();

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string textLower = text.ToLower();

            char[] separators = { '\n', ',', '.', ' ' };
            string[] textSpilt = textLower.Split(separators);

            foreach (string obscenity in _obscenities)
            {
                if (textSpilt.Contains(obscenity))
                {
                    obscenities.Add(obscenity);
                }
            }

            return obscenities.Any();
        }

        internal static string CapitaliseFirstLetter(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            text = text.Trim();

            string firstCharacter = text.Substring(0, 1);

            if (firstCharacter.ToUpper() != firstCharacter)
            {
                return firstCharacter.ToUpper() + text[1..];
            }
            else
            {
                return text;
            }
        }

        internal static string SplitByCapitals(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            StringBuilder builder = new();
            foreach (char c in text)
            {
                if (char.IsUpper(c) && builder.Length > 0) builder.Append(' ');
                builder.Append(c);
            }
            return builder.ToString();
        }

        internal static string NewLinesToParagraphs(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string[] lines = text.Split("\n");

            StringBuilder sb = new(text.Length + lines.Length * 30);

            foreach (string line in lines)
            {
                if (line.Length > 0)
                {
                    sb.Append($"<p style='margin:0px'>{line}</p>");
                }
                else
                {
                    sb.Append($"<p style='margin:10px 0'>{line}</p>");
                }
            }

            string sbStr = sb.ToString();

            return sbStr;
        }

        //internal static string CrLfToParagraphs(this string text, bool removeWhitespaceParagraphs, bool removeDoubleSpaces)
        //{
        //    if (string.IsNullOrEmpty(text))
        //        return text;

        //    //string[] paras = text.Split(Environment.NewLine + Environment.NewLine);

        //    if (removeDoubleSpaces && text.Contains("  "))
        //    {
        //        text = text.Replace("  ", "");
        //    }

        //    string[] paras = text.Split(Environment.NewLine);

        //    StringBuilder sb = new(text.Length + paras.Length * 12);

        //    foreach (string para in paras)
        //    {
        //        if (para.Contains(Environment.NewLine))
        //        {
        //            string[] lines = para.Split(Environment.NewLine);

        //            foreach (string line in lines)
        //            {
        //                string style = "";

        //                if (line != lines.Last())
        //                {
        //                    style += "margin-bottom:0px;";
        //                }

        //                sb.Append($"<p style='{style}'>{line}</p>");
        //            }
        //        }
        //        else if (!string.IsNullOrEmpty(para))
        //        {
        //            sb.Append($"<p>{para}</p>");
        //        }
        //        else if (!removeWhitespaceParagraphs)
        //        {
        //            sb.Append($"<p>&nbsp;</p>");
        //        }
        //    }

        //    string sbStr = sb.ToString();

        //    return sbStr;
        //}

        internal static string SpacesToLineBreaks(this string text, bool lastSpaceOnly)
        {
            if (text == null)
                return text;

            if (lastSpaceOnly)
            {
                string textChanged = text.Trim();

                int idx = textChanged.LastIndexOf(" ");

                if (idx >= 0)
                {
                    textChanged = textChanged.Substring(0, idx) + Environment.NewLine + textChanged[idx..];
                    return textChanged;
                }
                else
                {
                    return text;
                }
            }
            else
            {
                return text.Replace(" ", Environment.NewLine);
            }
        }

        //internal static string CrLfToBr(this string text)
        //{
        //    if (text == null)
        //        return text;

        //    return text.Replace(Environment.NewLine, "<br/>");
        //}

        internal static string FormatMobileNumber(this string number)
        {
            number = FormatPhoneNumber(number);

            if (string.IsNullOrEmpty(number))
                return number;

            return number;
        }

        internal static string FormatFixedPhoneNumber(this string number)
        {
            number = FormatPhoneNumber(number);

            if (string.IsNullOrEmpty(number))
                return number;

            return number;
        }

        internal static string FormatPhoneNumber(this string number)
        {
            if (string.IsNullOrEmpty(number))
            {
                return number;
            }

            number = number.Trim();

            if (!string.IsNullOrEmpty(number))
            {
                number = number.Replace(" ", "").Replace("-", "");
            }

            if (number.StartsWith("00"))
            {
                number = "+" + number[2..];
            }

            //if (InputValidation.IsValidGenericPhone(number, out string _))
            //{
            //if (number.StartsWith("+"))
            //{
            //    string countryPrefix = number.Substring(0, 3);
            //    string remainder = number.Substring(3);
            //    number = countryPrefix + " " + remainder;
            //}
            //}

            return number;
        }

        internal static string FormatUniqueReference(this string reference)
        {
            if (reference == null)
            {
                return "Null reference";
            }
            else
            {
                reference = reference.ToUpper();
            }

            if (reference.Length == 8)
            {
                return reference.Substring(0, 4) + "-" + reference[4..];
            }
            else if (reference.Length == 9)
            {
                return reference.Substring(0, 3) + "-" + reference.Substring(3, 3) + "-" + reference[6..];
            }
            else if (reference.Length == 6)
            {
                return reference.Substring(0, 3) + "-" + reference.Substring(3, 3);
            }
            else
            {
                return "Invalid reference " + reference;
            }
        }

        internal static string RemoveUnSluggableCharacters(this string str)
        {
            char[] arr = str.Replace(",", "").ToCharArray();

            arr = Array.FindAll<char>(arr, (c => (char.IsLetterOrDigit(c)
                                                || char.IsWhiteSpace(c)
                                                //|| c == '.'
                                                //|| c == '_'
                                                || c == '-')));

            string slug = new string(arr).TrimEnd();

            slug = slug.TrimFull(' ');

            return slug;
        }

        //internal static string TrimInternal(this string str, char separator = ' ')
        //{
        //    return string.Join(" ", str.Split(new char[] { separator }, StringSplitOptions.RemoveEmptyEntries)); 
        //}

        /// <summary>
        /// Trims from start, end and interior of the string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        internal static string TrimFull(this string str, char separator = ' ')
        {
            return string.Join(" ", str.Split(new char[] { separator }, StringSplitOptions.RemoveEmptyEntries));
        }

        internal static string HaveHas(int number)
        {
            return number > 1 ? "have" : "has";
        }

        internal static string GenerateHtmlPreview(string html, bool stripHtml, int? maxLength = null, bool addDotDotDot = false)
        {
            try
            {
                string buffer;

                if (string.IsNullOrEmpty(html))
                    return null;

                if (stripHtml)
                {
                    buffer = StripHtmlTags(html);
                }
                else
                {
                    buffer = html;
                }

                if (maxLength != null)
                {
                    if (buffer.Length > (int)maxLength)
                    {
                        buffer = buffer.Substring(0, (int)maxLength);

                        if (addDotDotDot)
                        {
                            buffer += "...";
                        }
                    }
                }

                return buffer;
            }
            catch
            {
                return "[cannot generate preview]";
            }
        }

        private static string StripHtmlTags(string html)
        {
            try
            {
                int idx = 0;
                string destHtml = "";

                while (idx < html.Length)
                {
                    int nextOpen = html.IndexOf("<", idx);
                    int lenToCopy = nextOpen - idx;

                    if (lenToCopy > 0)
                    {
                        if (destHtml != "")
                        {
                            destHtml += " " + html.Substring(idx, lenToCopy);
                        }
                        else
                        {
                            destHtml = html.Substring(idx, lenToCopy);
                        }
                    }

                    int nextClose = html.IndexOf(">", nextOpen);

                    if (nextClose > 0)
                        idx = nextClose + 1;
                }

                return destHtml;
            }
            catch
            {
                return "[failed to strip html tags]";
            }
        }

        internal static string DoPageBlockEncoding(string text)
        {
            // [hr] to horizontal rule
            if (!string.IsNullOrEmpty(text) && text.IndexOf("[hr]") > -1)
            {
                text = text.Replace("[hr]", "<hr />");
            }

            //// [gallery:name] to gallery name partial
            //int galleryIdx = text.IndexOf("[gallery");

            //while (galleryIdx > -1)
            //{
            //    int idxEnd = text.IndexOf("]", galleryIdx);
            //    string galleryText = text.Substring(galleryIdx, idxEnd - galleryIdx + 1);
            //    string galleryName = galleryText.Substring(9, galleryText.Length - 10);

            //    text = text.Substring(0, galleryIdx) + $"<partial name='_GalleryByName', model='{galleryName}' />" + text.Substring(idxEnd + 1);

            //    galleryIdx = text.IndexOf("[gallery");
            //}

            return text;
        }

        //        internal static string FormatHTML(string html)
        //        {
        //#if DEBUG
        //            try
        //            {
        //                string unformattedHtml = $"<html>{html.Replace("&nbsp;","").Replace("<br>","<br />")}</html>";

        //                string formattedOutput = System.Xml.Linq.XElement.Parse(unformattedHtml).ToString();

        //                string cleanedOutput = unformattedHtml.Replace("<html>", "").Replace("</html>", "");

        //                return cleanedOutput;
        //            }
        //            catch
        //            {
        //                return html;
        //            }
        //#else
        //            return html;
        //#endif
        //        }
    }
}
