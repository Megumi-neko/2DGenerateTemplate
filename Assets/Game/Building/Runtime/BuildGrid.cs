using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Build Grid")]
    [DisallowMultipleComponent]
    public sealed class BuildGrid : MonoBehaviour
    {
        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap buildableTilemap;
        [SerializeField] private BoundsInt buildBounds;

        private readonly Dictionary<Vector3Int, BuildInstance> occupiedCells =
            new Dictionary<Vector3Int, BuildInstance>();

        public Grid Grid
        {
            get
            {
                ResolveReferences();
                return grid;
            }
        }

        public Tilemap BuildableTilemap
        {
            get
            {
                ResolveReferences();
                return buildableTilemap;
            }
        }

        public BoundsInt BuildBounds
        {
            get
            {
                ResolveReferences();
                return GetBuildBounds();
            }
        }

        public IReadOnlyDictionary<Vector3Int, BuildInstance> OccupiedCells => occupiedCells;

        private void Awake()
        {
            ResolveReferences();
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            ResolveReferences();
            if (buildableTilemap != null)
            {
                return buildableTilemap.WorldToCell(worldPosition);
            }

            return grid == null
                ? Vector3Int.RoundToInt(worldPosition)
                : grid.WorldToCell(worldPosition);
        }

        public Vector3 CellToWorld(Vector3Int cellPosition, Vector2Int footprint)
        {
            ResolveReferences();
            if (buildableTilemap != null)
            {
                Vector3 origin = buildableTilemap.GetCellCenterWorld(cellPosition);
                Vector3 xStep = buildableTilemap.CellToWorld(
                    cellPosition + Vector3Int.right) -
                    buildableTilemap.CellToWorld(cellPosition);
                Vector3 yStep = buildableTilemap.CellToWorld(
                    cellPosition + Vector3Int.up) -
                    buildableTilemap.CellToWorld(cellPosition);
                return origin +
                    xStep * ((footprint.x - 1) * 0.5f) +
                    yStep * ((footprint.y - 1) * 0.5f);
            }

            if (grid == null)
            {
                return cellPosition + new Vector3(
                    (footprint.x - 1) * 0.5f,
                    (footprint.y - 1) * 0.5f,
                    0f);
            }

            Vector3 gridOrigin = grid.GetCellCenterWorld(cellPosition);
            Vector3 cellSize = grid.cellSize;
            return gridOrigin + new Vector3(
                (footprint.x - 1) * cellSize.x * 0.5f,
                (footprint.y - 1) * cellSize.y * 0.5f,
                0f);
        }

        public bool IsInsideBounds(Vector3Int cellPosition, Vector2Int footprint)
        {
            BoundsInt bounds = GetBuildBounds();
            if (bounds.size.x <= 0 || bounds.size.y <= 0)
            {
                return true;
            }

            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    Vector3Int cell = cellPosition + new Vector3Int(x, y, 0);
                    if (!bounds.Contains(cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool IsOccupied(Vector3Int cellPosition)
        {
            return occupiedCells.ContainsKey(cellPosition);
        }

        public bool IsBuildableCell(Vector3Int cellPosition)
        {
            BoundsInt bounds = BuildBounds;
            if (bounds.size.x > 0 && bounds.size.y > 0 &&
                !bounds.Contains(cellPosition))
            {
                return false;
            }

            return buildableTilemap == null ||
                buildableTilemap.HasTile(cellPosition);
        }

        public bool AreCellsBuildable(
            Vector3Int cellPosition,
            Vector2Int footprint)
        {
            if (footprint.x <= 0 || footprint.y <= 0)
            {
                return false;
            }

            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    if (!IsBuildableCell(
                            cellPosition + new Vector3Int(x, y, 0)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool CanOccupyCell(Vector3Int cellPosition)
        {
            return IsBuildableCell(cellPosition) &&
                !IsOccupied(cellPosition);
        }

        public bool CanOccupy(Vector3Int cellPosition, Vector2Int footprint)
        {
            if (!AreCellsBuildable(cellPosition, footprint))
            {
                return false;
            }

            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    if (!CanOccupyCell(
                            cellPosition + new Vector3Int(x, y, 0)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void GetOccupiedCells(
            Vector3Int cellPosition,
            Vector2Int footprint,
            List<Vector3Int> results)
        {
            if (results == null)
            {
                throw new System.ArgumentNullException(nameof(results));
            }

            results.Clear();
            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    results.Add(cellPosition + new Vector3Int(x, y, 0));
                }
            }
        }

        public bool TryRegister(
            BuildInstance instance,
            Vector3Int cellPosition,
            Vector2Int footprint)
        {
            if (instance == null || !CanOccupy(cellPosition, footprint))
            {
                return false;
            }

            List<Vector3Int> cells = new List<Vector3Int>();
            GetOccupiedCells(cellPosition, footprint, cells);
            foreach (Vector3Int cell in cells)
            {
                occupiedCells[cell] = instance;
            }

            return true;
        }

        public void ClearForTests()
        {
            occupiedCells.Clear();
        }

        internal void ConfigureForTests(Grid gridToUse, BoundsInt bounds)
        {
            grid = gridToUse;
            buildableTilemap = null;
            buildBounds = bounds;
            occupiedCells.Clear();
        }

        private BoundsInt GetBuildBounds()
        {
            if (buildBounds.size.x > 0 && buildBounds.size.y > 0)
            {
                return buildBounds;
            }

            return buildableTilemap == null ? default : buildableTilemap.cellBounds;
        }

        private void ResolveReferences()
        {
            if (grid == null)
            {
                grid = FindObjectOfType<Grid>();
            }

            if (buildableTilemap == null)
            {
                buildableTilemap = FindObjectOfType<Tilemap>();
            }
        }
    }
}
