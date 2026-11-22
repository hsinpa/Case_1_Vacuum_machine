using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hsinpa.Director
{
    /// <summary>
    /// The <c>key: value</c> tags the runner reads itself. Keys and values ignore case, and the colon may be
    /// full-width (<c>hold：2</c>).
    /// <code>
    /// # mode: bark     at the top of a knot or file: play it as background talk
    /// # hold: 2.5      on a line: move on after 2.5 seconds ("2.5s" works too)
    /// </code>
    /// </summary>
    static class DialogueTags
    {
        public const string ModeKey = "mode";
        public const string HoldKey = "hold";
        public const string BarkValue = "bark";

        /// <summary>"key: value"; the key must be followed by a colon, so "holder" is not "hold".</summary>
        public static bool TryRead(string tag, string key, out string value)
        {
            value = null;
            string text = tag.Trim();
            if (!text.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return false;

            string rest = text.Substring(key.Length).TrimStart();
            if (rest.Length == 0 || (rest[0] != ':' && rest[0] != '：')) return false;

            value = rest.Substring(1).Trim();
            return true;
        }

        public static bool IsModeTag(string tag) => TryRead(tag, ModeKey, out _);

        /// <summary>
        /// The mode from a knot's or file's tags. <paramref name="unknownTag"/> is the first mode tag
        /// with a value other than bark, or null; such an event plays as a cutscene.
        /// </summary>
        public static DialogueMode ReadMode(IReadOnlyList<string> tags, out string unknownTag)
        {
            unknownTag = null;
            if (tags == null) return DialogueMode.Blocking;

            foreach (string tag in tags)
            {
                if (!TryRead(tag, ModeKey, out string value)) continue;
                if (string.Equals(value, BarkValue, StringComparison.OrdinalIgnoreCase)) return DialogueMode.Bark;
                unknownTag ??= tag.Trim();
            }
            return DialogueMode.Blocking;
        }

        /// <summary>A hold value such as "2.5" or "2.5s". Fails with a message the writer can act on.</summary>
        public static bool TryParseHold(string text, out float seconds, out string error)
        {
            error = null;
            if (text.EndsWith("s", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 1);

            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            {
                error = "hold needs a number of seconds, e.g. # hold: 2.5";
                return false;
            }
            if (seconds <= 0f)
            {
                seconds = 0f;
                error = "hold must be more than 0 seconds; leave the tag out to wait for the player";
                return false;
            }
            return true;
        }
    }
}
