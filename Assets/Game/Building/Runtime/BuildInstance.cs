using Game.DayNight;
using Game.Lighting;
using UnityEngine;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Build Instance")]
    [DisallowMultipleComponent]
    public sealed class BuildInstance : MonoBehaviour
    {
        private const int MaximumProductionTicksPerFrame = 8;

        public BuildDefinition Definition { get; private set; }
        public Vector3Int CellPosition { get; private set; }
        public int RotationQuarterTurns { get; private set; }
        public bool IsInitialized { get; private set; }

        private CoinInventory coinInventory;
        private BuildingHealth buildingHealth;
        private BuildingHealthBar healthBar;
        private LightEmitter2D lightEmitter;
        private float productionTimer;

        public BuildingHealth BuildingHealth => buildingHealth;
        public LightEmitter2D LightEmitter => lightEmitter;

        private void Awake()
        {
            buildingHealth = GetComponent<BuildingHealth>();
            if (buildingHealth == null) buildingHealth = gameObject.AddComponent<BuildingHealth>();
            healthBar = GetComponent<BuildingHealthBar>();
            if (healthBar == null) healthBar = gameObject.AddComponent<BuildingHealthBar>();
            healthBar.Initialize(buildingHealth);
            buildingHealth.Died += OnBuildingDied;
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
            UpdateNightLight();
        }

        private void OnDestroy()
        {
            if (buildingHealth != null) buildingHealth.Died -= OnBuildingDied;
            EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
            if (lightEmitter != null) Destroy(lightEmitter);
        }

        private void UpdateNightLight()
        {
            if (!IsInitialized || Definition == null) return;
            if (lightEmitter == null) lightEmitter = GetComponent<LightEmitter2D>();
            if (lightEmitter == null) lightEmitter = gameObject.AddComponent<LightEmitter2D>();
            lightEmitter.Shape = LightShape2D.Circle;
            lightEmitter.BaseRadius = Definition.LightRadius;
            lightEmitter.BaseIntensity = Definition.LightIntensity;
            lightEmitter.BaseDamagePerSecond = Mathf.Min(
                Definition.LightDamagePerSecond,
                Definition.LightDamageCap);
            DayNightSystem dayNight = FindObjectOfType<DayNightSystem>();
            lightEmitter.SetEmitting(Definition.EmitsNightLight && buildingHealth != null && !buildingHealth.IsDead &&
                (dayNight == null || dayNight.CurrentPhase == DayNightPhase.Night));
        }

        private void OnDayNightStateChanged(DayNightStateChanged state) { UpdateNightLight(); }
        private bool IsNight()
        {
            DayNightSystem system = FindObjectOfType<DayNightSystem>();
            return system == null || system.CurrentPhase == DayNightPhase.Night;
        }

        private void OnBuildingDied(BuildingHealth _)
        {
            lightEmitter?.SetEmitting(false);
            Destroy(gameObject);
        }

        private void Update()
        {
            if (!isActiveAndEnabled || !IsInitialized || Definition == null ||
                !Definition.GeneratesCoins || coinInventory == null ||
                Definition.CoinProductionAtNightOnly && !IsNight())
            {
                if (Definition != null && Definition.CoinProductionAtNightOnly)
                {
                    productionTimer = 0f;
                }
                return;
            }

            productionTimer += Time.deltaTime;
            float interval = Definition.CoinProductionInterval;
            int ticks = 0;
            while (productionTimer >= interval && ticks < MaximumProductionTicksPerFrame)
            {
                productionTimer -= interval;
                coinInventory.Add(Definition.CoinProductionAmount);
                ticks++;
            }

            if (ticks == MaximumProductionTicksPerFrame && productionTimer >= interval)
            {
                productionTimer = interval;
            }
        }

        public void Initialize(
            BuildDefinition definition,
            Vector3Int cellPosition,
            int rotationQuarterTurns = 0)
        {
            Initialize(definition, cellPosition, rotationQuarterTurns, null);
        }

        public void Initialize(
            BuildDefinition definition,
            Vector3Int cellPosition,
            int rotationQuarterTurns,
            CoinInventory inventory)
        {
            Definition = definition;
            CellPosition = cellPosition;
            RotationQuarterTurns = rotationQuarterTurns;
            coinInventory = inventory;
            productionTimer = 0f;
            IsInitialized = definition != null;
            if (IsInitialized)
            {
                if (buildingHealth == null) buildingHealth = GetComponent<BuildingHealth>();
                buildingHealth?.ResetHealth(definition.MaxHealth);
                healthBar?.SetOffset(definition.BuildingId == "crystal_factory"
                    ? new Vector3(-0.02f, 1.1f, 0f)
                    : new Vector3(0.06f, 1.1f, 0f));
                UpdateNightLight();
            }
        }

        public void ConfigureEconomy(CoinInventory inventory)
        {
            coinInventory = inventory;
        }

        private void OnDisable()
        {
            productionTimer = 0f;
        }
    }
}
