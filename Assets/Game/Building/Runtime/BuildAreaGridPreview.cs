using System.Collections.Generic;
using Game.Lighting;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Build Area Grid Preview")]
    [DisallowMultipleComponent]
    public sealed class BuildAreaGridPreview : MonoBehaviour
    {
        private static readonly Color EmptyCellColor =
            new Color(0.65f, 0.85f, 1f, 0.3f);

        private const int OutlineTextureSize = 16;
        private const int OutlineBorderSize = 1;

        [SerializeField] private BuildGrid buildGrid;
        [SerializeField] private BuildSystem buildSystem;
        [SerializeField] private Tilemap gridTilemap;
        [SerializeField] private TileBase outlineTile;
        [SerializeField] private bool hideOccupiedCells = true;

        private readonly List<Vector3Int> displayedCells =
            new List<Vector3Int>();

        private GameObject runtimeTilemapObject;
        private Tile runtimeTile;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private LightEmitter2D cachedBuildLight;
        private float cachedBuildRange = -1f;
        private bool isShown;

        public bool IsVisible => isShown;

        private void Awake()
        {
            ResolveReferences();
            EnsurePreviewResources();
            SetTilemapActive(false);
        }

        private void Update()
        {
            if (!isShown)
            {
                return;
            }

            ResolveReferences();
            LightEmitter2D currentLight = buildSystem == null
                ? null
                : buildSystem.BuildLight;
            float currentRange = currentLight == null
                ? -1f
                : currentLight.MaximumEffectiveRange;
            if (currentLight != cachedBuildLight ||
                !Mathf.Approximately(currentRange, cachedBuildRange))
            {
                Rebuild();
            }
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<BuildPlaced>(OnBuildPlaced);
        }

        private void OnDisable()
        {
            EventBus.Instance.UnSubscribe<BuildPlaced>(OnBuildPlaced);
            Hide();
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

        public void Show()
        {
            ResolveReferences();
            EnsurePreviewResources();
            if (buildGrid == null || gridTilemap == null ||
                outlineTile == null)
            {
                return;
            }

            isShown = true;
            SetTilemapActive(true);
            Rebuild();
        }

        public void Hide()
        {
            isShown = false;
            cachedBuildLight = null;
            cachedBuildRange = -1f;
            Clear();
            SetTilemapActive(false);
        }

        public void Rebuild()
        {
            ResolveReferences();
            EnsurePreviewResources();
            Clear();

            if (!isShown || buildGrid == null ||
                gridTilemap == null || outlineTile == null)
            {
                return;
            }

            LightEmitter2D buildLight = buildSystem == null
                ? null
                : buildSystem.BuildLight;
            cachedBuildLight = buildLight;
            cachedBuildRange = buildLight == null
                ? -1f
                : buildLight.MaximumEffectiveRange;
            if (buildLight == null)
            {
                return;
            }

            BoundsInt bounds = buildGrid.BuildBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0)
            {
                return;
            }

            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (!buildGrid.IsBuildableCell(cell) ||
                    hideOccupiedCells && buildGrid.IsOccupied(cell) ||
                    !buildGrid.IsFootprintInsideCircle(
                        cell,
                        Vector2Int.one,
                        buildLight.WorldPosition,
                        buildLight.MaximumEffectiveRange))
                {
                    continue;
                }

                gridTilemap.SetTile(cell, outlineTile);
                gridTilemap.SetTileFlags(cell, TileFlags.None);
                gridTilemap.SetColor(cell, EmptyCellColor);
                displayedCells.Add(cell);
            }
        }

        public void Clear()
        {
            if (gridTilemap != null)
            {
                foreach (Vector3Int cell in displayedCells)
                {
                    gridTilemap.SetTile(cell, null);
                }
            }

            displayedCells.Clear();
        }

        private void OnBuildPlaced(BuildPlaced placed)
        {
            if (isShown)
            {
                Rebuild();
            }
        }

        private void ResolveReferences()
        {
            if (buildGrid == null)
            {
                buildGrid = GetComponent<BuildGrid>() ??
                    FindObjectOfType<BuildGrid>();
            }

            if (buildSystem == null)
            {
                buildSystem = GetComponent<BuildSystem>() ??
                    FindObjectOfType<BuildSystem>();
            }
        }

        private void EnsurePreviewResources()
        {
            if (gridTilemap == null)
            {
                CreateRuntimeTilemap();
            }

            if (outlineTile == null)
            {
                CreateRuntimeOutlineTile();
            }
        }

        private void CreateRuntimeTilemap()
        {
            if (buildGrid == null || buildGrid.Grid == null)
            {
                return;
            }

            runtimeTilemapObject = new GameObject(
                "Build Area Grid Tilemap",
                typeof(Tilemap),
                typeof(TilemapRenderer));
            runtimeTilemapObject.transform.SetParent(
                buildGrid.Grid.transform,
                false);

            gridTilemap = runtimeTilemapObject.GetComponent<Tilemap>();
            TilemapRenderer gridRenderer =
                runtimeTilemapObject.GetComponent<TilemapRenderer>();

            Tilemap sourceTilemap = buildGrid.BuildableTilemap;
            if (sourceTilemap == null)
            {
                gridRenderer.sortingOrder = 0;
                return;
            }

            Transform sourceTransform = sourceTilemap.transform;
            Transform gridTransform = runtimeTilemapObject.transform;
            gridTransform.localPosition = sourceTransform.localPosition;
            gridTransform.localRotation = sourceTransform.localRotation;
            gridTransform.localScale = sourceTransform.localScale;

            gridTilemap.tileAnchor = sourceTilemap.tileAnchor;
            gridTilemap.orientation = sourceTilemap.orientation;
            gridTilemap.orientationMatrix = sourceTilemap.orientationMatrix;

            TilemapRenderer sourceRenderer =
                sourceTilemap.GetComponent<TilemapRenderer>();
            if (sourceRenderer != null)
            {
                gridRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                gridRenderer.sortingOrder = sourceRenderer.sortingOrder + 1;
            }
        }

        private void CreateRuntimeOutlineTile()
        {
            runtimeTexture = new Texture2D(
                OutlineTextureSize,
                OutlineTextureSize,
                TextureFormat.RGBA32,
                false)
            {
                name = "Build Area Grid Outline Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < OutlineTextureSize; y++)
            {
                for (int x = 0; x < OutlineTextureSize; x++)
                {
                    bool isBorder =
                        x < OutlineBorderSize ||
                        y < OutlineBorderSize ||
                        x >= OutlineTextureSize - OutlineBorderSize ||
                        y >= OutlineTextureSize - OutlineBorderSize;
                    runtimeTexture.SetPixel(
                        x,
                        y,
                        isBorder ? Color.white : Color.clear);
                }
            }

            runtimeTexture.Apply(false, false);
            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(
                    0f,
                    0f,
                    OutlineTextureSize,
                    OutlineTextureSize),
                new Vector2(0.5f, 0.5f),
                OutlineTextureSize);
            runtimeSprite.name = "Build Area Grid Outline Sprite";
            runtimeSprite.hideFlags = HideFlags.HideAndDontSave;

            runtimeTile = ScriptableObject.CreateInstance<Tile>();
            runtimeTile.name = "Build Area Grid Outline Tile";
            runtimeTile.sprite = runtimeSprite;
            runtimeTile.color = Color.white;
            runtimeTile.colliderType = Tile.ColliderType.None;
            runtimeTile.hideFlags = HideFlags.HideAndDontSave;
            outlineTile = runtimeTile;
        }

        private void SetTilemapActive(bool active)
        {
            if (gridTilemap != null)
            {
                gridTilemap.gameObject.SetActive(active);
            }
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
