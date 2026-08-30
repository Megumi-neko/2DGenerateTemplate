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
        [SerializeField] private BuildDefinition crystalFactory;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private BuildPlacementCameraController placementCameraController;
        [SerializeField] private StageUIController stageUIController;

        private Button lookoutTowerButton;
        [SerializeField] private Button crystalFactoryButton;
        private BuildDefinition selectedDefinition;
        private Vector3Int currentCellPosition;
        private bool isPlacing;
        private bool skipPointerClick;

        private void Awake()
        {
            ResolveReferences();
            ConfigureLookoutTowerButton();
            ConfigureCrystalFactoryButton();
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
            ResolveReferences();
            ConfigureLookoutTowerButton();
            ConfigureCrystalFactoryButton();
        }

        private void Start()
        {
            ResolveReferences();
            ConfigureLookoutTowerButton();
            ConfigureCrystalFactoryButton();
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

            bool ignorePointerClick = skipPointerClick;
            skipPointerClick = false;

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
            BuildDefinition definition = selectedDefinition == null ? lookoutTower : selectedDefinition;
            BuildPlacementResult result = buildSystem.ValidatePlacement(
                definition,
                currentCellPosition);
            if (buildPreview != null)
            {
                buildPreview.Show(
                    definition,
                    buildSystem.CellToWorld(currentCellPosition, definition),
                    result.IsValid);
            }
            if (footprintPreview != null)
            {
                footprintPreview.Show(
                    currentCellPosition,
                    definition.Footprint,
                    result);
            }

            if (!ignorePointerClick &&
                Input.GetMouseButtonDown(0) &&
                !IsPointerOverUi() &&
                result.IsValid)
            {
                if (buildSystem.TryPlace(definition, currentCellPosition))
                {
                    CancelPlacement();
                }
            }
        }

        public void BeginLookoutTowerPlacement()
        {
            BeginPlacement(lookoutTower);
        }

        public void BeginCrystalFactoryPlacement()
        {
            BeginPlacement(crystalFactory);
        }

        private void BeginPlacement(BuildDefinition definition)
        {
            ResolveReferences();
            if (definition == null || buildSystem == null || buildPreview == null ||
                targetCamera == null)
            {
                Debug.LogWarning(
                    $"[{nameof(BuildInputController)}] Cannot start building: " +
                    "required references are missing.",
                    this);
                return;
            }

            if (buildSystem.ValidatePlacement(
                    definition,
                    buildSystem.WorldToCell(transform.position)).Reason ==
                BuildPlacementFailureReason.WrongPhase)
            {
                return;
            }

            selectedDefinition = definition;
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
                stageUIController.SetBuildMode(true);
            }
        }

        public void CancelPlacement()
        {
            isPlacing = false;
            skipPointerClick = false;
            selectedDefinition = null;
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

            stageUIController?.SetBuildMode(false);
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

        private void ConfigureLookoutTowerButton()
        {
            if (stageUIController == null || stageUIController.ConstructPanel == null)
            {
                return;
            }

            Transform buttonTransform = FindChildByName(
                stageUIController.ConstructPanel.transform,
                "BuidLookout");
            lookoutTowerButton = buttonTransform == null
                ? null
                : buttonTransform.GetComponent<Button>();
            if (lookoutTowerButton == null)
            {
                Debug.LogWarning(
                    $"[{nameof(BuildInputController)}] The authored BuidLookout button " +
                    "could not be found in the ConstructPanel.",
                    this);
                return;
            }

            lookoutTowerButton.onClick.RemoveListener(BeginLookoutTowerPlacement);
            lookoutTowerButton.onClick.AddListener(BeginLookoutTowerPlacement);
        }

        private void ConfigureCrystalFactoryButton()
        {
            if (crystalFactory == null || crystalFactoryButton == null &&
                (stageUIController == null || stageUIController.ConstructPanel == null))
            {
                return;
            }

            Transform buttonTransform = crystalFactoryButton == null
                ? FindChildByName(
                    stageUIController.ConstructPanel.transform,
                    "BuidFactory")
                : crystalFactoryButton.transform;
            if (buttonTransform == null)
            {
                Button[] buttons = FindObjectsOfType<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && buttons[i].name == "BuidFactory")
                    {
                        buttonTransform = buttons[i].transform;
                        break;
                    }
                }
            }
            if (buttonTransform == null)
            {
                Debug.LogWarning(
                    $"[{nameof(BuildInputController)}] The authored BuidFactory button " +
                    "could not be found in the ConstructPanel.",
                    this);
                return;
            }

            crystalFactoryButton = buttonTransform.GetComponent<Button>();
            if (crystalFactoryButton == null) return;
            crystalFactoryButton.onClick.RemoveListener(BeginLookoutTowerPlacement);
            crystalFactoryButton.onClick.RemoveListener(BeginCrystalFactoryPlacement);
            crystalFactoryButton.onClick.AddListener(BeginCrystalFactoryPlacement);
            Text label = crystalFactoryButton.GetComponentInChildren<Text>(true);
            if (label != null) label.text = $"{crystalFactory.DisplayName}\n{crystalFactory.CoinCost}";
            FindObjectOfType<StageAudioController>()?.RebindButtons();
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
                label.text = $"{lookoutTower.DisplayName}\n{lookoutTower.CoinCost}";
            }
        }

        private static Transform FindChildByName(Transform root, string objectName)
        {
            if (root == null) return null;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == objectName)
                {
                    return children[i];
                }
            }
            return null;
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
