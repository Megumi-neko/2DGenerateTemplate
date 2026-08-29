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
        private float productionTimer;

        private void Update()
        {
            if (!isActiveAndEnabled || !IsInitialized || Definition == null ||
                !Definition.GeneratesCoins || coinInventory == null)
            {
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
