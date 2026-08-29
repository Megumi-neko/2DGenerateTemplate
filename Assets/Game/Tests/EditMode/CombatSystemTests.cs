using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    public sealed class CombatSystemTests
    {
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        [TestCase(3, 4)]
        [TestCase(4, 5)]
        [TestCase(5, 6)]
        [TestCase(99, 6)]
        public void MaximumThreat_IncreasesFromTwoToSix(int day, int expected)
        {
            Assert.That(EnemyStats.GetMaximumThreatForDay(day), Is.EqualTo(expected));
        }

        [Test]
        public void DefaultStats_IncreaseHealthAndAttackAcrossLevels()
        {
            EnemyLevelStats previous = EnemyStats.GetDefault(1);
            for (int level = 2; level <= EnemyStats.MaximumThreatLevel; level++)
            {
                EnemyLevelStats current = EnemyStats.GetDefault(level);
                Assert.That(current.MaxHealth, Is.GreaterThan(previous.MaxHealth));
                Assert.That(current.AttackDamage, Is.GreaterThan(previous.AttackDamage));
                previous = current;
            }
        }

        [Test]
        public void ThreatScale_IncreasesWithThreatLevel()
        {
            float previous = EnemyController.GetScaleMultiplier(1, false, 0.1f);
            for (int level = 2; level <= EnemyStats.MaximumThreatLevel; level++)
            {
                float current = EnemyController.GetScaleMultiplier(level, false, 0.1f);
                Assert.That(current, Is.GreaterThan(previous));
                previous = current;
            }
        }

        [Test]
        public void BossScale_IsLargerThanSameThreatEnemy()
        {
            float normal = EnemyController.GetScaleMultiplier(6, false, 0.1f, 1.5f);
            float boss = EnemyController.GetScaleMultiplier(6, true, 0.1f, 1.5f);

            Assert.That(boss, Is.GreaterThan(normal));
            Assert.That(normal, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(boss, Is.EqualTo(2.25f).Within(0.0001f));
        }

        [TestCase(-10, 1f)]
        [TestCase(99, 1.5f)]
        public void ThreatScale_ClampsThreatLevel(int threatLevel, float expected)
        {
            Assert.That(
                EnemyController.GetScaleMultiplier(threatLevel, false, 0.1f),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void BossTrigger_OnlyFiresOnceAtOrAfterHalfNight()
        {
            Assert.That(EnemySpawner.ShouldSpawnBoss(true, false, 0.51f), Is.False);
            Assert.That(EnemySpawner.ShouldSpawnBoss(true, false, 0.5f), Is.True);
            Assert.That(EnemySpawner.ShouldSpawnBoss(true, true, 0.25f), Is.False);
            Assert.That(EnemySpawner.ShouldSpawnBoss(false, false, 0.25f), Is.False);
        }

        [Test]
        public void Health_DiesOnceAndCanBeResetForPooling()
        {
            GameObject owner = new GameObject("Health Test");
            Health health = owner.AddComponent<Health>();
            int deathCount = 0;
            health.Died += _ => deathCount++;

            health.ResetHealth(10f);
            Assert.That(health.TakeDamage(4f), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(6f));
            Assert.That(health.TakeDamage(20f), Is.True);
            Assert.That(health.IsDead, Is.True);
            Assert.That(deathCount, Is.EqualTo(1));
            Assert.That(health.TakeDamage(1f), Is.False);
            Assert.That(deathCount, Is.EqualTo(1));

            health.ResetHealth(25f);
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(25f));

            Object.DestroyImmediate(owner);
        }
    }
}
