using System.Collections.Generic;
using Game.DayNight;
using NUnit.Framework;
using UnityEngine;

namespace Game.DayNight.Tests
{
    public sealed class DayNightSystemTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private readonly List<DayNightStateChanged> stateChanges = new List<DayNightStateChanged>();
        private int completionCount;

        [SetUp]
        public void SetUp()
        {
            EventBus.Instance.Subscribe<DayNightStateChanged>(OnStateChanged);
            EventBus.Instance.Subscribe<DayNightCompleted>(OnCompleted);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnStateChanged);
            EventBus.Instance.UnSubscribe<DayNightCompleted>(OnCompleted);

            foreach (GameObject createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
            stateChanges.Clear();
            completionCount = 0;
        }

        [Test]
        public void InitialState_IsFirstDaytime()
        {
            DayNightSystem system = CreateSystem();

            Assert.That(system.CurrentDay, Is.EqualTo(1));
            Assert.That(system.CurrentPhase, Is.EqualTo(DayNightPhase.Day));
            Assert.That(system.NightRemainingSeconds, Is.Zero);
            Assert.That(system.IsCompleted, Is.False);
        }

        [Test]
        public void EndDay_StartsNightWithConfiguredDuration()
        {
            DayNightSystem system = CreateSystem();

            Assert.That(system.EndDay(), Is.True);

            Assert.That(system.CurrentPhase, Is.EqualTo(DayNightPhase.Night));
            Assert.That(system.NightDurationSeconds, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(system.NightRemainingSeconds, Is.EqualTo(60f).Within(0.0001f));
        }

        [Test]
        public void EndDay_DoesNothingWhileNightIsActive()
        {
            DayNightSystem system = CreateSystem();
            system.EndDay();
            system.AdvanceTime(12f);

            Assert.That(system.EndDay(), Is.False);
            Assert.That(system.NightRemainingSeconds, Is.EqualTo(48f).Within(0.0001f));
        }

        [Test]
        public void NightCompletion_AdvancesToNextDaytimeWithoutNegativeTime()
        {
            DayNightSystem system = CreateSystem();
            system.EndDay();

            system.AdvanceTime(60.5f);

            Assert.That(system.CurrentDay, Is.EqualTo(2));
            Assert.That(system.CurrentPhase, Is.EqualTo(DayNightPhase.Day));
            Assert.That(system.NightRemainingSeconds, Is.Zero);
        }

        [Test]
        public void NightDurations_AreOneTwoFourSevenAndElevenMinutes()
        {
            DayNightSystem system = CreateSystem();
            float[] expectedSeconds = { 60f, 120f, 240f, 420f, 660f };

            for (int i = 0; i < expectedSeconds.Length; i++)
            {
                Assert.That(system.EndDay(), Is.True);
                Assert.That(system.NightDurationSeconds, Is.EqualTo(expectedSeconds[i]).Within(0.0001f));
                system.AdvanceTime(expectedSeconds[i]);
            }

            Assert.That(system.CurrentDay, Is.EqualTo(5));
            Assert.That(system.CurrentPhase, Is.EqualTo(DayNightPhase.Completed));
        }

        [Test]
        public void FifthNightCompletion_EntersCompletedOnceAndIgnoresFurtherCommands()
        {
            DayNightSystem system = CreateSystem();
            float[] durations = { 60f, 120f, 240f, 420f, 660f };

            for (int i = 0; i < durations.Length; i++)
            {
                Assert.That(system.EndDay(), Is.True);
                system.AdvanceTime(durations[i]);
            }

            int stateChangeCount = stateChanges.Count;
            system.AdvanceTime(1000f);

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(system.EndDay(), Is.False);
            Assert.That(system.CurrentDay, Is.EqualTo(5));
            Assert.That(system.CurrentPhase, Is.EqualTo(DayNightPhase.Completed));
            Assert.That(stateChanges.Count, Is.EqualTo(stateChangeCount));
        }

        [Test]
        public void StateEvent_ContainsCurrentPhaseAndRemainingTime()
        {
            DayNightSystem system = CreateSystem();
            stateChanges.Clear();

            system.EndDay();

            DayNightStateChanged state = stateChanges[stateChanges.Count - 1];
            Assert.That(state.Day, Is.EqualTo(1));
            Assert.That(state.Phase, Is.EqualTo(DayNightPhase.Night));
            Assert.That(state.NightDurationSeconds, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(state.NightRemainingSeconds, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(state.NightRemainingRatio, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void InvalidDurationConfiguration_FallsBackToSafeDefaults()
        {
            DayNightSystem system = CreateSystem();
            system.SetNightDurationsForTests(new[] { -1f, float.NaN });

            Assert.That(system.EndDay(), Is.True);
            Assert.That(system.NightDurationSeconds, Is.EqualTo(60f).Within(0.0001f));
        }

        private DayNightSystem CreateSystem()
        {
            GameObject systemObject = new GameObject("Day Night System Test");
            createdObjects.Add(systemObject);
            DayNightSystem system = systemObject.AddComponent<DayNightSystem>();
            system.InitializeForTests();
            stateChanges.Clear();
            completionCount = 0;
            return system;
        }

        private void OnStateChanged(DayNightStateChanged state)
        {
            stateChanges.Add(state);
        }

        private void OnCompleted(DayNightCompleted completed)
        {
            completionCount++;
        }
    }
}
