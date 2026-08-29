using System;
using UnityEngine;

namespace Game.Combat
{
    [Serializable]
    public struct EnemyLevelStats
    {
        [SerializeField, Min(1f)] private float maxHealth;
        [SerializeField, Min(0f)] private float attackDamage;
        [SerializeField, Min(0f)] private float moveSpeed;

        [SerializeField, Min(0)] private int coinReward;

        public float MaxHealth => maxHealth;
        public float AttackDamage => attackDamage;
        public float MoveSpeed => moveSpeed;
        public int CoinReward => coinReward;

        public EnemyLevelStats(float maxHealth, float attackDamage, float moveSpeed, int coinReward = 0)
        {
            this.maxHealth = Mathf.Max(1f, maxHealth);
            this.attackDamage = Mathf.Max(0f, attackDamage);
            this.moveSpeed = Mathf.Max(0f, moveSpeed);
            this.coinReward = Mathf.Max(0, coinReward);
        }
    }

    [CreateAssetMenu(fileName = "EnemyStats", menuName = "Game/Combat/Enemy Stats")]
    public sealed class EnemyStats : ScriptableObject
    {
        public const int MinimumThreatLevel = 1;
        public const int MaximumThreatLevel = 6;

        private static readonly EnemyLevelStats[] DefaultLevels =
        {
            new EnemyLevelStats(30f, 5f, 1.2f, 2),
            new EnemyLevelStats(45f, 7f, 1.25f, 4),
            new EnemyLevelStats(65f, 10f, 1.3f, 7),
            new EnemyLevelStats(90f, 14f, 1.35f, 11),
            new EnemyLevelStats(125f, 19f, 1.4f, 16),
            new EnemyLevelStats(170f, 25f, 1.45f, 22)
        };

        [SerializeField] private EnemyLevelStats[] levels =
        {
            new EnemyLevelStats(30f, 5f, 1.2f, 2),
            new EnemyLevelStats(45f, 7f, 1.25f, 4),
            new EnemyLevelStats(65f, 10f, 1.3f, 7),
            new EnemyLevelStats(90f, 14f, 1.35f, 11),
            new EnemyLevelStats(125f, 19f, 1.4f, 16),
            new EnemyLevelStats(170f, 25f, 1.45f, 22)
        };

        public EnemyLevelStats Get(int threatLevel)
        {
            int index = Mathf.Clamp(threatLevel, MinimumThreatLevel, MaximumThreatLevel) - 1;
            if (levels == null || levels.Length != MaximumThreatLevel ||
                levels[index].MaxHealth <= 0f || levels[index].CoinReward <= 0)
            {
                return DefaultLevels[index];
            }

            return levels[index];
        }

        public static EnemyLevelStats GetDefault(int threatLevel)
        {
            int index = Mathf.Clamp(threatLevel, MinimumThreatLevel, MaximumThreatLevel) - 1;
            return DefaultLevels[index];
        }

        public int GetCoinReward(int threatLevel)
        {
            return Get(threatLevel).CoinReward;
        }

        public static int GetMaximumThreatForDay(int day, int initialThreatLevel = 2)
        {
            int sanitizedDay = Mathf.Max(1, day);
            return Mathf.Clamp(
                initialThreatLevel + sanitizedDay - 1,
                MinimumThreatLevel,
                MaximumThreatLevel);
        }

        private void OnValidate()
        {
            if (levels == null || levels.Length != MaximumThreatLevel)
            {
                levels = (EnemyLevelStats[])DefaultLevels.Clone();
                return;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i].MaxHealth <= 0f)
                {
                    levels[i] = DefaultLevels[i];
                }
            }
        }
    }
}
