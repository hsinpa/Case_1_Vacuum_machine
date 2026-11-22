using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hsinpa.Director
{
    /// <summary>A parsed command tag, e.g. <c># func walk_to("koren", 12, 4)</c>.</summary>
    public readonly struct CommandCall
    {
        public readonly string Name;
        /// <summary>Each argument is a string, an int, a float or a bool.</summary>
        public readonly object[] Args;
        /// <summary>The tag as written, for error messages.</summary>
        public readonly string Source;

        public CommandCall(string name, object[] args, string source)
        {
            Name = name;
            Args = args;
            Source = source;
        }
    }

    public enum ParseResult
    {
        /// <summary>Not a command (no "func" prefix): a display tag such as "wait:1".</summary>
        NotCommand,
        Command,
        Error,
    }

    /// <summary>
    /// Parses ink tags of the form <c>func name(arg, arg, ...)</c>.
    /// Literals: <c>"text"</c> is a string, <c>12</c> or <c>-1</c> an int, <c>0.5f</c>, <c>2f</c> or <c>0.5</c> a float,
    /// <c>true</c> / <c>false</c> a bool.
    /// Full-width punctuation typed by a Chinese IME （ ）， “ ” is accepted as ( ) , " — but only outside
    /// strings, so the text inside quotes is kept exactly as written.
    /// Strings cannot contain the quote that opened them; ink itself forbids # and { } inside tags.
    /// </summary>
    public static class CommandParser
    {
        public const string Prefix = "func";

        public static ParseResult Parse(string tag, out CommandCall call, out string error)
        {
            call = default;
            error = null;
            if (string.IsNullOrWhiteSpace(tag)) return ParseResult.NotCommand;

            string text = NormalizeOutsideStrings(tag).Trim();

            if (text == Prefix)
                return Fail($"'{Prefix}' needs a command after it, e.g. {Prefix} wait(1f)", out error);

            // "func" followed by whitespace; "function ..." or "funcX" are not commands.
            if (!text.StartsWith(Prefix, StringComparison.Ordinal) || text.Length == Prefix.Length || !char.IsWhiteSpace(text[Prefix.Length]))
            {
                // "# wait_for("koren")" would otherwise be silently kept as a display tag.
                if (LooksLikeCall(text))
                    return Fail($"looks like a command but is missing '{Prefix}'; write # {Prefix} {text}", out error);
                return ParseResult.NotCommand;
            }

            text = text.Substring(Prefix.Length).Trim();

            int open = text.IndexOf('(');
            if (open < 0 || !text.EndsWith(")", StringComparison.Ordinal))
                return Fail("expected name(arguments), e.g. func wait(1f)", out error);

            string name = text.Substring(0, open).Trim();
            if (!IsIdentifier(name))
                return Fail($"'{name}' is not a valid command name", out error);

            string argText = text.Substring(open + 1, text.Length - open - 2);
            if (!TryParseArgs(argText, out object[] args, out error))
                return ParseResult.Error;

            call = new CommandCall(name, args, tag.Trim());
            return ParseResult.Command;
        }

        static ParseResult Fail(string message, out string error)
        {
            error = message;
            return ParseResult.Error;
        }

        // Outside strings, full-width punctuation becomes ASCII and every kind of quote becomes ".
        // Inside a string nothing changes, except its closing quote: a string opened with " ends only at ",
        // one opened with a full-width quote ends at ” ＂ or ".
        static string NormalizeOutsideStrings(string s)
        {
            var sb = new StringBuilder(s.Length);
            char opener = '\0';
            foreach (char c in s)
            {
                if (opener == '\0')
                {
                    char ascii = c switch
                    {
                        '（' => '(',
                        '）' => ')',
                        '，' => ',',
                        '“' or '”' or '＂' => '"',
                        _ => c,
                    };
                    if (ascii == '"') opener = c;
                    sb.Append(ascii);
                }
                else if (ClosesString(opener, c))
                {
                    opener = '\0';
                    sb.Append('"');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        static bool ClosesString(char opener, char c) =>
            opener == '"' ? c == '"' : c == '"' || c == '”' || c == '＂';

        static bool LooksLikeCall(string text)
        {
            int open = text.IndexOf('(');
            return open > 0 && text.EndsWith(")", StringComparison.Ordinal) && IsIdentifier(text.Substring(0, open).Trim());
        }

        static bool IsIdentifier(string s)
        {
            if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_')) return false;
            foreach (char c in s)
                if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
            return true;
        }

        static bool TryParseArgs(string text, out object[] args, out string error)
        {
            args = Array.Empty<object>();
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            // Split on commas outside quotes. After normalizing, the only quote left is ".
            var tokens = new List<string>();
            var current = new StringBuilder();
            bool inString = false;
            foreach (char c in text)
            {
                if (c == '"') inString = !inString;
                if (c == ',' && !inString)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            if (inString)
            {
                error = "a string is missing its closing \"";
                return false;
            }
            tokens.Add(current.ToString());

            var values = new object[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!TryParseLiteral(tokens[i].Trim(), out values[i], out string literalError))
                {
                    error = $"argument {i + 1}: {literalError}";
                    return false;
                }
            }
            args = values;
            return true;
        }

        static bool TryParseLiteral(string token, out object value, out string error)
        {
            value = null;
            error = null;

            if (token.Length == 0)
            {
                error = "empty argument";
                return false;
            }

            if (token[0] == '"')
            {
                if (token.Length < 2 || token[token.Length - 1] != '"')
                {
                    error = $"{token} has text after its closing quote";
                    return false;
                }
                value = token.Substring(1, token.Length - 2);
                return true;
            }

            if (token == "true" || token == "false")
            {
                value = token == "true";
                return true;
            }

            if (int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int i))
            {
                value = i;
                return true;
            }

            string number = token.EndsWith("f", StringComparison.OrdinalIgnoreCase) ? token.Substring(0, token.Length - 1) : token;
            bool looksNumeric = number.Length > 0 && (char.IsDigit(number[0]) || number[0] == '-' || number[0] == '.');
            if (looksNumeric && float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                value = f;
                return true;
            }

            error = $"'{token}' is not a value; strings need double quotes, e.g. \"{token}\"";
            return false;
        }
    }
}
