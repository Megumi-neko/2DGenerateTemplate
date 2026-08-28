using UnityEngine;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Build Instance")]
    [DisallowMultipleComponent]
    public sealed class BuildInstance : MonoBehaviour
    {
        public BuildDefinition Definition { get; private set; }
        public Vector3Int CellPosition { get; private set; }
        public int RotationQuarterTurns { get; private set; }

        public void Initialize(
            BuildDefinition definition,
            Vector3Int cellPosition,
            int rotationQuarterTurns = 0)
        {
            Definition = definition;
            CellPosition = cellPosition;
            RotationQuarterTurns = rotationQuarterTurns;
        }
    }
}
