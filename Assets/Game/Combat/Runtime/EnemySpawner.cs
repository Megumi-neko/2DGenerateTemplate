using System.Collections.Generic;
using Game.DayNight;
using UnityEngine;

namespace Game.Combat
{
    [AddComponentMenu("Game/Combat/Enemy Spawner")]
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DayNightSystem dayNightSystem;
        [SerializeField] private EnemyController enemyPrefab;
        [SerializeField] private EnemyStats enemyStats;
        [SerializeField] private MainTower mainTower;
        [SerializeField] private Transform enemiesRoot;
        [SerializeField] private Transform[] spawnPoints;

        [Header("Night Spawning")]
        [SerializeField, Range(1, 6)] private int initialThreatLevel = 2;
        [SerializeField, Min(0.05f)] private float spawnInterval = 2f;
        [SerializeField, Min(0f)] private float firstSpawnDelay = 1f;
        [SerializeField, Min(1)] private int maxAlive = 20;
        [SerializeField, Min(1)] private int maxSpawnedPerNight = 60;
        [SerializeField, Range(0f, 1f)] private float maximumThreatChance = 0.55f;
        [SerializeField, Min(0f)] private float fallbackSpawnRadius = 9f;
        [SerializeField, Min(0f)] private float bossSpawnRadius = 3f;
        [SerializeField, Min(0)] private int prewarmCount = 8;
        [SerializeField, Min(1)] private int poolCapacity = 40;

        [Header("Boss Multipliers")]
        [SerializeField, Min(1f)] private float bossHealthMultiplier = 4f;
        [SerializeField, Min(1f)] private float bossAttackMultiplier = 1.75f;

        private readonly HashSet<EnemyController> activeEnemies =
            new HashSet<EnemyController>();
        private readonly List<EnemyController> releaseBuffer =
            new List<EnemyController>();

        private GameObjectPool enemyPool;
        private float spawnTimer;
        private int spawnedThisNight;
        private bool bossSpawnedThisNight;
        private bool nightActive;

        public int AliveCount => activeEnemies.Count;
        public int SpawnedThisNight => spawnedThisNight;
        public bool BossSpawnedThisNight => bossSpawnedThisNight;
        public int CurrentMaximumThreatLevel => EnemyStats.GetMaximumThreatForDay(
            dayNightSystem == null ? 1 : dayNightSystem.CurrentDay,
            initialThreatLevel);

        private void Awake()
        {
            ResolveReferences();
            InitializePool();
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeToTowerDeath();

            if (dayNightSystem != null && dayNightSystem.CurrentPhase == DayNightPhase.Night)
            {
                BeginNight();
            }
        }

        private void OnDisable()
        {
            EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
            UnsubscribeFromTowerDeath();
            EndNight();
        }

        private void OnDestroy()
        {
            ReturnAllEnemies();
            enemyPool?.Dispose();
            enemyPool = null;
        }

        private void Update()
        {
            if (!nightActive || dayNightSystem == null || mainTower == null ||
                mainTower.Health == null || mainTower.Health.IsDead)
            {
                return;
            }

            if (ShouldSpawnBoss(
                nightActive,
                bossSpawnedThisNight,
                dayNightSystem.NightRemainingRatio))
            {
                bossSpawnedThisNight = SpawnEnemy(CurrentMaximumThreatLevel, true);
            }

            if (spawnedThisNight >= maxSpawnedPerNight || activeEnemies.Count >= maxAlive)
            {
                return;
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f)
            {
                return;
            }

            SpawnEnemy(ChooseThreatLevel(CurrentMaximumThreatLevel), false);
            spawnTimer = spawnInterval;
        }

        public static bool ShouldSpawnBoss(
            bool isNightActive,
            bool alreadySpawned,
            float nightRemainingRatio)
        {
            return isNightActive && !alreadySpawned && nightRemainingRatio <= 0.5f;
        }

        public void BeginNight()
        {
            ResolveReferences();
            if (enemyPool == null)
            {
                InitializePool();
            }

            ReturnAllEnemies();
            nightActive = enemyPool != null && mainTower != null && mainTower.Health != null;
            spawnedThisNight = 0;
            bossSpawnedThisNight = false;
            spawnTimer = firstSpawnDelay;
        }

        public void EndNight()
        {
            nightActive = false;
            ReturnAllEnemies();
        }

        private void InitializePool()
        {
            if (enemyPool != null || enemyPrefab == null)
            {
                return;
            }

            int capacity = Mathf.Max(poolCapacity, maxAlive + 1);
            enemyPool = new GameObjectPool(
                CreateEnemy,
                enemiesRoot == null ? transform : enemiesRoot,
                obj => obj.SetActive(true),
                obj => obj.SetActive(false),
                capacity);
            enemyPool.Prewarm(Mathf.Min(prewarmCount, capacity));
        }

        private GameObject CreateEnemy()
        {
            GameObject instance = Instantiate(enemyPrefab.gameObject);
            instance.SetActive(false);
            return instance;
        }

