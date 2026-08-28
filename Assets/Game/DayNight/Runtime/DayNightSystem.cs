using System;
using UnityEngine;

namespace Game.DayNight
{
    [AddComponentMenu("Game/Day Night/Day Night System")]
    [DisallowMultipleComponent]
    public sealed class DayNightSystem : MonoBehaviour
    {
        public const int TotalDays = 5;

        private static readonly float[] DefaultNightDurationsMinutes =
        {
            1f,
            2f,
            4f,
            7f,
            11f
        };

        [SerializeField] private float[] nightDurationsMinutes =
        {
            1f,
            2f,
            4f,
            7f,
            11f
        };
        [SerializeField] private bool useUnscaledTime;

        private int currentDay = 1;
        private DayNightPhase currentPhase = DayNightPhase.Day;
        private float nightRemainingSeconds;
        private bool initialized;

        public int CurrentDay => currentDay;
        public DayNightPhase CurrentPhase => currentPhase;
        public bool IsCompleted => currentPhase == DayNightPhase.Completed;
        public bool UseUnscaledTime => useUnscaledTime;
        public float NightDurationSeconds => GetNightDurationSeconds(currentDay);
        public float NightRemainingSeconds => nightRemainingSeconds;
        public float NightRemainingRatio => currentPhase == DayNightPhase.Night
            ? Mathf.Clamp01(nightRemainingSeconds / NightDurationSeconds)
            : 0f;

        private void Awake()
        {
            InitializeState();
        }

        private void OnValidate()
        {
            SanitizeDurationConfiguration();
        }

        private void Update()
        {
            float deltaSeconds = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            AdvanceTime(deltaSeconds);
        }

        public bool EndDay()
        {
            EnsureInitialized();
            if (currentPhase != DayNightPhase.Day || currentDay > TotalDays)
            {
                return false;
            }

            currentPhase = DayNightPhase.Night;
            nightRemainingSeconds = NightDurationSeconds;
            PublishStateChanged();
            return true;
        }

        internal void AdvanceTime(float deltaSeconds)
        {
            if (!initialized || currentPhase != DayNightPhase.Night ||
                !IsFinite(deltaSeconds) || deltaSeconds <= 0f)
            {
                return;
            }

            nightRemainingSeconds = Mathf.Max(0f, nightRemainingSeconds - deltaSeconds);
            if (nightRemainingSeconds > 0f)
            {
                return;
            }

            FinishNight();
        }

        internal void InitializeForTests()
        {
            SanitizeDurationConfiguration();
            InitializeState();
        }

        internal void SetNightDurationsForTests(float[] durationsMinutes)
        {
            nightDurationsMinutes = durationsMinutes == null
                ? null
                : (float[])durationsMinutes.Clone();
            SanitizeDurationConfiguration();
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                InitializeState();
            }
        }

        private void InitializeState()
        {
            SanitizeDurationConfiguration();
            currentDay = 1;
            currentPhase = DayNightPhase.Day;
            nightRemainingSeconds = 0f;
            initialized = true;
            PublishStateChanged();
        }

        private void FinishNight()
        {
            nightRemainingSeconds = 0f;
            if (currentDay < TotalDays)
            {
                currentDay++;
                currentPhase = DayNightPhase.Day;
                PublishStateChanged();
                return;
            }

            currentPhase = DayNightPhase.Completed;
            PublishStateChanged();
            EventBus.Instance.Publish(new DayNightCompleted(currentDay));
        }

        private void PublishStateChanged()
        {
            EventBus.Instance.Publish(new DayNightStateChanged(
                currentDay,
                currentPhase,
                NightDurationSeconds,
                nightRemainingSeconds,
                NightRemainingRatio));
        }

        private float GetNightDurationSeconds(int day)
        {
            int index = Mathf.Clamp(day - 1, 0, TotalDays - 1);
            return nightDurationsMinutes[index] * 60f;
        }

        private void SanitizeDurationConfiguration()
        {
            if (nightDurationsMinutes == null || nightDurationsMinutes.Length != TotalDays)
            {
                float[] configuredDurations = new float[TotalDays];
                int copyCount = nightDurationsMinutes == null
                    ? 0
                    : Mathf.Min(nightDurationsMinutes.Length, TotalDays);

                for (int i = 0; i < TotalDays; i++)
                {
                    configuredDurations[i] = i < copyCount &&
                        IsValidDuration(nightDurationsMinutes[i])
                        ? nightDurationsMinutes[i]
                        : DefaultNightDurationsMinutes[i];
                }

                nightDurationsMinutes = configuredDurations;
                return;
            }

            for (int i = 0; i < TotalDays; i++)
            {
                if (!IsValidDuration(nightDurationsMinutes[i]))
                {
                    nightDurationsMinutes[i] = DefaultNightDurationsMinutes[i];
                }
            }
        }

        private static bool IsValidDuration(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
