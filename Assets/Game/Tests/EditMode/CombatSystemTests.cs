using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
            Assert.That(previous.MaxHealth, Is.EqualTo(90f));
            for (int level = 2; level <= EnemyStats.MaximumThreatLevel; level++)
            {
                EnemyLevelStats current = EnemyStats.GetDefault(level);
                Assert.That(current.MaxHealth, Is.GreaterThan(previous.MaxHealth));
                Assert.That(current.AttackDamage, Is.GreaterThan(previous.AttackDamage));
                previous = current;
            }
        }

        [Test]
        public void SpawnBudget_IncreasesWithThreatLevel()
        {
            int previous = EnemySpawner.GetSpawnLimitForThreat(1, 60);
            for (int level = 2; level <= EnemyStats.MaximumThreatLevel; level++)
            {
                int current = EnemySpawner.GetSpawnLimitForThreat(level, 60);
                Assert.That(current, Is.GreaterThan(previous));
                previous = current;
            }

            Assert.That(EnemySpawner.GetMaxAliveForThreat(6, 20), Is.GreaterThan(20));
        }

        [Test]
        public void SpawnDistanceRange_ExpandsBeyondOuterLightRadius()
        {
            Vector2 range = EnemySpawner.GetSpawnDistanceRange(6f, 12f, 20f, 0.5f);

            Assert.That(range.x, Is.EqualTo(20.5f).Within(0.0001f));
            Assert.That(range.y, Is.EqualTo(26.5f).Within(0.0001f));
        }

        [Test]
        public void ThreatScale_IncreasesNonLinearlyWithThreatLevel()
        {
            float levelOne = EnemyController.GetScaleMultiplier(1, false);
            float levelTwo = EnemyController.GetScaleMultiplier(2, false);
            float levelThree = EnemyController.GetScaleMultiplier(3, false);
            float levelSix = EnemyController.GetScaleMultiplier(6, false);

            Assert.That(levelOne, Is.GreaterThan(1f));
            Assert.That(levelTwo, Is.GreaterThan(levelOne));
            Assert.That(levelThree, Is.GreaterThan(levelTwo));
            Assert.That(levelSix, Is.GreaterThan(levelThree));
            Assert.That(levelThree - levelTwo, Is.GreaterThan(levelTwo - levelOne));
        }

        [Test]
        public void BossScale_IsLargerThanSameThreatEnemy()
        {
            float normal = EnemyController.GetScaleMultiplier(6, false, 0.1f, 1.5f, 1f, 1f);
            float boss = EnemyController.GetScaleMultiplier(6, true, 0.1f, 1.5f, 1f, 1f);

            Assert.That(boss, Is.GreaterThan(normal));
            Assert.That(normal, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(boss, Is.EqualTo(2.25f).Within(0.0001f));
        }

        [TestCase(-10, 1f)]
        [TestCase(99, 1.5f)]
        public void ThreatScale_ClampsThreatLevel(int threatLevel, float expected)
        {
            Assert.That(
                EnemyController.GetScaleMultiplier(threatLevel, false, 0.1f, 1.5f, 1f, 1f),
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
        public void MainTower_IndependentUpgradesChangeRealCombatStats()
        {
            GameObject towerObject = new GameObject("Main Tower Test");
            Health health = towerObject.AddComponent<Health>();
            MainTower tower = towerObject.AddComponent<MainTower>();

            Assert.That(tower.AttackDamage, Is.EqualTo(15f));
            Assert.That(tower.AttackRange, Is.EqualTo(4f));
            Assert.That(health.MaxHealth, Is.EqualTo(500f));

            health.TakeDamage(100f);
            Assert.That(tower.UpgradeRange(), Is.True);
            Assert.That(tower.RangeUpgradeLevel, Is.EqualTo(1));
            Assert.That(tower.QualityUpgradeLevel, Is.Zero);
            Assert.That(tower.AttackRange, Is.EqualTo(4.25f).Within(0.0001f));
            Assert.That(tower.AttackDamage, Is.EqualTo(15f));

            Assert.That(tower.UpgradeQuality(), Is.True);
            Assert.That(tower.QualityUpgradeLevel, Is.EqualTo(1));
            Assert.That(tower.AttackDamage, Is.EqualTo(18f));
            Assert.That(health.MaxHealth, Is.EqualTo(570f));
            Assert.That(health.CurrentHealth, Is.EqualTo(470f));

            Object.DestroyImmediate(towerObject);
        }

        [Test]
        public void MainTower_UpgradesStopAtTenLevels()
        {
            GameObject towerObject = new GameObject("Main Tower Upgrade Limit Test");
            towerObject.AddComponent<Health>();
            MainTower tower = towerObject.AddComponent<MainTower>();

            for (int i = 0; i < 10; i++)
            {
                Assert.That(tower.UpgradeQuality(), Is.True);
                Assert.That(tower.UpgradeRange(), Is.True);
            }

            Assert.That(tower.UpgradeQuality(), Is.False);
            Assert.That(tower.UpgradeRange(), Is.False);
            Assert.That(tower.QualityUpgradeLevel, Is.EqualTo(10));
            Assert.That(tower.RangeUpgradeLevel, Is.EqualTo(10));
            Assert.That(tower.AttackDamage, Is.EqualTo(45f));
            Assert.That(tower.AttackRange, Is.EqualTo(6.5f).Within(0.0001f));
            Assert.That(tower.Health.MaxHealth, Is.EqualTo(1200f));

            Object.DestroyImmediate(towerObject);
        }

        [Test]
        public void MainTower_AttackPathUsesCurrentAttackDamage()
        {
            GameObject towerObject = new GameObject("Main Tower Attack Test");
            towerObject.AddComponent<Health>();
            MainTower tower = towerObject.AddComponent<MainTower>();

            GameObject enemyObject = new GameObject("Enemy Attack Target Test");
            enemyObject.transform.position = towerObject.transform.position;
            enemyObject.AddComponent<Health>();
            EnemyController enemy = enemyObject.AddComponent<EnemyController>();
            MethodInfo enemyAwake = typeof(EnemyController).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(enemyAwake, Is.Not.Null);
            enemyAwake.Invoke(enemy, null);
            enemy.Initialize(tower.Health, EnemyStats.GetDefault(1), 1, false, 1f, 1f, null);
            float healthBefore = enemy.Health.CurrentHealth;

            MethodInfo update = typeof(MainTower).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(tower, null);

            Assert.That(
                enemy.Health.CurrentHealth,
                Is.EqualTo(healthBefore - tower.AttackDamage).Within(0.0001f));

            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(towerObject);
        }

        [Test]
        public void MainTower_UpgradeCostsFollowConfiguredCurve()
        {
            int[] expectedCosts = { 20, 30, 45, 65, 90, 120, 155, 195, 240, 290 };
            GameObject towerObject = new GameObject("Main Tower Cost Test");
            towerObject.AddComponent<Health>();
            MainTower tower = towerObject.AddComponent<MainTower>();

            for (int i = 0; i < expectedCosts.Length; i++)
            {
                Assert.That(tower.NextQualityUpgradeCost, Is.EqualTo(expectedCosts[i]));
                Assert.That(tower.NextRangeUpgradeCost, Is.EqualTo(expectedCosts[i]));
                Assert.That(tower.UpgradeQuality(), Is.True);
                Assert.That(tower.UpgradeRange(), Is.True);
            }

            Assert.That(tower.NextQualityUpgradeCost, Is.Zero);
            Assert.That(tower.NextRangeUpgradeCost, Is.Zero);
            Object.DestroyImmediate(towerObject);
        }

        [Test]
        public void EnemyStatsAsset_MatchesRuntimeDefaults()
        {
            EnemyStats asset = AssetDatabase.LoadAssetAtPath<EnemyStats>(
                "Assets/Game/Combat/Data/EnemyStats.asset");
            Assert.That(asset, Is.Not.Null);

            for (int level = EnemyStats.MinimumThreatLevel;
                 level <= EnemyStats.MaximumThreatLevel;
                 level++)
            {
                EnemyLevelStats expected = EnemyStats.GetDefault(level);
                EnemyLevelStats actual = asset.Get(level);
                Assert.That(actual.MaxHealth, Is.EqualTo(expected.MaxHealth));
                Assert.That(actual.AttackDamage, Is.EqualTo(expected.AttackDamage));
                Assert.That(actual.MoveSpeed, Is.EqualTo(expected.MoveSpeed));
                Assert.That(actual.CoinReward, Is.EqualTo(expected.CoinReward));
            }
        }

        [Test]
        public void DefaultStats_UseRaisedHealthWithoutChangingAttack()
        {
            EnemyLevelStats levelOne = EnemyStats.GetDefault(1);
            EnemyLevelStats levelSix = EnemyStats.GetDefault(6);

            Assert.That(levelOne.MaxHealth, Is.EqualTo(90f));
            Assert.That(levelSix.MaxHealth, Is.EqualTo(675f));
            Assert.That(levelSix.AttackDamage, Is.EqualTo(25f));
        }

        [Test]
        public void Health_IncreaseMaximumPreservesExistingDamage()
        {
            GameObject owner = new GameObject("Health Growth Test");
            Health health = owner.AddComponent<Health>();
            health.ResetHealth(500f);
            health.TakeDamage(100f);

            health.IncreaseMaximumHealth(570f);

            Assert.That(health.MaxHealth, Is.EqualTo(570f));
            Assert.That(health.CurrentHealth, Is.EqualTo(470f));
            Assert.That(health.IsDead, Is.False);
            Object.DestroyImmediate(owner);
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