        private bool SpawnEnemy(int threatLevel, bool boss)
        {
            if (enemyPool == null || mainTower == null || mainTower.Health == null)
            {
                return false;
            }

            GameObject instance = enemyPool.Get();
            EnemyController enemy = instance.GetComponent<EnemyController>();
            if (enemy == null)
            {
                Debug.LogError("Enemy prefab must contain EnemyController.", instance);
                enemyPool.Return(instance);
                return false;
            }

            int sanitizedThreat = Mathf.Clamp(
                threatLevel,
                EnemyStats.MinimumThreatLevel,
                EnemyStats.MaximumThreatLevel);
            EnemyLevelStats stats = enemyStats == null
                ? EnemyStats.GetDefault(sanitizedThreat)
                : enemyStats.Get(sanitizedThreat);

            instance.transform.position = GetSpawnPosition(boss);
            instance.transform.rotation = Quaternion.identity;
            instance.name = boss
                ? $"Enemy Boss L{sanitizedThreat}"
                : $"Enemy L{sanitizedThreat}";
            enemy.Initialize(
                mainTower.Health,
                stats,
                sanitizedThreat,
                boss,
                boss ? bossHealthMultiplier : 1f,
                boss ? bossAttackMultiplier : 1f,
                OnEnemyReleaseRequested);

            activeEnemies.Add(enemy);
            if (!boss)
            {
                spawnedThisNight++;
            }

            return true;
        }

        private int ChooseThreatLevel(int maximumThreat)
        {
            if (maximumThreat <= EnemyStats.MinimumThreatLevel ||
                Random.value <= maximumThreatChance)
            {
                return maximumThreat;
            }

            return Random.Range(EnemyStats.MinimumThreatLevel, maximumThreat + 1);
        }

        private Vector3 GetSpawnPosition(bool boss)
        {
            if (!boss && spawnPoints != null && spawnPoints.Length > 0)
            {
                int startIndex = Random.Range(0, spawnPoints.Length);
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    Transform point = spawnPoints[(startIndex + i) % spawnPoints.Length];
                    if (point != null)
                    {
                        return point.position;
                    }
                }
            }

            Vector2 center = mainTower == null
                ? (Vector2)transform.position
                : (Vector2)mainTower.transform.position;
            float radius = boss ? bossSpawnRadius : fallbackSpawnRadius;
            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }

            return center + direction.normalized * radius;
        }

        private void OnEnemyReleaseRequested(EnemyController enemy)
        {
            if (enemy == null || !activeEnemies.Remove(enemy))
            {
                return;
            }

            enemyPool?.Return(enemy.gameObject);
        }

        private void ReturnAllEnemies()
        {
            if (activeEnemies.Count == 0)
            {
                return;
            }

            releaseBuffer.Clear();
            releaseBuffer.AddRange(activeEnemies);
            for (int i = 0; i < releaseBuffer.Count; i++)
            {
                EnemyController enemy = releaseBuffer[i];
                if (enemy != null)
                {
                    enemy.RequestRelease();
                }
            }

            activeEnemies.Clear();
            releaseBuffer.Clear();
        }

        private void OnDayNightStateChanged(DayNightStateChanged state)
        {
            if (state.Phase == DayNightPhase.Night)
            {
                BeginNight();
            }
            else
            {
                EndNight();
            }
        }

        private void OnTowerDied(Health _)
        {
            EndNight();
        }

        private void ResolveReferences()
        {
            if (dayNightSystem == null)
            {
                dayNightSystem = FindObjectOfType<DayNightSystem>();
            }

            if (mainTower == null)
            {
                mainTower = FindObjectOfType<MainTower>();
            }
        }

        private void SubscribeToTowerDeath()
        {
            if (mainTower != null && mainTower.Health != null)
            {
                mainTower.Health.Died -= OnTowerDied;
                mainTower.Health.Died += OnTowerDied;
            }
        }

        private void UnsubscribeFromTowerDeath()
        {
            if (mainTower != null && mainTower.Health != null)
            {
                mainTower.Health.Died -= OnTowerDied;
            }
        }

        private void OnValidate()
        {
            initialThreatLevel = Mathf.Clamp(initialThreatLevel, 1, 6);
            spawnInterval = Mathf.Max(0.05f, spawnInterval);
            firstSpawnDelay = Mathf.Max(0f, firstSpawnDelay);
            maxAlive = Mathf.Max(1, maxAlive);
            maxSpawnedPerNight = Mathf.Max(1, maxSpawnedPerNight);
            fallbackSpawnRadius = Mathf.Max(0f, fallbackSpawnRadius);
            bossSpawnRadius = Mathf.Max(0f, bossSpawnRadius);
            prewarmCount = Mathf.Max(0, prewarmCount);
            poolCapacity = Mathf.Max(1, poolCapacity);
            bossHealthMultiplier = Mathf.Max(1f, bossHealthMultiplier);
            bossAttackMultiplier = Mathf.Max(1f, bossAttackMultiplier);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = mainTower == null ? transform.position : mainTower.transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, fallbackSpawnRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, bossSpawnRadius);
        }
    }
}
