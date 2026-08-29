using Game.Building;
using Game.DayNight;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    [AddComponentMenu("Game/UI/Build Input Controller")]
    [DisallowMultipleComponent]
    public sealed class BuildInputController : MonoBehaviour
    {
        [SerializeField] private BuildSystem buildSystem;
        [SerializeField] private BuildPreview buildPreview;
        [SerializeField] private BuildAreaGridPreview areaGridPreview;
        [SerializeField] private BuildFootprintPreview footprintPreview;
        [SerializeField] private BuildDefinition lookoutTower;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private BuildPlacementCameraController placementCameraController;
        [SerializeField] private StageUIController stageUIController;

        private Button lookoutTowerButton;
        private Vector3Int currentCellPosition;
        private bool isPlacing;
        private bool skipPointerClick;

        private void Awake()
        {
            ResolveReferences();
            CreateLookoutTowerButton();
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
            CancelPlacement();
        }

        private void Update()
        {
            if (!isPlacing)
            {
                return;
            }

            if (skipPointerClick)
            {
                skipPointerClick = false;
            }

            placementCameraController?.UpdatePlacement();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            if (!TryGetPointerWorldPosition(out Vector3 worldPosition))
            {
                buildPreview?.Hide();
                if (footprintPreview != null)
                {
                    footprintPreview.Clear();
                }
                return;
            }

            currentCellPosition = buildSystem.WorldToCell(worldPosition);
            BuildPlacementResult result = buildSystem.ValidatePlacement(
                lookoutTower,
                currentCellPosition);
            if (buildPreview != null)
            {
                buildPreview.Show(
                    lookoutTower,
                    buildSystem.CellToWorld(currentCellPosition, lookoutTower),
                    result.IsValid);
            }
            if (footprintPreview != null)
            {
                footprintPreview.Show(
                    currentCellPosition,
                    lookoutTower.Footprint,
                    result);
            }

            if (!skipPointerClick &&
                Input.GetMouseButtonDown(0) &&
                !IsPointerOverUi() &&
                result.IsValid)
            {
                if (buildSystem.TryPlace(lookoutTower, currentCellPosition))
                {
                    CancelPlacement();
                }
            }
        }

        public void BeginLookoutTowerPlacement()
        {
            ResolveReferences();
            if (buildSystem == null || lookoutTower == null || buildPreview == null)
            {
                return;
            }

            if (buildSystem.ValidatePlacement(
                    lookoutTower,
                    buildSystem.WorldToCell(transform.position)).Reason ==
                BuildPlacementFailureReason.WrongPhase)
            {
                return;
            }

            isPlacing = true;
            skipPointerClick = true;
            if (placementCameraController != null &&
                !placementCameraController.BeginPlacement())
            {
                isPlacing = false;
                return;
            }

            if (areaGridPreview != null)
            {
                areaGridPreview.Show();
            }

            if (stageUIController != null)
            {
                stageUIController.CloseConstruct();
            }
        }

        public void CancelPlacement()
        {
            isPlacing = false;
            skipPointerClick = false;
            if (buildPreview != null)
            {
                buildPreview.Hide();
            }

            if (footprintPreview != null)
            {
                footprintPreview.Clear();
            }

            if (areaGridPreview != null)
            {
                areaGridPreview.Hide();
            }

            placementCameraController?.EndPlacement();
        }

        private void OnDayNightStateChanged(DayNightStateChanged state)
        {
            if (state.Phase != DayNightPhase.Day)
            {
                CancelPlacement();
            }
        }

        private void ResolveReferences()
        {
            if (buildSystem == null)
            {
                buildSystem = FindObjectOfType<BuildSystem>();
            }

            if (buildPreview == null)
            {
                buildPreview = FindObjectOfType<BuildPreview>();
            }

            if (footprintPreview == null)
            {
                footprintPreview = FindObjectOfType<BuildFootprintPreview>();
            }

            if (areaGridPreview == null)
            {
                areaGridPreview = FindObjectOfType<BuildAreaGridPreview>();
            }

            if (lookoutTower == null && buildSystem != null)
            {
                lookoutTower = buildSystem.DefaultDefinition;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (placementCameraController == null)
            {
                placementCameraController = FindObjectOfType<BuildPlacementCameraController>();
            }

            if (stageUIController == null)
            {
                stageUIController = FindObjectOfType<StageUIController>();
            }
        }

        private void CreateLookoutTowerButton()
        {
            if (stageUIController == null || stageUIController.ConstructPanel == null)
            {
                return;
            }

            Transform panel = stageUIController.ConstructPanel.transform;
            Transform existingButton = panel.Find("Lookout Tower Button");
            if (existingButton != null)
            {
                lookoutTowerButton = existingButton.GetComponent<Button>();
            }

            if (lookoutTowerButton == null)
            {
                GameObject buttonObject = new GameObject(
                    "Lookout Tower Button",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                buttonObject.transform.SetParent(panel, false);
                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(24f, 0f);
                rect.sizeDelta = new Vector2(180f, 64f);

                Image image = buttonObject.GetComponent<Image>();
                image.color = new Color(0.2f, 0.5f, 0.25f, 1f);
                lookoutTowerButton = buttonObject.GetComponent<Button>();
                lookoutTowerButton.targetGraphic = image;

                GameObject textObject = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(Text));
                textObject.transform.SetParent(buttonObject.transform, false);
                RectTransform textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                Text label = textObject.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 20;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.text = "瞭望塔\n影结晶：10";
            }

            RefreshLookoutTowerLabel();
            lookoutTowerButton.onClick.RemoveListener(BeginLookoutTowerPlacement);
            lookoutTowerButton.onClick.AddListener(BeginLookoutTowerPlacement);
        }

        private void RefreshLookoutTowerLabel()
        {
            if (lookoutTowerButton == null || lookoutTower == null)
            {
                return;
            }

            Text label = lookoutTowerButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = $"{lookoutTower.DisplayName}\n影结晶：{lookoutTower.CoinCost}";
            }
        }

        private bool TryGetPointerWorldPosition(out Vector3 worldPosition)
        {
            if (targetCamera == null)
            {
                worldPosition = default;
                return false;
            }

            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Abs(ray.direction.z) <= 0.0001f)
            {
                worldPosition = default;
                return false;
            }

            float distance = -ray.origin.z / ray.direction.z;
            if (distance < 0f)
            {
                worldPosition = default;
                return false;
            }

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject();
        }
    }
}
