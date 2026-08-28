using System;

namespace Game.DayNight
{
    public readonly struct DayNightStateChanged
    {
        public readonly int Day;
        public readonly DayNightPhase Phase;
        public readonly float NightDurationSeconds;
        public readonly float NightRemainingSeconds;
        public readonly float NightRemainingRatio;

        public DayNightStateChanged(
            int day,
            DayNightPhase phase,
            float nightDurationSeconds,
            float nightRemainingSeconds,
            float nightRemainingRatio)
        {
            Day = day;
            Phase = phase;
            NightDurationSeconds = nightDurationSeconds;
            NightRemainingSeconds = nightRemainingSeconds;
            NightRemainingRatio = nightRemainingRatio;
        }
    }

    public readonly struct DayNightCompleted
    {
        public readonly int Day;

        public DayNightCompleted(int day)
        {
            Day = day;
        }
    }
}
