using Game.DayNight;
using Game.Lighting;
using UnityEngine;

namespace Game.Building
{
    public enum BuildPlacementFailureReason
    {
        None,
        MissingDefinition,
        MissingPrefab,
        WrongPhase,
        OutsideBuildBounds,
        OutsideLightRange,
        UnbuildableCell,
        Occupied,
        InsufficientCoins,
        InvalidFootprint,
        MissingGrid,
        MissingInventory,
        MissingBuildLight
    }

    public readonly struct BuildPlacementResult
    {
        public readonly bool IsValid;
        public readonly BuildPlacementFailureReason Reason;

        public BuildPlacementResult(
            bool isValid,
            BuildPlacementFailureReason reason)
        {
            IsValid = isValid;
            Reason = reason;
        }

        public static BuildPlacementResult Valid =>
            new BuildPlacementResult(true, BuildPlacementFailureReason.None);
    }

    public sealed class BuildPlacementValidator
    {
        public BuildPlacementResult Validate(
            BuildDefinition definition,
            Vector3Int cellPosition,
            DayNightSystem dayNightSystem,
            BuildGrid buildGrid,
            CoinInventory coinInventory,
            LightEmitter2D buildLight)
        {
            if (definition == null)
            {
                return Invalid(BuildPlacementFailureReason.MissingDefinition);
            }

            if (definition.Prefab == null)
            {
                return Invalid(BuildPlacementFailureReason.MissingPrefab);
            }

            if (dayNightSystem == null ||
                dayNightSystem.CurrentPhase != DayNightPhase.Day ||
                !definition.CanBuildDuringDay)
            {
                return Invalid(BuildPlacementFailureReason.WrongPhase);
            }

            Vector2Int footprint = definition.Footprint;
            if (footprint.x <= 0 || footprint.y <= 0)
            {
                return Invalid(BuildPlacementFailureReason.InvalidFootprint);
            }

            if (buildGrid == null)
            {
                return Invalid(BuildPlacementFailureReason.MissingGrid);
            }

            if (!buildGrid.IsInsideBounds(cellPosition, footprint))
            {
                return Invalid(BuildPlacementFailureReason.OutsideBuildBounds);
            }

            if (!buildGrid.AreCellsBuildable(cellPosition, footprint))
            {
                return Invalid(BuildPlacementFailureReason.UnbuildableCell);
            }

            if (buildLight == null)
            {
                return Invalid(BuildPlacementFailureReason.MissingBuildLight);
            }

            if (!buildGrid.IsFootprintInsideCircle(
                    cellPosition,
                    footprint,
                    buildLight.WorldPosition,
                    buildLight.MaximumEffectiveRange))
            {
                return Invalid(BuildPlacementFailureReason.OutsideLightRange);
            }

            if (!buildGrid.CanOccupy(cellPosition, footprint))
            {
                return Invalid(BuildPlacementFailureReason.Occupied);
            }

            if (coinInventory == null)
            {
                return Invalid(BuildPlacementFailureReason.MissingInventory);
            }

            if (!coinInventory.CanSpend(definition.CoinCost))
            {
                return Invalid(BuildPlacementFailureReason.InsufficientCoins);
            }

            return BuildPlacementResult.Valid;
        }

        private static BuildPlacementResult Invalid(BuildPlacementFailureReason reason)
        {
            return new BuildPlacementResult(false, reason);
        }
    }
}
