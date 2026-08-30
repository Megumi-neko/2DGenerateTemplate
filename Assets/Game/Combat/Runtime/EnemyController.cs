using System.Collections;
using System;
using System.Collections.Generic;
using Game.Lighting;
using Game.Building;
using UnityEngine;
using UnityEngine.Serialization;

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
        [Tooltip("光照伤害的触发间隔（秒）。敌人每隔该时间结算一次光照伤害，而不是每帧造成伤害。")]
        [FormerlySerializedAs("illuminationSampleInterval")]
        [SerializeField, Min(0.02f)] private float illuminationDamageInterval = 0.1f;
        [SerializeField, Range(0.01f, 1f)] private float minimumSectorSpeedMultiplier = 0.5f;

        [Header("Visual")]
        
        [SerializeField] private Animator animator;
        [SerializeField] private string walkAnimationState = "GhostWalk";
        [SerializeField] private string deathAnimationState = "GhostBoom";
        [SerializeField, Min(0f)] private float deathAnimationDuration = 1.35f;
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
        private Coroutine illuminationDamageRoutine;
        private Vector3 baseScale;
        private Vector3 visualBaseScale;
        private Color baseColor = Color.white;
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private Camera targetCamera;

        private bool isSpawned;
        [SerializeField] private CoinInventory coinInventory;
        private int coinReward;
        
        private bool isDying;
        private Coroutine deathRoutine;
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
            animator = animator == null ? GetComponent<Animator>() : animator;
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
            StopIlluminationDamageRoutine();
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

            if (!IsAlive)
            {
                return;
            }

            Vector2 currentPosition = transform.position;
            Vector2 targetPosition = targetTransform.position;
            UpdateFacing(targetPosition.x - currentPosition.x);
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
            isDying = false;
            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
                deathRoutine = null;
            }

            PlayAnimationState(walkAnimationState);
target = targetHealth;
            towerTarget = targetHealth;
            buildingTarget = null;
            targetTransform = targetHealth == null ? null : targetHealth.transform;
            if (targetTransform != null)
            {
                UpdateFacing(targetTransform.position.x - transform.position.x);
            }
            ThreatLevel = Mathf.Clamp(
                threatLevel,
                EnemyStats.MinimumThreatLevel,
                EnemyStats.MaximumThreatLevel);
            IsBoss = boss;
            moveSpeed = Mathf.Max(0f, stats.MoveSpeed);
            attackDamage = Mathf.Max(0f, stats.AttackDamage * Mathf.Max(0f, attackMultiplier));
            attackCooldown = attackInterval;
            StopIlluminationDamageRoutine();
            releaseRequested = onReleaseRequested;
            coinReward = Mathf.Max(0, reward);
            rewardGranted = false;
            isSpawned = true;

            health.ResetHealth(stats.MaxHealth * Mathf.Max(0.01f, healthMultiplier));
            illuminationDamageRoutine = StartCoroutine(IlluminationDamageRoutine());
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

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
                deathRoutine = null;
            }

            isDying = false;
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

        private void UpdateFacing(float horizontalDirection)
        {
            if (visual == null || Mathf.Abs(horizontalDirection) < 0.001f)
            {
                return;
            }

            // GhostWalk faces left by default, so flip when moving toward the right.
            visual.flipX = horizontalDirection > 0f;
        }

        private float GetMovementSpeedMultiplier()
        {
            if (!IlluminationSystem.IsAffectedByMinimumSector(WorldPosition))
            {
                return 1f;
            }

            return Mathf.Clamp(minimumSectorSpeedMultiplier, 0.01f, 1f);
        }

        private IEnumerator IlluminationDamageRoutine()
        {
            while (IsAlive)
            {
                yield return new WaitForSeconds(illuminationDamageInterval);
                if (!IsAlive)
                {
                    yield break;
                }

                float damagePerSecond = IlluminationSystem.GetDamagePerSecond(WorldPosition);
                if (damagePerSecond > 0f)
                {
                    health.TakeDamage(damagePerSecond * illuminationDamageInterval);
                }
            }

            illuminationDamageRoutine = null;
        }

        private void StopIlluminationDamageRoutine()
        {
            if (illuminationDamageRoutine == null)
            {
                return;
            }

            StopCoroutine(illuminationDamageRoutine);
            illuminationDamageRoutine = null;
        }

private void OnDied(Health _)
        {
            StopIlluminationDamageRoutine();
            if (!rewardGranted)
            {
                rewardGranted = true;
                if (coinInventory == null)
                {
                    coinInventory = FindObjectOfType<CoinInventory>();
                }

                coinInventory?.Add(coinReward);
            }

            if (isDying || !isSpawned)
            {
                return;
            }

            isDying = true;
            PlayAnimationState(deathAnimationState);
            deathRoutine = StartCoroutine(ReleaseAfterDeathAnimation());
        }

        private IEnumerator ReleaseAfterDeathAnimation()
        {
            yield return new WaitForSeconds(GetDeathAnimationDuration());
            deathRoutine = null;
            RequestRelease();
        }

        private float GetDeathAnimationDuration()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null && clips[i].name == deathAnimationState)
                    {
                        return Mathf.Max(0.01f, clips[i].length);
                    }
                }
            }

            return Mathf.Max(0.01f, deathAnimationDuration);
        }

        private void PlayAnimationState(string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            animator.Play(stateName, 0, 0f);
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
            illuminationDamageInterval = Mathf.Max(0.02f, illuminationDamageInterval);
            minimumSectorSpeedMultiplier = Mathf.Clamp(minimumSectorSpeedMultiplier, 0.01f, 1f);
            bossScaleMultiplier = Mathf.Max(1f, bossScaleMultiplier);
            baseScaleMultiplier = Mathf.Max(1f, baseScaleMultiplier);
            threatScaleStep = Mathf.Max(0f, threatScaleStep);
            threatScaleExponent = Mathf.Max(0.01f, threatScaleExponent);
            deathAnimationDuration = Mathf.Max(0f, deathAnimationDuration);
        }
    }
}
