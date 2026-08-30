using System.Collections.Generic;
using Game.DayNight;
using Game.Lighting;
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
        [SerializeField] private GameObject bossWarningFrame;

        [Header("Night Spawning")]
        [SerializeField, Range(1, 6)] private int initialThreatLevel = 2;
        [SerializeField, Min(0.05f)] private float spawnInterval = 2f;
        [SerializeField, Min(0f)] private float firstSpawnDelay = 1f;
        [SerializeField, Min(1)] private int maxAlive = 20;
        [SerializeField, Min(1)] private int maxSpawnedPerNight = 60;
        [SerializeField, Min(1f)] private float spawnCountThreatMultiplier = 1.8f;
        [SerializeField, Min(0.01f)] private float spawnCountThreatExponent = 1.35f;
        [SerializeField, Min(1f)] private float aliveThreatMultiplier = 1.5f;
        [SerializeField, Range(0f, 1f)] private float maximumThreatChance = 0.55f;
        [SerializeField, Min(0f)] private float fallbackSpawnRadius = 9f;
        [SerializeField, Min(0f)] private float bossSpawnRadius = 3f;
        [SerializeField, Min(0f)] private float minimumSpawnDistance = 6f;
        [SerializeField, Min(0f)] private float maximumSpawnDistance = 12f;
        [SerializeField, Min(1)] private int darkSpawnAttempts = 24;
        [SerializeField, Min(0f)] private float darknessPadding = 0.35f;
        [SerializeField, Min(0f)] private float spawnVerticalJitter = 1f;
        [SerializeField, Min(0)] private int prewarmCount = 8;
        [SerializeField, Min(1)] private int poolCapacity = 40;

        [Header("Boss Multipliers")]
        [SerializeField, Min(1f)] private float bossHealthMultiplier = 4f;
        [SerializeField, Min(1f)] private float bossAttackMultiplier = 1.75f;
        [SerializeField, Min(1f)] private float bossCoinRewardMultiplier = 2f;

        private readonly HashSet<EnemyController> activeEnemies =
            new HashSet<EnemyController>();
        private readonly List<EnemyController> releaseBuffer =
            new List<EnemyController>();

        private GameObjectPool enemyPool;
        private float spawnTimer;
        private int spawnedThisNight;
        private int nightSpawnLimit;
        private int nightMaxAlive;
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
            HideBossWarning();
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
                if (bossSpawnedThisNight)
                {
                    ShowBossWarning();
                }
            }

            if (spawnedThisNight >= nightSpawnLimit || activeEnemies.Count >= nightMaxAlive)
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

        public static int GetSpawnLimitForThreat(
            int threatLevel,
            int baseLimit,
            float threatMultiplier = 1.8f,
            float threatExponent = 1.35f)
        {
            float multiplier = GetThreatMultiplier(threatLevel, threatMultiplier, threatExponent);
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, baseLimit) * multiplier));
        }

        public static int GetMaxAliveForThreat(
            int threatLevel,
            int baseMaxAlive,
            float threatMultiplier = 1.5f)
        {
            float multiplier = GetThreatMultiplier(threatLevel, threatMultiplier, 1f);
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, baseMaxAlive) * multiplier));
        }

        private static float GetThreatMultiplier(int threatLevel, float maximumMultiplier, float exponent)
        {
            int sanitizedThreat = Mathf.Clamp(
                threatLevel,
                EnemyStats.MinimumThreatLevel,
                EnemyStats.MaximumThreatLevel);
            float normalizedThreat = (sanitizedThreat - EnemyStats.MinimumThreatLevel) /
                (float)(EnemyStats.MaximumThreatLevel - EnemyStats.MinimumThreatLevel);
            return 1f + (Mathf.Max(1f, maximumMultiplier) - 1f) *
                Mathf.Pow(normalizedThreat, Mathf.Max(0.01f, exponent));
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
            HideBossWarning();
            nightActive = enemyPool != null && mainTower != null && mainTower.Health != null;
            spawnedThisNight = 0;
            nightSpawnLimit = GetSpawnLimitForThreat(
                CurrentMaximumThreatLevel,
                maxSpawnedPerNight,
                spawnCountThreatMultiplier,
                spawnCountThreatExponent);
            nightMaxAlive = GetMaxAliveForThreat(
                CurrentMaximumThreatLevel,
                maxAlive,
                aliveThreatMultiplier);
            bossSpawnedThisNight = false;
            spawnTimer = firstSpawnDelay;
        }

        public void EndNight()
        {
            nightActive = false;
            HideBossWarning();
            ReturnAllEnemies();
        }

        private void InitializePool()
        {
            if (enemyPool != null || enemyPrefab == null)
            {
                return;
            }

            int capacity = Mathf.Max(
                poolCapacity,
                GetMaxAliveForThreat(
                    EnemyStats.MaximumThreatLevel,
                    maxAlive,
                    aliveThreatMultiplier) + 1);
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

            if (!TryGetSpawnPosition(boss, out Vector3 spawnPosition))
            {
                enemyPool.Return(instance);
                return false;
            }

            instance.transform.position = spawnPosition;
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
                OnEnemyReleaseRequested,
                GetCoinReward(sanitizedThreat, boss));

            activeEnemies.Add(enemy);
            if (!boss)
            {
                spawnedThisNight++;
            }

            return true;
        }

        private int GetCoinReward(int threatLevel, bool boss)
        {
            int reward = enemyStats == null
                ? EnemyStats.GetDefault(threatLevel).CoinReward
                : enemyStats.GetCoinReward(threatLevel);
            if (!boss)
            {
                return reward;
            }

            float multiplied = reward * Mathf.Max(1f, bossCoinRewardMultiplier);
            return multiplied >= int.MaxValue ? int.MaxValue : Mathf.Max(0, Mathf.RoundToInt(multiplied));
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

        private bool TryGetSpawnPosition(bool boss, out Vector3 position)
        {
            Vector2 center = mainTower == null
                ? (Vector2)transform.position
                : (Vector2)mainTower.transform.position;
            float configuredMinimum = boss
                ? Mathf.Max(0f, minimumSpawnDistance * 0.5f)
                : minimumSpawnDistance;
            float configuredMaximum = boss
                ? Mathf.Max(configuredMinimum, bossSpawnRadius)
                : Mathf.Max(configuredMinimum, maximumSpawnDistance);
            Vector2 distanceRange = GetSpawnDistanceRange(
                configuredMinimum,
                configuredMaximum,
                GetOuterLightRadius(center),
                darknessPadding);

            for (int attempt = 0; attempt < darkSpawnAttempts; attempt++)
            {
                float radius = Random.Range(distanceRange.x, distanceRange.y);
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 candidate = center + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)) * radius;
                candidate.y += Random.Range(-spawnVerticalJitter, spawnVerticalJitter);

                if (IsDarkSpawnPosition(candidate))
                {
                    position = new Vector3(candidate.x, candidate.y, transform.position.z);
                    return true;
                }
            }

            position = default;
            return false;
        }

        public static Vector2 GetSpawnDistanceRange(
            float configuredMinimum,
            float configuredMaximum,
            float outerLightRadius,
            float padding)
        {
            float sanitizedMinimum = Mathf.Max(0f, configuredMinimum);
            float sanitizedMaximum = Mathf.Max(sanitizedMinimum, configuredMaximum);
            float searchBandWidth = Mathf.Max(0.5f, sanitizedMaximum - sanitizedMinimum);
            float minimumDistance = Mathf.Max(
                sanitizedMinimum,
                Mathf.Max(0f, outerLightRadius) + Mathf.Max(0f, padding));
            return new Vector2(minimumDistance, minimumDistance + searchBandWidth);
        }

        private static float GetOuterLightRadius(Vector2 center)
        {
            float outerRadius = 0f;
            IReadOnlyList<LightEmitter2D> emitters = IlluminationSystem.RegisteredEmitters;
            for (int i = 0; i < emitters.Count; i++)
            {
                LightEmitter2D emitter = emitters[i];
                if (emitter == null || !emitter.IsOperational)
                {
                    continue;
                }

                float radiusFromCenter = Vector2.Distance(center, emitter.WorldPosition) +
                    emitter.MaximumEffectiveRange;
                outerRadius = Mathf.Max(outerRadius, radiusFromCenter);
            }

            return outerRadius;
        }

        private bool IsDarkSpawnPosition(Vector2 candidate)
        {
            if (IlluminationSystem.IsLit(candidate))
            {
                return false;
            }

            if (darknessPadding <= 0f)
            {
                return true;
            }

            // Keep a small fully-dark footprint around the enemy, not just its center.
            return !IlluminationSystem.IsLit(candidate + Vector2.right * darknessPadding) &&
                   !IlluminationSystem.IsLit(candidate - Vector2.right * darknessPadding) &&
                   !IlluminationSystem.IsLit(candidate + Vector2.up * darknessPadding) &&
                   !IlluminationSystem.IsLit(candidate - Vector2.up * darknessPadding);
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

        private void ShowBossWarning()
        {
            ResolveWarningFrame();
            if (bossWarningFrame == null)
            {
                return;
            }

            bossWarningFrame.SetActive(true);
            CancelInvoke(nameof(HideBossWarning));
            Invoke(nameof(HideBossWarning), 5f);
        }

        private void HideBossWarning()
        {
            CancelInvoke(nameof(HideBossWarning));
            ResolveWarningFrame();
            if (bossWarningFrame != null)
            {
                bossWarningFrame.SetActive(false);
            }
        }

        private void ResolveWarningFrame()
        {
            if (bossWarningFrame == null)
            {
                bossWarningFrame = GameObject.Find("Canvas/WarnTextFrame");
            }
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

            ResolveWarningFrame();
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
            spawnCountThreatMultiplier = Mathf.Max(1f, spawnCountThreatMultiplier);
            spawnCountThreatExponent = Mathf.Max(0.01f, spawnCountThreatExponent);
            aliveThreatMultiplier = Mathf.Max(1f, aliveThreatMultiplier);
            fallbackSpawnRadius = Mathf.Max(0f, fallbackSpawnRadius);
            bossSpawnRadius = Mathf.Max(0f, bossSpawnRadius);
            minimumSpawnDistance = Mathf.Max(0f, minimumSpawnDistance);
            maximumSpawnDistance = Mathf.Max(minimumSpawnDistance, maximumSpawnDistance);
            darkSpawnAttempts = Mathf.Max(1, darkSpawnAttempts);
            darknessPadding = Mathf.Max(0f, darknessPadding);
            spawnVerticalJitter = Mathf.Max(0f, spawnVerticalJitter);
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
