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

        public string BuildingId => buildingId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public Vector2Int Footprint => footprint;
        public int CoinCost => coinCost;
        public bool CanBuildDuringDay => canBuildDuringDay;

        private void OnValidate()
        {
            footprint.x = Mathf.Max(1, footprint.x);
            footprint.y = Mathf.Max(1, footprint.y);
            coinCost = Mathf.Max(0, coinCost);
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
