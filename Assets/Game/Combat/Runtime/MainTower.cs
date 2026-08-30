using Game.BaseSystem;
using UnityEngine;

namespace Game.Combat
{
    [AddComponentMenu("Game/Combat/Main Tower")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class MainTower : MonoBehaviour
    {
        [Header("Independent Upgrades")]
        [SerializeField, Range(0, 10)] private int qualityUpgradeLevel;
        [SerializeField, Range(0, 10)] private int rangeUpgradeLevel;
        [SerializeField, Min(1)] private int maximumUpgradeLevel = 10;
        [SerializeField] private int[] upgradeCosts = { 20, 30, 45, 65, 90, 120, 155, 195, 240, 290 };

        [Header("Quality Growth")]
        [SerializeField, Min(1f)] private float baseMaxHealth = 500f;
        [SerializeField, Min(0f)] private float maxHealthPerQualityLevel = 70f;
        [SerializeField, Min(0f)] private float baseAttackDamage = 15f;
        [SerializeField, Min(0f)] private float attackDamagePerQualityLevel = 3f;

        [Header("Height Growth")]
        [SerializeField, Min(0.1f)] private float baseAttackRange = 4f;
        [SerializeField, Min(0f)] private float attackRangePerLevel = 0.25f;
        [SerializeField, Min(0.05f)] private float attackInterval = 0.5f;
        [SerializeField] private Transform attackOrigin;

        private Health health;
        private float attackCooldown;
        private bool statsInitialized;
        private float attackDamage;
        private float attackRange;

        public Health Health
        {
            get
            {
                InitializeStats();
                return health;
            }
        }
        public int QualityUpgradeLevel => qualityUpgradeLevel;
        public int RangeUpgradeLevel => rangeUpgradeLevel;
        public int MaximumUpgradeLevel => maximumUpgradeLevel;
        public float AttackDamage
        {
            get
            {
                InitializeStats();
                return attackDamage;
            }
            private set => attackDamage = value;
        }
        public float AttackRange
        {
            get
            {
                InitializeStats();
                return attackRange;
            }
            private set => attackRange = value;
        }
        public int NextQualityUpgradeCost => GetUpgradeCost(qualityUpgradeLevel);
        public int NextRangeUpgradeCost => GetUpgradeCost(rangeUpgradeLevel);
        public bool CanUpgradeQuality => qualityUpgradeLevel < maximumUpgradeLevel;
        public bool CanUpgradeRange => rangeUpgradeLevel < maximumUpgradeLevel;

        public event System.Action<int, int> QualityUpgraded;
        public event System.Action<int, int> RangeUpgraded;

        private void Awake()
        {
            InitializeStats();
        }

        private void OnEnable()
        {
            InitializeStats();
        }

        private void InitializeStats()
        {
            if (statsInitialized)
            {
                return;
            }

            health = GetComponent<Health>();
            if (health != null)
            {
                health.Damaged += OnDamaged;
            }

            ApplyStats(true);
            statsInitialized = true;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(Health _, float amount)
        {
            if (amount > 0f)
            {
                CameraShakeController.ShakeMainCamera();
                CandleHitFlash.FlashMainCandle();
            }
        }

        private void Update()
        {
            if (health == null || health.IsDead)
            {
                return;
            }

            attackCooldown -= Time.deltaTime;
            if (attackCooldown > 0f)
            {
                return;
            }

            EnemyController target = FindNearestEnemy();
            if (target == null)
            {
                return;
            }

            target.TakeDamage(AttackDamage);
            attackCooldown = attackInterval;
        }

        public bool UpgradeQuality()
        {
            InitializeStats();
            if (!CanUpgradeQuality)
            {
                return false;
            }

            qualityUpgradeLevel++;
            ApplyStats(true);
            QualityUpgraded?.Invoke(qualityUpgradeLevel, GetUpgradeCost(qualityUpgradeLevel - 1));
            return true;
        }

        public bool UpgradeRange()
        {
            InitializeStats();
            if (!CanUpgradeRange)
            {
                return false;
            }

            rangeUpgradeLevel++;
            ApplyStats(false);
            RangeUpgraded?.Invoke(rangeUpgradeLevel, GetUpgradeCost(rangeUpgradeLevel - 1));
            return true;
        }

        // Compatibility for older callers that used one level for all stats.
        public void SetLevel(int newLevel)
        {
            InitializeStats();
            int targetLevel = Mathf.Clamp(newLevel, 0, maximumUpgradeLevel);
            qualityUpgradeLevel = targetLevel;
            rangeUpgradeLevel = targetLevel;
            ApplyStats(true);
        }

        private int GetUpgradeCost(int currentLevel)
        {
            if (currentLevel < 0 || currentLevel >= maximumUpgradeLevel)
            {
                return 0;
            }

            if (upgradeCosts != null && currentLevel < upgradeCosts.Length)
            {
                return Mathf.Max(0, upgradeCosts[currentLevel]);
            }

            long fallback = 10L + currentLevel * 5L;
            return fallback > int.MaxValue ? int.MaxValue : (int)fallback;
        }

        private void ApplyStats(bool qualityChanged)
        {
            maximumUpgradeLevel = Mathf.Clamp(maximumUpgradeLevel, 1, 10);
            qualityUpgradeLevel = Mathf.Clamp(qualityUpgradeLevel, 0, maximumUpgradeLevel);
            rangeUpgradeLevel = Mathf.Clamp(rangeUpgradeLevel, 0, maximumUpgradeLevel);
            AttackDamage = Mathf.Max(0f, baseAttackDamage +
                attackDamagePerQualityLevel * qualityUpgradeLevel);
            AttackRange = Mathf.Max(0.1f, baseAttackRange +
                attackRangePerLevel * rangeUpgradeLevel);

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            float maxHealth = Mathf.Max(1f, baseMaxHealth +
                maxHealthPerQualityLevel * qualityUpgradeLevel);
            if (health != null)
            {
                if (qualityChanged)
                {
                    health.IncreaseMaximumHealth(maxHealth);
                }
                else if (health.MaxHealth <= 0f)
                {
                    health.ResetHealth(maxHealth);
                }
            }
            attackCooldown = 0f;
        }

        private EnemyController FindNearestEnemy()
        {
            Vector2 origin = attackOrigin == null
                ? (Vector2)transform.position
                : (Vector2)attackOrigin.position;
            float maximumDistanceSquared = AttackRange * AttackRange;
            float nearestDistanceSquared = maximumDistanceSquared;
            EnemyController nearest = null;

            var enemies = EnemyController.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                float distanceSquared = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (distanceSquared <= nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        private void OnValidate()
        {
            maximumUpgradeLevel = Mathf.Clamp(maximumUpgradeLevel, 1, 10);
            qualityUpgradeLevel = Mathf.Clamp(qualityUpgradeLevel, 0, maximumUpgradeLevel);
            rangeUpgradeLevel = Mathf.Clamp(rangeUpgradeLevel, 0, maximumUpgradeLevel);
            baseMaxHealth = Mathf.Max(1f, baseMaxHealth);
            maxHealthPerQualityLevel = Mathf.Max(0f, maxHealthPerQualityLevel);
            baseAttackDamage = Mathf.Max(0f, baseAttackDamage);
            attackDamagePerQualityLevel = Mathf.Max(0f, attackDamagePerQualityLevel);
            baseAttackRange = Mathf.Max(0.1f, baseAttackRange);
            attackRangePerLevel = Mathf.Max(0f, attackRangePerLevel);
            attackInterval = Mathf.Max(0.05f, attackInterval);
        }

        private void OnDrawGizmosSelected()
        {
            float range = Mathf.Max(0.1f, baseAttackRange +
                attackRangePerLevel * Mathf.Clamp(rangeUpgradeLevel, 0, maximumUpgradeLevel));
            Vector3 origin = attackOrigin == null ? transform.position : attackOrigin.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, range);
        }
    }
}
