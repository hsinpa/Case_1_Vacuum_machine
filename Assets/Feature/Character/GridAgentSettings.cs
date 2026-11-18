namespace Hsinpa.Character
{
    /// <summary>
    /// Pathfinding behaviour shared by every GridAgent. Per-character tuning (speed) stays on the agent.
    /// </summary>
    static class GridAgentSettings
    {
        /// <summary>Cells one search may expand before giving up. Bounds the cost of goals sealed off by characters.</summary>
        public const int MaxSearchCells = 2000;

        /// <summary>Seconds to wait before retrying when the next cell is occupied.</summary>
        public const float BlockedWaitTime = 0.3f;

        /// <summary>Extra random wait, as a fraction of BlockedWaitTime, so two blocked agents do not retry in lockstep.</summary>
        public const float BlockedWaitJitter = 0.5f;

        /// <summary>Retries (wait, then re-plan) before giving up on a blocked path.</summary>
        public const int MaxBlockedRetries = 3;
    }
}
