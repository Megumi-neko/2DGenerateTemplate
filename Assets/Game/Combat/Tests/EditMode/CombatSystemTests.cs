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
