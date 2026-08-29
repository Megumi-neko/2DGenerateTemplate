using System.Collections.Generic;
using Game.DayNight;
using Game.Lighting;
using UnityEngine;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Build System")]
    [DisallowMultipleComponent]
    public sealed class BuildSystem : MonoBehaviour
    {
        [SerializeField] private BuildGrid buildGrid;
        [SerializeField] private DayNightSystem dayNightSystem;
        [SerializeField] private CoinInventory coinInventory;
        [SerializeField] private Transform buildingsRoot;
        [SerializeField] private BuildDefinition defaultDefinition;
        [SerializeField] private LightEmitter2D buildLight;

        private readonly List<BuildInstance> builds = new List<BuildInstance>();
        private readonly BuildPlacementValidator validator = new BuildPlacementValidator();

        public IReadOnlyList<BuildInstance> Builds => builds;
        public BuildPlacementFailureReason LastFailureReason { get; private set; }
        public BuildGrid Grid => buildGrid;
        public BuildDefinition DefaultDefinition => defaultDefinition;
        public LightEmitter2D BuildLight => buildLight;

        private void Awake()
        {
            ResolveReferences();
        }

        public BuildPlacementResult ValidatePlacement(
            BuildDefinition definition,
            Vector3Int cellPosition)
        {
            ResolveReferences();
            BuildPlacementResult result = validator.Validate(
                definition,
                cellPosition,
                dayNightSystem,
                buildGrid,
                coinInventory,
                buildLight);
            LastFailureReason = result.Reason;
            return result;
        }

        public bool TryPlace(
            BuildDefinition definition,
            Vector3Int cellPosition)
        {
            BuildPlacementResult result = ValidatePlacement(definition, cellPosition);
            if (!result.IsValid)
            {
                PublishPlacementFailed(definition, cellPosition, result.Reason);
                return false;
            }

            if (!coinInventory.TrySpend(definition.CoinCost))
            {
                LastFailureReason = BuildPlacementFailureReason.InsufficientCoins;
                PublishPlacementFailed(definition, cellPosition, LastFailureReason);
                return false;
            }

            Transform parent = buildingsRoot == null ? transform : buildingsRoot;
            Vector3 worldPosition = buildGrid.CellToWorld(
                cellPosition,
                definition.Footprint);
            GameObject instanceObject = Instantiate(
                definition.Prefab,
                worldPosition,
                Quaternion.identity,
                parent);
            BuildInstance instance = instanceObject.GetComponent<BuildInstance>();
            if (instance == null)
            {
                Destroy(instanceObject);
                coinInventory.Add(definition.CoinCost);
                LastFailureReason = BuildPlacementFailureReason.MissingPrefab;
                PublishPlacementFailed(definition, cellPosition, LastFailureReason);
                return false;
            }

            instance.Initialize(definition, cellPosition, 0, coinInventory);
            if (!buildGrid.TryRegister(instance, cellPosition, definition.Footprint))
            {
                Destroy(instanceObject);
                coinInventory.Add(definition.CoinCost);
                LastFailureReason = BuildPlacementFailureReason.Occupied;
                PublishPlacementFailed(definition, cellPosition, LastFailureReason);
                return false;
            }

            builds.Add(instance);
            instance.BuildingHealth.Died += _ => RemoveBuild(instance);
            LastFailureReason = BuildPlacementFailureReason.None;
            EventBus.Instance.Publish(new BuildPlaced(
                instance,
                definition.BuildingId,
                cellPosition,
                definition.Footprint));
            return true;
        }

        public bool RemoveBuild(BuildInstance instance)
        {
            if (instance == null || !builds.Remove(instance)) return false;
            buildGrid?.Unregister(instance);
            return true;
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            ResolveReferences();
            return buildGrid == null
                ? Vector3Int.RoundToInt(worldPosition)
                : buildGrid.WorldToCell(worldPosition);
        }

        public Vector3 CellToWorld(
            Vector3Int cellPosition,
            BuildDefinition definition)
        {
            ResolveReferences();
            return buildGrid == null || definition == null
                ? cellPosition
                : buildGrid.CellToWorld(cellPosition, definition.Footprint);
        }

        public bool TryGetBuild(
            Vector3Int cellPosition,
            out BuildInstance instance)
        {
            ResolveReferences();
            if (buildGrid != null && buildGrid.OccupiedCells.TryGetValue(cellPosition, out instance))
            {
                return instance != null;
            }

            instance = null;
            return false;
        }

        internal void ConfigureForTests(
            BuildGrid grid,
            DayNightSystem system,
            CoinInventory inventory)
        {
            buildGrid = grid;
            dayNightSystem = system;
            coinInventory = inventory;
            builds.Clear();
            LastFailureReason = BuildPlacementFailureReason.None;
        }

        internal void ConfigureLightingForTests(LightEmitter2D light)
        {
            buildLight = light;
        }

        private void ResolveReferences()
        {
            if (buildGrid == null)
            {
                buildGrid = GetComponent<BuildGrid>();
                if (buildGrid == null)
                {
                    buildGrid = FindObjectOfType<BuildGrid>();
                }
            }

            if (dayNightSystem == null)
            {
                dayNightSystem = FindObjectOfType<DayNightSystem>();
            }

            if (coinInventory == null)
            {
                coinInventory = GetComponent<CoinInventory>();
                if (coinInventory == null)
                {
                    coinInventory = FindObjectOfType<CoinInventory>();
                }
            }

            StageLightingBootstrap bootstrap = FindObjectOfType<StageLightingBootstrap>();
            LightEmitter2D stageCandle = bootstrap == null ? null : bootstrap.CandleEmitter;
            LightEmitter2D longestSector = IlluminationSystem.GetLongestSectorEmitter(true);
            if (longestSector != null)
            {
                buildLight = longestSector;
            }
            else if (stageCandle != null)
            {
                buildLight = stageCandle;
            }
        }

        private static void PublishPlacementFailed(
            BuildDefinition definition,
            Vector3Int cellPosition,
            BuildPlacementFailureReason reason)
        {
            EventBus.Instance.Publish(new BuildPlacementFailed(
                definition == null ? string.Empty : definition.BuildingId,
                cellPosition,
                reason));
        }
    }
}
