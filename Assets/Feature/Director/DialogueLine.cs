using System.Collections.Generic;

namespace Hsinpa.Director
{
    /// <summary>One line of dialogue after commands and the speaker have been taken out.</summary>
    public readonly struct DialogueLine
    {
        /// <summary>The actor id before the colon, or null for narration.</summary>
        public readonly string Speaker;
        public readonly string Text;
        /// <summary>Tags the runner does not use, e.g. "style: shout"; the visuals pick how the line looks from them.</summary>
        public readonly IReadOnlyList<string> Tags;
        public readonly DialogueMode Mode;
        /// <summary>Seconds until the line moves on by itself, or 0 when it waits for the player.</summary>
        public readonly float AutoAdvanceSeconds;

        public bool WaitsForPlayer => AutoAdvanceSeconds <= 0f;

        public DialogueLine(string speaker, string text, IReadOnlyList<string> tags, DialogueMode mode, float autoAdvanceSeconds)
        {
            Speaker = speaker;
            Text = text;
            Tags = tags;
            Mode = mode;
            AutoAdvanceSeconds = autoAdvanceSeconds;
        }
    }
}
