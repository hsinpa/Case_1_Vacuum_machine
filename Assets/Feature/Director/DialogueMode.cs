namespace Hsinpa.Director
{
    /// <summary>How an event plays, set by a <c># mode: ...</c> tag at the top of its knot or file.</summary>
    public enum DialogueMode
    {
        /// <summary>No mode tag: a cutscene. Player input is off and lines wait for the player.</summary>
        Blocking,
        /// <summary><c># mode: bark</c>: background talk. Player input stays on and lines move on by themselves.</summary>
        Bark,
    }
}
