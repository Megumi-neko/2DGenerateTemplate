namespace Game.UI
{
    public enum EndOutcome { GameOver, Victory }

    public static class EndSceneFlow
    {
        public static EndOutcome PendingOutcome { get; private set; } = EndOutcome.GameOver;
        public static bool HasPendingOutcome { get; private set; }
        public static void SetOutcome(EndOutcome outcome) { PendingOutcome = outcome; HasPendingOutcome = true; }
        public static EndOutcome ConsumeOutcome(EndOutcome fallback)
        {
            EndOutcome result = HasPendingOutcome ? PendingOutcome : fallback;
            HasPendingOutcome = false;
            return result;
        }
    }
}
