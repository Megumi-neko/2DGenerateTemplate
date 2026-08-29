using System.Collections.Generic;
using Game.Building;
using Game.Combat;
using Game.DayNight;
using Game.Lighting;
using UnityEngine;

namespace Game.Stage4
{
    [AddComponentMenu("Game/Stage 4/Debris Manager")]
    [DisallowMultipleComponent]
    public sealed class Stage4DebrisManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DayNightSystem dayNightSystem;
        [SerializeField] private CoinInventory coinInventory;
        [SerializeField] private MainTower mainTower;
        [SerializeField] private GameObject debrisPrefab;
        [Tooltip("Assign EBF03C0A0B7FAD2E9F5341A8CCEF0BEA.png here.")]
        [SerializeField] private Sprite debrisSprite;
        [SerializeField] private Transform debrisRoot;

        [Header("Spawn")]
        [SerializeField] private Vector2 spawnAreaCenter;
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(24f, 14f);
        [SerializeField, Min(0)] private int debrisPerNight = 5;
        [SerializeField, Min(1)] private int maximumActiveDebris = 12;
        [SerializeField, Min(1)] private int spawnAttemptsPerDebris = 30;
        [SerializeField, Min(0.1f)] private float minimumDistanceFromTower = 2f;
        [SerializeField, Min(0f)] private float minimumDistanceBetweenDebris = 1.25f;

        [Header("Repair")]
        [SerializeField, Min(0.01f)] private float repairRequired = 10f;
        [SerializeField, Min(0f)] private float repairRatePerIntensity = 1f;
        [SerializeField, Min(0)] private int repairReward = 25;
        [SerializeField] private Vector3 progressBarOffset = new Vector3(0f, 0.7f, 0f);
        [SerializeField] private Vector2 progressBarSize = new Vector2(1.2f, 0.12f);

        private readonly List<Stage4Debris> spawned = new List<Stage4Debris>();
        private bool nightActive;

        public IReadOnlyList<Stage4Debris> SpawnedDebris => spawned;

        private void Awake()
        {
            if (dayNightSystem == null) dayNightSystem = FindObjectOfType<DayNightSystem>();
            if (coinInventory == null) coinInventory = FindObjectOfType<CoinInventory>();
            if (mainTower == null) mainTower = FindObjectOfType<MainTower>();
            if (debrisRoot == null) debrisRoot = transform;
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
        }

        private void Start()
        {
            if (dayNightSystem != null && dayNightSystem.CurrentPhase == DayNightPhase.Night) BeginNight();
        }

        private void OnDisable()
        {
            EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
            ClearSpawned();
            nightActive = false;
        }

        private void Update()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
                if (spawned[i] == null) spawned.RemoveAt(i);
        }

        private void OnDayNightStateChanged(DayNightStateChanged state)
        {
            if (state.Phase == DayNightPhase.Night) BeginNight();
            else { nightActive = false; ClearSpawned(); }
        }

        public void BeginNight()
        {
            nightActive = true;
            ClearSpawned();
            int count = Mathf.Min(debrisPerNight, maximumActiveDebris);
            for (int i = 0; i < count; i++)
            {
                if (TryGetDarkPosition(out Vector3 position)) Spawn(position);
            }
        }

        private bool TryGetDarkPosition(out Vector3 position)
        {
            Vector2 half = spawnAreaSize * 0.5f;
            for (int attempt = 0; attempt < spawnAttemptsPerDebris; attempt++)
            {
                Vector2 candidate = spawnAreaCenter + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
                if (mainTower != null && Vector2.Distance(candidate, mainTower.transform.position) < minimumDistanceFromTower) continue;
                bool tooClose = false;
                for (int i = 0; i < spawned.Count; i++)
                    if (spawned[i] != null && Vector2.Distance(candidate, spawned[i].transform.position) < minimumDistanceBetweenDebris) { tooClose = true; break; }
                if (tooClose || IlluminationSystem.IsLit(candidate)) continue;
                position = new Vector3(candidate.x, candidate.y, transform.position.z);
                return true;
            }
            position = default;
            return false;
        }

        private void Spawn(Vector3 position)
        {
            GameObject instance = debrisPrefab == null ? new GameObject("Stage 4 Debris") : Instantiate(debrisPrefab, position, Quaternion.identity, debrisRoot);
            if (debrisPrefab == null) { instance.transform.SetParent(debrisRoot, false); instance.transform.position = position; }
            Stage4Debris debris = instance.GetComponent<Stage4Debris>();
            if (debris == null) debris = instance.AddComponent<Stage4Debris>();
            debris.Initialize(debrisSprite, coinInventory, repairRequired, repairRatePerIntensity, repairReward, progressBarOffset, progressBarSize);
            spawned.Add(debris);
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < spawned.Count; i++) if (spawned[i] != null) Destroy(spawned[i].gameObject);
            spawned.Clear();
        }

        private void OnValidate()
        {
            spawnAreaSize.x = Mathf.Max(0.1f, spawnAreaSize.x);
            spawnAreaSize.y = Mathf.Max(0.1f, spawnAreaSize.y);
            debrisPerNight = Mathf.Max(0, debrisPerNight);
            maximumActiveDebris = Mathf.Max(1, maximumActiveDebris);
            spawnAttemptsPerDebris = Mathf.Max(1, spawnAttemptsPerDebris);
            minimumDistanceFromTower = Mathf.Max(0.1f, minimumDistanceFromTower);
            minimumDistanceBetweenDebris = Mathf.Max(0f, minimumDistanceBetweenDebris);
            repairRequired = Mathf.Max(0.01f, repairRequired);
            repairRatePerIntensity = Mathf.Max(0f, repairRatePerIntensity);
            repairReward = Mathf.Max(0, repairReward);
            progressBarSize.x = Mathf.Max(0.01f, progressBarSize.x);
            progressBarSize.y = Mathf.Max(0.01f, progressBarSize.y);
        }
    }
}
