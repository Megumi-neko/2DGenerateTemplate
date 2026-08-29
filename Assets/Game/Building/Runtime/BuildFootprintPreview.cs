using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Build Footprint Preview")]
    [DisallowMultipleComponent]
    public sealed class BuildFootprintPreview : MonoBehaviour
    {
        private static readonly Color ValidColor =
            new Color(0.2f, 1f, 0.3f, 0.45f);
        private static readonly Color InvalidColor =
            new Color(1f, 0.15f, 0.15f, 0.55f);
        private static readonly Color InsufficientCoinsColor =
            new Color(1f, 0.75f, 0.1f, 0.55f);
        private static readonly Color WrongPhaseColor =
            new Color(0.5f, 0.5f, 0.5f, 0.45f);

        [SerializeField] private BuildGrid buildGrid;
        [SerializeField] private Tilemap previewTilemap;
        [SerializeField] private TileBase previewTile;

        private readonly List<Vector3Int> visibleCells =
            new List<Vector3Int>();

        private GameObject runtimeTilemapObject;
        private Tile runtimeTile;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;

        public bool IsVisible => visibleCells.Count > 0;

        private void Awake()
        {
            ResolveReferences();
            EnsurePreviewResources();
        }

        public void Show(
            Vector3Int originCell,
            Vector2Int footprint,
            BuildPlacementResult placementResult)
        {
            ResolveReferences();
            EnsurePreviewResources();
            Clear();

            if (buildGrid == null || previewTilemap == null ||
                previewTile == null || footprint.x <= 0 || footprint.y <= 0)
            {
                return;
            }

            buildGrid.GetOccupiedCells(
                originCell,
                footprint,
                visibleCells);

            foreach (Vector3Int cell in visibleCells)
            {
                previewTilemap.SetTile(cell, previewTile);
                previewTilemap.SetTileFlags(cell, TileFlags.None);
                previewTilemap.SetColor(
                    cell,
                    GetCellColor(cell, placementResult));
            }
        }

        public void Clear()
        {
            if (previewTilemap != null)
            {
                foreach (Vector3Int cell in visibleCells)
                {
                    previewTilemap.SetTile(cell, null);
                }
            }

            visibleCells.Clear();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
            DestroyRuntimeObject(runtimeTile);
            DestroyRuntimeObject(runtimeSprite);
            DestroyRuntimeObject(runtimeTexture);

            if (runtimeTilemapObject != null)
            {
                DestroyRuntimeObject(runtimeTilemapObject);
            }
        }

        private Color GetCellColor(
            Vector3Int cell,
            BuildPlacementResult placementResult)
        {
            if (placementResult.IsValid)
            {
                return ValidColor;
            }

            switch (placementResult.Reason)
            {
                case BuildPlacementFailureReason.Occupied:
                case BuildPlacementFailureReason.OutsideBuildBounds:
                case BuildPlacementFailureReason.UnbuildableCell:
                    return buildGrid.CanOccupyCell(cell)
                        ? ValidColor
                        : InvalidColor;

                case BuildPlacementFailureReason.OutsideLightRange:
                    return InvalidColor;

                case BuildPlacementFailureReason.InsufficientCoins:
                    return InsufficientCoinsColor;

                case BuildPlacementFailureReason.WrongPhase:
                    return WrongPhaseColor;

                default:
                    return InvalidColor;
            }
        }

        private void ResolveReferences()
        {
            if (buildGrid == null)
            {
                buildGrid = GetComponent<BuildGrid>() ??
                    FindObjectOfType<BuildGrid>();
            }
        }

        private void EnsurePreviewResources()
        {
            if (previewTilemap == null)
            {
                CreateRuntimeTilemap();
            }

            if (previewTile == null)
            {
                CreateRuntimeTile();
            }
        }

        private void CreateRuntimeTilemap()
        {
            if (buildGrid == null || buildGrid.Grid == null)
            {
                return;
            }

            runtimeTilemapObject = new GameObject(
                "Build Preview Tilemap",
                typeof(Tilemap),
                typeof(TilemapRenderer));
            runtimeTilemapObject.transform.SetParent(
                buildGrid.Grid.transform,
                false);

            previewTilemap = runtimeTilemapObject.GetComponent<Tilemap>();
            TilemapRenderer previewRenderer =
                runtimeTilemapObject.GetComponent<TilemapRenderer>();

            Tilemap sourceTilemap = buildGrid.BuildableTilemap;
            if (sourceTilemap == null)
            {
                previewRenderer.sortingOrder = 1;
                return;
            }

            Transform sourceTransform = sourceTilemap.transform;
            Transform previewTransform = runtimeTilemapObject.transform;
            previewTransform.localPosition = sourceTransform.localPosition;
            previewTransform.localRotation = sourceTransform.localRotation;
            previewTransform.localScale = sourceTransform.localScale;

            previewTilemap.tileAnchor = sourceTilemap.tileAnchor;
            previewTilemap.orientation = sourceTilemap.orientation;
            previewTilemap.orientationMatrix = sourceTilemap.orientationMatrix;

            TilemapRenderer sourceRenderer =
                sourceTilemap.GetComponent<TilemapRenderer>();
            if (sourceRenderer != null)
            {
                previewRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                previewRenderer.sortingOrder = sourceRenderer.sortingOrder + 2;
            }
        }

        private void CreateRuntimeTile()
        {
            runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Build Preview Cell Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            runtimeTexture.SetPixel(0, 0, Color.white);
            runtimeTexture.Apply(false, false);

            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            runtimeSprite.name = "Build Preview Cell Sprite";
            runtimeSprite.hideFlags = HideFlags.HideAndDontSave;

            runtimeTile = ScriptableObject.CreateInstance<Tile>();
            runtimeTile.name = "Build Preview Cell Tile";
            runtimeTile.sprite = runtimeSprite;
            runtimeTile.color = Color.white;
            runtimeTile.colliderType = Tile.ColliderType.None;
            runtimeTile.hideFlags = HideFlags.HideAndDontSave;
            previewTile = runtimeTile;
        }

        private static void DestroyRuntimeObject(Object runtimeObject)
        {
            if (runtimeObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeObject);
            }
            else
            {
                DestroyImmediate(runtimeObject);
            }
        }
    }
}
