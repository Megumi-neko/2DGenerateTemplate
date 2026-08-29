using System;
using System.Collections.Generic;
using Game.Lighting;
using Game.Building;
using UnityEngine;

namespace Game.Combat
{
    [AddComponentMenu("Game/Combat/Enemy Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
        private static readonly List<EnemyController> ActiveEnemiesInternal =
            new List<EnemyController>();

        [Header("Combat")]
        [SerializeField, Min(0.01f)] private float attackRange = 0.75f;
        [SerializeField, Min(0.05f)] private float attackInterval = 1f;
        [SerializeField, Min(0.02f)] private float illuminationSampleInterval = 0.1f;
        [SerializeField, Range(0.01f, 1f)] private float minimumSectorSpeedMultiplier = 0.5f;

        [Header("Visual")]
        [SerializeField] private Transform visualRoot;

        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color bossColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField, Min(1f)] private float baseScaleMultiplier = 1.25f;
        [SerializeField, Min(1f)] private float bossScaleMultiplier = 1.75f;
        [SerializeField, Min(0f)] private float threatScaleStep = 0.18f;
        [SerializeField, Min(0.01f)] private float threatScaleExponent = 1.6f;


        private Health health;
        private Health target;
        private Health towerTarget;
        private BuildingHealth buildingTarget;
        private Transform targetTransform;
        private Action<EnemyController> releaseRequested;
        private float moveSpeed;
        private float attackDamage;
        private float attackCooldown;
        private float illuminationAccumulator;        private Vector3 baseScale;
        private Vector3 visualBaseScale;
        private Color baseColor = Color.white;
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private Camera targetCamera;

        private bool isSpawned;
        [SerializeField] private CoinInventory coinInventory;
        private int coinReward;
        private bool rewardGranted;

        public static IReadOnlyList<EnemyController> ActiveEnemies => ActiveEnemiesInternal;
        public Health Health => health;
        public int ThreatLevel { get; private set; }
        public bool IsBoss { get; private set; }
        public bool IsAlive => isSpawned && health != null && !health.IsDead;
        public Vector2 WorldPosition => transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            ActiveEnemiesInternal.Clear();
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Died += OnDied;
            baseScale = transform.localScale;

            if (visual == null)
            {
                visual = GetComponentInChildren<SpriteRenderer>();
            }

            if (visualRoot == null && visual != null)
            {
                visualRoot = visual.transform;
            }

            if (visualRoot != null)
            {
                visualBaseScale = visualRoot.localScale;
            }

            if (visual != null)
            {
                baseColor = visual.color;
            }
        }

        private void OnDisable()
        {
            ActiveEnemiesInternal.Remove(this);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }

