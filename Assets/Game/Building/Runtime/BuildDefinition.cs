using UnityEngine;

namespace Game.Building
{
    [CreateAssetMenu(menuName = "Game/Building/Build Definition", fileName = "BuildDefinition")]
    public sealed class BuildDefinition : ScriptableObject
    {
        [SerializeField] private string buildingId = "lookout_tower";
        [SerializeField] private string displayName = "瞭望塔";
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField, Min(0)] private int coinCost = 10;
        [SerializeField] private bool canBuildDuringDay = true;

        [Header("Building Vitality")]
        [SerializeField, Min(1f)] private float maxHealth = 150f;

        [Header("Night Light")]
        [SerializeField] private bool emitsNightLight = true;
        [SerializeField, Min(0.01f)] private float lightRadius = 2.5f;
        [SerializeField, Min(0f)] private float lightIntensity = 0.8f;
        [SerializeField, Min(0f)] private float lightDamagePerSecond = 3f;
        [Tooltip("Safety cap keeps a building's light damage below the main tower's attack damage.")]
        [SerializeField, Min(0f)] private float lightDamageCap = 10f;

        [Header("Economy")]
        [SerializeField] private bool generatesCoins;
        [SerializeField, Min(1)] private int coinProductionAmount = 1;
        [SerializeField, Min(0.1f)] private float coinProductionInterval = 10f;

        public string BuildingId => buildingId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public Vector2Int Footprint => footprint;
        public int CoinCost => coinCost;
        public bool CanBuildDuringDay => canBuildDuringDay;
        public float MaxHealth => maxHealth;
        public bool EmitsNightLight => emitsNightLight;
        public float LightRadius => lightRadius;
        public float LightIntensity => lightIntensity;
        public float LightDamagePerSecond => lightDamagePerSecond;
        public float LightDamageCap => lightDamageCap;
        public bool GeneratesCoins => generatesCoins;
        public int CoinProductionAmount => coinProductionAmount;
        public float CoinProductionInterval => coinProductionInterval;

        private void OnValidate()
        {
            footprint.x = Mathf.Max(1, footprint.x);
            footprint.y = Mathf.Max(1, footprint.y);
            coinCost = Mathf.Max(0, coinCost);
            maxHealth = Mathf.Max(1f, maxHealth);
            lightRadius = Mathf.Max(0.01f, lightRadius);
            lightIntensity = Mathf.Max(0f, lightIntensity);
            lightDamagePerSecond = Mathf.Max(0f, lightDamagePerSecond);
            lightDamageCap = Mathf.Max(0f, lightDamageCap);
            coinProductionAmount = Mathf.Max(1, coinProductionAmount);
            coinProductionInterval = Mathf.Max(0.1f, coinProductionInterval);
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                buildingId = "building";
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = buildingId;
            }
        }
    }
}
