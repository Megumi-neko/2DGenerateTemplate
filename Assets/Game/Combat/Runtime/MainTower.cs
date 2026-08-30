using Game.BaseSystem;
using UnityEngine;

namespace Game.Combat
{
    [AddComponentMenu("Game/Combat/Main Tower")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class MainTower : MonoBehaviour
    {
        [Header("Level")]
        [SerializeField, Range(1, 6)] private int level = 1;

        [Header("Upgrade Economy")]
        [SerializeField, Min(1)] private int maximumLevel = 6;
        [SerializeField, Min(0)] private int upgradeBaseCost = 25;
        [SerializeField, Min(0)] private int upgradeCostPerLevel = 15;
        [SerializeField] private Game.Building.CoinInventory coinInventory;


        [Header("Base Stats")]
        [SerializeField, Min(1f)] private float baseMaxHealth = 500f;
        [SerializeField, Min(0f)] private float healthPerLevel = 100f;
        [SerializeField, Min(0f)] private float baseAttackDamage = 15f;
        [SerializeField, Min(0f)] private float attackDamagePerLevel = 5f;
        [SerializeField, Min(0.1f)] private float baseAttackRange = 4f;
        [SerializeField, Min(0f)] private float attackRangePerLevel = 0.75f;
        [SerializeField, Min(0.05f)] private float attackInterval = 0.5f;
        [SerializeField] private Transform attackOrigin;

        private Health health;
        private float attackCooldown;

        public Health Health => health;
        public int Level => level;
        public float AttackDamage { get; private set; }
        public float AttackRange { get; private set; }
        public int NextUpgradeCost => CanUpgrade
            ? CalculateUpgradeCost(level)
            : 0;
        public bool CanUpgrade => level < maximumLevel;
        public event System.Action<int, int> Upgraded;


        private void Awake()
        {
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            ApplyLevel(true);
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(Health _, float amount)
        {
            if (amount > 0f) CameraShakeController.ShakeMainCamera();
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

        public bool TryUpgrade()
        {
            ResolveCoinInventory();
            int upgradeCost = NextUpgradeCost;
            if (!CanUpgrade || coinInventory == null ||
                !coinInventory.TrySpend(upgradeCost))
            {
                return false;
            }

            level++;
            ApplyLevel(true);
            Upgraded?.Invoke(level, upgradeCost);
            return true;
        }

        private int CalculateUpgradeCost(int currentLevel)
        {
            long cost = (long)Mathf.Max(0, upgradeBaseCost) +
                (long)Mathf.Max(0, currentLevel - 1) * Mathf.Max(0, upgradeCostPerLevel);
            return cost > int.MaxValue ? int.MaxValue : (int)cost;
        }

        private void ResolveCoinInventory()
        {
            if (coinInventory == null)
            {
                coinInventory = FindObjectOfType<Game.Building.CoinInventory>();
            }
        }

        internal void ConfigureEconomyForTests(Game.Building.CoinInventory inventory)
        {
            coinInventory = inventory;
        }

        
public void SetLevel(int newLevel)
        {
            int sanitizedLevel = Mathf.Clamp(newLevel, 1, maximumLevel);
            if (level == sanitizedLevel && health != null)
            {
                return;
            }

            level = sanitizedLevel;
            ApplyLevel(true);
        }

        private void ApplyLevel(bool restoreHealth)
        {
            int levelOffset = Mathf.Max(0, level - 1);
            float maxHealth = baseMaxHealth + healthPerLevel * levelOffset;
            AttackDamage = baseAttackDamage + attackDamagePerLevel * levelOffset;
            AttackRange = baseAttackRange + attackRangePerLevel * levelOffset;
            attackCooldown = 0f;

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (restoreHealth && health != null)
            {
                health.ResetHealth(maxHealth);
            }
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
            maximumLevel = Mathf.Clamp(maximumLevel, 1, 6);
            level = Mathf.Clamp(level, 1, maximumLevel);
            upgradeBaseCost = Mathf.Max(0, upgradeBaseCost);
            upgradeCostPerLevel = Mathf.Max(0, upgradeCostPerLevel);
            baseMaxHealth = Mathf.Max(1f, baseMaxHealth);
            healthPerLevel = Mathf.Max(0f, healthPerLevel);
            baseAttackDamage = Mathf.Max(0f, baseAttackDamage);
            attackDamagePerLevel = Mathf.Max(0f, attackDamagePerLevel);
            baseAttackRange = Mathf.Max(0.1f, baseAttackRange);
            attackRangePerLevel = Mathf.Max(0f, attackRangePerLevel);
            attackInterval = Mathf.Max(0.05f, attackInterval);
        }

        private void OnDrawGizmosSelected()
        {
            int levelOffset = Mathf.Max(0, level - 1);
            float range = baseAttackRange + attackRangePerLevel * levelOffset;
            Vector3 origin = attackOrigin == null ? transform.position : attackOrigin.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, range);
        }
    }
}
