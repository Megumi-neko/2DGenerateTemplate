using System;
using Game.BaseSystem;
using Game.Building;
using Game.DayNight;
using Game.Lighting;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public interface IStagePanel
    {
        bool IsOpen { get; }
        void Open();
        void Close();
    }

    [AddComponentMenu("Game/UI/Stage UI Controller")]
    [DisallowMultipleComponent]
    public sealed class StageUIController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DayNightSystem dayNightSystem;
        [SerializeField] private CoinInventory coinInventory;
        [SerializeField] private int initialCoinCount;
        [SerializeField] private Text coinText;
        [SerializeField] private Text dayText;

        [Header("Day / Night")]
        [SerializeField] private Button sunMoonButton;
        [SerializeField] private Image sunMoonIcon;
        [SerializeField] private Sprite sunSprite;
        [SerializeField] private Sprite moonSprite;

        [Header("Panels")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button constructButton;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button constructCloseButton;
        [SerializeField] private Button backToMainMenuButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject constructPanel;

        [Header("Scene Flow")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private int coinCount;
        private bool eventsSubscribed;
        private bool isBuildMode;
        private bool buildModeStateCaptured;
        private bool savedSunMoonInteractable;
        private bool savedSettingsInteractable;
        private bool savedConstructInteractable;
        private bool savedSettingsCloseInteractable;
        private bool savedConstructCloseInteractable;
        private bool savedBackToMainMenuInteractable;
        private bool savedExitInteractable;

        public int CoinCount => coinCount;
        public GameObject ConstructPanel => constructPanel;
        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.activeSelf;
        public bool IsConstructOpen => constructPanel != null && constructPanel.activeSelf;
        public bool IsBuildMode => isBuildMode;

        public event Action<int> CoinCountChanged;
        public event Action<bool> SettingsVisibilityChanged;
        public event Action<bool> ConstructVisibilityChanged;

        private void Awake()
        {
            if (coinInventory == null)
            {
                coinInventory = FindObjectOfType<CoinInventory>();
            }

            coinCount = coinInventory == null
                ? Mathf.Max(0, initialCoinCount)
                : coinInventory.Coins;
            ResolveSettingsButtons();
            ConfigureButtonListeners();
            EnsureDayNightLightingController();
            CloseSettings();
            CloseConstruct();
            RefreshCoinCount(coinCount);
            RefreshDayNight(dayNightSystem == null ? DayNightPhase.Day : dayNightSystem.CurrentPhase,
                dayNightSystem == null ? 1 : dayNightSystem.CurrentDay);
        }

        private void Start()
        {
            if (coinInventory != null)
            {
                RefreshCoinCount(coinInventory.Coins);
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
            if (coinInventory != null)
            {
                coinInventory.CoinsChanged += OnCoinsChanged;
            }
            if (dayNightSystem != null)
            {
                RefreshDayNight(dayNightSystem.CurrentPhase, dayNightSystem.CurrentDay);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            if (coinInventory != null)
            {
                coinInventory.CoinsChanged -= OnCoinsChanged;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseSettings();
                CloseConstruct();
            }
        }

        public void OnSunMoonClicked()
        {
            if (isBuildMode || dayNightSystem == null ||
                dayNightSystem.CurrentPhase != DayNightPhase.Day)
            {
                return;
            }

            dayNightSystem.EndDay();
        }

        public void SetBuildMode(bool enabled)
        {
            if (enabled == isBuildMode)
            {
                return;
            }

            if (enabled)
            {
                CaptureBuildModeState();
                isBuildMode = true;
                SetInteractable(sunMoonButton, false);
                SetInteractable(settingsButton, false);
                SetInteractable(constructButton, false);
                SetInteractable(settingsCloseButton, false);
                SetInteractable(constructCloseButton, false);
                SetInteractable(backToMainMenuButton, false);
                SetInteractable(exitButton, false);
                CloseSettings();
                CloseConstruct();
                return;
            }

            isBuildMode = false;
            RestoreBuildModeState();
            if (dayNightSystem != null &&
                dayNightSystem.CurrentPhase != DayNightPhase.Day)
            {
                RefreshDayNight(dayNightSystem.CurrentPhase, dayNightSystem.CurrentDay);
            }
        }

        private void CaptureBuildModeState()
        {
            if (buildModeStateCaptured)
            {
                return;
            }

            savedSunMoonInteractable = GetInteractable(sunMoonButton);
            savedSettingsInteractable = GetInteractable(settingsButton);
            savedConstructInteractable = GetInteractable(constructButton);
            savedSettingsCloseInteractable = GetInteractable(settingsCloseButton);
            savedConstructCloseInteractable = GetInteractable(constructCloseButton);
            savedBackToMainMenuInteractable = GetInteractable(backToMainMenuButton);
            savedExitInteractable = GetInteractable(exitButton);
            buildModeStateCaptured = true;
        }

        private void RestoreBuildModeState()
        {
            if (!buildModeStateCaptured)
            {
                return;
            }

            SetInteractable(sunMoonButton, savedSunMoonInteractable);
            SetInteractable(settingsButton, savedSettingsInteractable);
            SetInteractable(constructButton, savedConstructInteractable);
            SetInteractable(settingsCloseButton, savedSettingsCloseInteractable);
            SetInteractable(constructCloseButton, savedConstructCloseInteractable);
            SetInteractable(backToMainMenuButton, savedBackToMainMenuInteractable);
            SetInteractable(exitButton, savedExitInteractable);
            buildModeStateCaptured = false;
        }

        private static bool GetInteractable(Button button)
        {
            return button != null && button.interactable;
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        public void SetCoinCount(int count)
        {
            if (coinInventory != null)
            {
                coinInventory.SetCoins(count);
                return;
            }

            RefreshCoinCount(count);
        }

        public void RefreshCoinCount(int count)
        {
            coinCount = Mathf.Max(0, count);
            if (coinText != null)
            {
                coinText.text = $"影结晶：{coinCount}";
            }

            CoinCountChanged?.Invoke(coinCount);
        }

        public void OpenSettings()
        {
            if (isBuildMode)
            {
                return;
            }

            SetPanelVisibility(settingsPanel, true);
            SetPanelVisibility(constructPanel, false);
            SettingsVisibilityChanged?.Invoke(true);
            ConstructVisibilityChanged?.Invoke(false);
        }

        public void CloseSettings()
        {
            SetPanelVisibility(settingsPanel, false);
            if (settingsPanel != null)
            {
                SettingsVisibilityChanged?.Invoke(false);
            }
        }

        public void ToggleSettings()
        {
            if (isBuildMode)
            {
                return;
            }

            if (IsSettingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        public void OpenConstruct()
        {
            if (isBuildMode)
            {
                return;
            }

            SetPanelVisibility(constructPanel, true);
            SetPanelVisibility(settingsPanel, false);
            ConstructVisibilityChanged?.Invoke(true);
            SettingsVisibilityChanged?.Invoke(false);
        }

        public void CloseConstruct()
        {
            SetPanelVisibility(constructPanel, false);
            if (constructPanel != null)
            {
                ConstructVisibilityChanged?.Invoke(false);
            }
        }

        public void ToggleConstruct()
        {
            if (isBuildMode)
            {
                return;
            }

            if (IsConstructOpen)
            {
                CloseConstruct();
            }
            else
            {
                OpenConstruct();
            }
        }

        private void SubscribeToEvents()
        {
            if (eventsSubscribed)
            {
                return;
            }

            EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
            EventBus.Instance.Subscribe<DayNightCompleted>(OnDayNightCompleted);
            eventsSubscribed = true;
        }

        private void UnsubscribeFromEvents()
        {
            if (!eventsSubscribed)
            {
                return;
            }

            EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
            EventBus.Instance.UnSubscribe<DayNightCompleted>(OnDayNightCompleted);
            eventsSubscribed = false;
        }

        private void OnCoinsChanged(int count)
        {
            RefreshCoinCount(count);
        }

        private void OnDayNightStateChanged(DayNightStateChanged state)
        {
            RefreshDayNight(state.Phase, state.Day);
        }

        private void OnDayNightCompleted(DayNightCompleted completed)
        {
            if (dayText != null)
            {
                dayText.text = $"第 {completed.Day} 天";
            }
        }

        private void RefreshDayNight(DayNightPhase phase, int day)
        {
            if (dayText != null)
            {
                dayText.text = $"第 {day} 天";
            }

            bool isDay = phase == DayNightPhase.Day;
            if (sunMoonIcon != null)
            {
                sunMoonIcon.sprite = isDay ? sunSprite : moonSprite;
            }

            if (sunMoonButton != null)
            {
                sunMoonButton.interactable = isDay && !isBuildMode;
            }

            if (constructButton != null)
            {
                bool wasConstructOpen = IsConstructOpen;
                constructButton.interactable = isDay && !isBuildMode;
                constructButton.gameObject.SetActive(isDay);
                if (!isDay && wasConstructOpen)
                {
                    CloseConstruct();
                }
            }
        }

        private void EnsureDayNightLightingController()
        {
            DayNightLightingController controller = FindObjectOfType<DayNightLightingController>();
            if (controller == null)
            {
                StageLightingBootstrap bootstrap = FindObjectOfType<StageLightingBootstrap>();
                GameObject host = bootstrap == null ? gameObject : bootstrap.gameObject;
                host.AddComponent<DayNightLightingController>();
            }
        }

        private void ConfigureButtonListeners()
        {
            if (sunMoonButton != null)
            {
                sunMoonButton.onClick.RemoveListener(OnSunMoonClicked);
                sunMoonButton.onClick.AddListener(OnSunMoonClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(ToggleSettings);
                settingsButton.onClick.AddListener(ToggleSettings);
            }

            if (constructButton != null)
            {
                constructButton.onClick.RemoveListener(ToggleConstruct);
                constructButton.onClick.AddListener(ToggleConstruct);
            }

            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.RemoveListener(CloseSettings);
                settingsCloseButton.onClick.AddListener(CloseSettings);
            }

            if (constructCloseButton != null)
            {
                constructCloseButton.onClick.RemoveListener(CloseConstruct);
                constructCloseButton.onClick.AddListener(CloseConstruct);
            }

            if (backToMainMenuButton != null)
            {
                backToMainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
                backToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(QuitGame);
                exitButton.onClick.AddListener(QuitGame);
            }
        }

        private void ResolveSettingsButtons()
        {
            if (settingsPanel == null)
            {
                return;
            }

            if (backToMainMenuButton == null)
            {
                Transform button = settingsPanel.transform.Find("BackToMainMenu");
                if (button != null)
                {
                    backToMainMenuButton = button.GetComponent<Button>();
                }
            }

            if (exitButton == null)
            {
                Transform button = settingsPanel.transform.Find("Exit");
                if (button != null)
                {
                    exitButton = button.GetComponent<Button>();
                }
            }
        }

        public void ReturnToMainMenu()
        {
            if (string.IsNullOrWhiteSpace(mainMenuSceneName) || !SceneManagerSystem.HasInstance)
            {
                Debug.LogWarning("Cannot return to the main menu: SceneManagerSystem is unavailable or the scene name is empty.");
                return;
            }

            if (SceneManagerSystem.Instance.LoadScene(mainMenuSceneName) == SceneLoadRequestStatus.Accepted)
            {
                CloseSettings();
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void SetPanelVisibility(GameObject panel, bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }
    }
}
