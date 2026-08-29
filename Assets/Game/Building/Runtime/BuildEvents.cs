using System;
using UnityEngine;

namespace Game.Building
{
    public readonly struct BuildPlaced
    {
        public readonly BuildInstance Instance;
        public readonly string BuildingId;
        public readonly Vector3Int CellPosition;
        public readonly Vector2Int Footprint;

        public BuildPlaced(
            BuildInstance instance,
            string buildingId,
            Vector3Int cellPosition,
            Vector2Int footprint)
        {
            Instance = instance;
            BuildingId = buildingId;
            CellPosition = cellPosition;
            Footprint = footprint;
        }
    }

    public readonly struct BuildPlacementFailed
    {
        public readonly string BuildingId;
        public readonly Vector3Int CellPosition;
        public readonly BuildPlacementFailureReason Reason;

        public BuildPlacementFailed(
            string buildingId,
            Vector3Int cellPosition,
            BuildPlacementFailureReason reason)
        {
            BuildingId = buildingId;
            CellPosition = cellPosition;
            Reason = reason;
        }
    }
}