            ActiveEnemiesInternal.Remove(this);
        }
        private void LateUpdate()
        {
            if (!faceCamera)
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return;
            }

            Vector3 directionToCamera = targetCamera.transform.position - transform.position;
            if (directionToCamera.sqrMagnitude > 0.0001f)
            {                Transform billboard = visualRoot == null ? transform : visualRoot;
                billboard.rotation = Quaternion.LookRotation(
                    directionToCamera.normalized,
                    targetCamera.transform.up);
            }
        }



        private void Update()
        {
            FindNearestTarget();
            if (!IsAlive || targetTransform == null ||
                (target == null && (buildingTarget == null || buildingTarget.IsDead)))
            {
                return;
            }

            ApplyIlluminationDamage(Time.deltaTime);
            if (!IsAlive)
            {
                return;
            }

            Vector2 currentPosition = transform.position;
            Vector2 targetPosition = targetTransform.position;
            float distance = Vector2.Distance(currentPosition, targetPosition);

            if (distance > attackRange)
            {
                transform.position = Vector2.MoveTowards(
                    currentPosition,
                    targetPosition,
                    moveSpeed * GetMovementSpeedMultiplier() * Time.deltaTime);
                attackCooldown = Mathf.Max(0f, attackCooldown - Time.deltaTime);
                return;
            }

            attackCooldown -= Time.deltaTime;
            if (attackCooldown <= 0f)
            {
                if (buildingTarget != null && !buildingTarget.IsDead)
                {
                    buildingTarget.TakeDamage(attackDamage);
                }
                else if (target != null && !target.IsDead)
                {
                    target.TakeDamage(attackDamage);
                }
                attackCooldown = attackInterval;
            }
        }

        public void Initialize(
            Health targetHealth,
            EnemyLevelStats stats,
            int threatLevel,
            bool boss,
            float healthMultiplier,
            float attackMultiplier,
            Action<EnemyController> onReleaseRequested,
            int reward = 0)
        {
            target = targetHealth;
            towerTarget = targetHealth;
            buildingTarget = null;
            targetTransform = targetHealth == null ? null : targetHealth.transform;
            ThreatLevel = Mathf.Clamp(
                threatLevel,
                EnemyStats.MinimumThreatLevel,
                EnemyStats.MaximumThreatLevel);
            IsBoss = boss;
            moveSpeed = Mathf.Max(0f, stats.MoveSpeed);
            attackDamage = Mathf.Max(0f, stats.AttackDamage * Mathf.Max(0f, attackMultiplier));
            attackCooldown = attackInterval;
            illuminationAccumulator = 0f;
            releaseRequested = onReleaseRequested;
            coinReward = Mathf.Max(0, reward);
            rewardGranted = false;
            isSpawned = true;

            health.ResetHealth(stats.MaxHealth * Mathf.Max(0.01f, healthMultiplier));
            float scaleMultiplier = GetScaleMultiplier(
                ThreatLevel,
                boss,
                threatScaleStep,
                bossScaleMultiplier,
                baseScaleMultiplier,
                threatScaleExponent);
            transform.localScale = baseScale;
            if (visualRoot != null)
            {
                visualRoot.localScale = visualBaseScale * scaleMultiplier;
            }
            else
            {
                transform.localScale = baseScale * scaleMultiplier;
            }

            if (visual != null)
            {
                visual.color = boss ? bossColor : baseColor;
            }

            if (!ActiveEnemiesInternal.Contains(this))
            {
                ActiveEnemiesInternal.Add(this);
            }
        }

        public static float GetScaleMultiplier(
            int threatLevel,
            bool boss,
            float threatScaleStep = 0.18f,
            float bossScaleMultiplier = 1.75f,
            float baseScaleMultiplier = 1.25f,
            float threatScaleExponent = 1.6f)
        {
            int sanitizedThreat = Mathf.Clamp(
                threatLevel,
                EnemyStats.MinimumThreatLevel,
                EnemyStats.MaximumThreatLevel);
            float levelOffset = sanitizedThreat - EnemyStats.MinimumThreatLevel;
            float threatMultiplier = Mathf.Max(1f, baseScaleMultiplier) *
                (1f + Mathf.Max(0f, threatScaleStep) *
                    Mathf.Pow(levelOffset, Mathf.Max(0.01f, threatScaleExponent)));
            return threatMultiplier * (boss ? Mathf.Max(1f, bossScaleMultiplier) : 1f);
        }


        public bool TakeDamage(float amount)
        {
            return IsAlive && health.TakeDamage(amount);
        }

        public void RequestRelease()
        {
            if (!isSpawned)
            {
                return;
            }

            isSpawned = false;
            ActiveEnemiesInternal.Remove(this);
            Action<EnemyController> callback = releaseRequested;
            releaseRequested = null;
            target = null;
            buildingTarget = null;
            targetTransform = null;
            callback?.Invoke(this);
        }

        private bool FindNearestTarget()
        {
            float nearestDistance = float.PositiveInfinity;
            BuildInstance nearestBuilding = null;
            BuildSystem buildSystem = FindObjectOfType<BuildSystem>();
            if (buildSystem != null)
            {
                for (int i = 0; i < buildSystem.Builds.Count; i++)
                {
                    BuildInstance candidate = buildSystem.Builds[i];
                    if (candidate == null || candidate.BuildingHealth == null || candidate.BuildingHealth.IsDead) continue;
                    float distance = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                    if (distance < nearestDistance) { nearestDistance = distance; nearestBuilding = candidate; }
                }
            }

            if (nearestBuilding != null)
            {
                buildingTarget = nearestBuilding.BuildingHealth;
                targetTransform = nearestBuilding.transform;
                return true;
            }

            if (towerTarget != null && !towerTarget.IsDead)
            {
                target = towerTarget;
                buildingTarget = null;
                targetTransform = towerTarget.transform;
                return true;
            }

            target = null;
            targetTransform = null;
            return false;
        }

        private float GetMovementSpeedMultiplier()
        {
            if (!IlluminationSystem.IsAffectedByMinimumSector(WorldPosition))
            {
                return 1f;
            }

            return Mathf.Clamp(minimumSectorSpeedMultiplier, 0.01f, 1f);
        }

        private void ApplyIlluminationDamage(float deltaTime)
        {
            illuminationAccumulator += deltaTime;
            if (illuminationAccumulator < illuminationSampleInterval)
            {
                return;
            }

            float sampleDuration = illuminationAccumulator;
            illuminationAccumulator = 0f;
            float damagePerSecond = IlluminationSystem.GetDamagePerSecond(WorldPosition);
            if (damagePerSecond > 0f)
            {
                health.TakeDamage(damagePerSecond * sampleDuration);
            }
        }

        private void OnDied(Health _)
        {
            if (!rewardGranted)
            {
                rewardGranted = true;
                if (coinInventory == null)
                {
                    coinInventory = FindObjectOfType<CoinInventory>();
                }

                coinInventory?.Add(coinReward);
            }

            RequestRelease();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
        private void Reset()
        {
            targetCamera = Camera.main;
        }



        private void OnValidate()
        {
            attackRange = Mathf.Max(0.01f, attackRange);
            attackInterval = Mathf.Max(0.05f, attackInterval);
            illuminationSampleInterval = Mathf.Max(0.02f, illuminationSampleInterval);
            minimumSectorSpeedMultiplier = Mathf.Clamp(minimumSectorSpeedMultiplier, 0.01f, 1f);
            bossScaleMultiplier = Mathf.Max(1f, bossScaleMultiplier);
            baseScaleMultiplier = Mathf.Max(1f, baseScaleMultiplier);
            threatScaleStep = Mathf.Max(0f, threatScaleStep);
            threatScaleExponent = Mathf.Max(0.01f, threatScaleExponent);

        }
    }
}
