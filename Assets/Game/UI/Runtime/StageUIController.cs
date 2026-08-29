using System;
using DG.Tweening;
using Game.BaseSystem;
using Game.Building;
using Game.Combat;
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
        [SerializeField] private Button intensityUpgradeButton;
        [SerializeField] private Button rangeUpgradeButton;
        [SerializeField] private Text intensityUpgradeText;
        [SerializeField] private Text rangeUpgradeText;
        [SerializeField] private Text intensityLevelText;
        [SerializeField] private Text rangeLevelText;
        [SerializeField] private StageLightingBootstrap stageLightingBootstrap;
        [SerializeField, Min(0)] private int baseIntensityUpgradeCost = 2;
        [SerializeField, Min(0)] private int baseRangeUpgradeCost = 2;

        [Header("Health")]
        [SerializeField] private Health mainTowerHealth;
        [SerializeField] private Image healthBarFill;
        [SerializeField] private Text healthPoint;
        [SerializeField] private SpriteRenderer mainTowerRenderer;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float damageFlashDuration = 0.12f;
        [SerializeField, Min(1)] private int damageFlashLoops = 2;
        [SerializeField, Min(0f)] private float healthBarTweenDuration = 0.25f;

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
        private Tween healthBarTween;
        private Tween damageFlashTween;
        private Color mainTowerBaseColor = Color.white;
        private DayNightPhase previousDayNightPhase;
        private bool hasPreviousDayNightPhase;

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
            ResolveUpgradeButtons();
            ResolveHealthUI();
            ConfigureButtonListeners();
            EnsureDayNightLightingController();
            CloseSettings();
            CloseConstruct();
            RefreshCoinCount(coinCount);
            RefreshDayNight(dayNightSystem == null ? DayNightPhase.Day : dayNightSystem.CurrentPhase,
                dayNightSystem == null ? 1 : dayNightSystem.CurrentDay);
            previousDayNightPhase = dayNightSystem == null ? DayNightPhase.Day : dayNightSystem.CurrentPhase;
            hasPreviousDayNightPhase = true;
            RefreshHealthUI(false);
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
            ResolveSettingsButtons();
            ResolveUpgradeButtons();
            ResolveHealthUI();
            ConfigureButtonListeners();
            SubscribeToEvents();
            if (coinInventory != null)
            {
                coinInventory.CoinsChanged += OnCoinsChanged;
            }
            SubscribeHealth();
            if (dayNightSystem != null)
            {
                RefreshDayNight(dayNightSystem.CurrentPhase, dayNightSystem.CurrentDay);
            }
            RefreshHealthUI(false);
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            if (coinInventory != null)
            {
                coinInventory.CoinsChanged -= OnCoinsChanged;
            }
            UnsubscribeHealth();
            healthBarTween?.Kill();
            healthBarTween = null;
            damageFlashTween?.Kill();
            damageFlashTween = null;
            if (mainTowerRenderer != null)
            {
                mainTowerRenderer.color = mainTowerBaseColor;
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
                coinText.text = $"{coinCount}";
            }

            CoinCountChanged?.Invoke(coinCount);
            RefreshUpgradeButtons();
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

        private void ResolveHealthUI()
        {
            if (mainTowerRenderer != null)
            {
                mainTowerBaseColor = mainTowerRenderer.color;
            }

            if (healthBarFill != null)
            {
                healthBarFill.type = Image.Type.Filled;
                healthBarFill.fillMethod = Image.FillMethod.Horizontal;
            }

            if (mainTowerHealth == null)
            {
                Debug.LogWarning($"[{nameof(StageUIController)}] Main Tower Health reference is not configured.", this);
            }
            if (healthBarFill == null || healthPoint == null)
            {
                Debug.LogWarning($"[{nameof(StageUIController)}] HealthBar fill or HealthPoint reference is not configured.", this);
            }
            if (mainTowerRenderer == null)
            {
                Debug.LogWarning($"[{nameof(StageUIController)}] Main Tower renderer reference is not configured.", this);
            }
        }

        private void SubscribeHealth()
        {
            ResolveHealthUI();
            if (mainTowerHealth != null)
            {
                mainTowerHealth.Changed += OnHealthChanged;
                mainTowerHealth.Damaged += OnHealthDamaged;
                mainTowerHealth.Died += OnHealthDied;
            }
        }

        private void UnsubscribeHealth()
        {
            if (mainTowerHealth == null) return;
            mainTowerHealth.Changed -= OnHealthChanged;
            mainTowerHealth.Damaged -= OnHealthDamaged;
            mainTowerHealth.Died -= OnHealthDied;
        }

        private void OnHealthChanged(Health _) { RefreshHealthUI(true); }

        private void OnHealthDamaged(Health _, float __)
        {
            RefreshHealthUI(true);
            PlayDamageFlash();
        }

        private void OnHealthDied(Health _) { RefreshHealthUI(true); }

        private void PlayDamageFlash()
        {
            if (mainTowerRenderer == null || damageFlashDuration <= 0f)
            {
                return;
            }

            damageFlashTween?.Kill();
            mainTowerRenderer.color = damageFlashColor;
            damageFlashTween = DOTween.Sequence()
                .Append(mainTowerRenderer.DOColor(mainTowerBaseColor, damageFlashDuration))
                .SetLoops(Mathf.Max(1, damageFlashLoops), LoopType.Yoyo)
                .OnComplete(() =>
                {
                    mainTowerRenderer.color = mainTowerBaseColor;
                    damageFlashTween = null;
                });
        }

        private void RefreshHealthUI(bool animate)
        {
            if (mainTowerHealth == null) return;
            float target = Mathf.Clamp01(mainTowerHealth.NormalizedHealth);
            if (healthPoint != null)
            {
                healthPoint.text = $"{Mathf.CeilToInt(mainTowerHealth.CurrentHealth)}/{Mathf.CeilToInt(mainTowerHealth.MaxHealth)}";
            }
            if (healthBarFill == null) return;
            healthBarTween?.Kill();
            if (!animate || healthBarTweenDuration <= 0f)
            {
                healthBarFill.fillAmount = target;
                return;
            }
            healthBarTween = DOTween.To(
                    () => healthBarFill.fillAmount,
                    value => healthBarFill.fillAmount = value,
                    target,
                    healthBarTweenDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => healthBarTween = null);
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
            bool enteredDay = hasPreviousDayNightPhase &&
                previousDayNightPhase == DayNightPhase.Night &&
                state.Phase == DayNightPhase.Day;
            if (enteredDay && mainTowerHealth != null)
            {
                mainTowerHealth.ResetHealth(mainTowerHealth.MaxHealth);
            }
            previousDayNightPhase = state.Phase;
            hasPreviousDayNightPhase = true;
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

            RefreshUpgradeButtons();
        }

private void ResolveUpgradeButtons()
        {
            if (stageLightingBootstrap == null)
            {
                stageLightingBootstrap = FindObjectOfType<StageLightingBootstrap>();
            }

            if (constructPanel == null)
            {
                return;
            }

            if (intensityUpgradeButton == null)
            {
                Transform button = constructPanel.transform.Find("IntensityUp");
                intensityUpgradeButton = button == null ? null : button.GetComponent<Button>();
            }

            if (rangeUpgradeButton == null)
            {
                Transform button = constructPanel.transform.Find("LengthUp");
                rangeUpgradeButton = button == null ? null : button.GetComponent<Button>();
            }

            if (intensityUpgradeText == null && intensityUpgradeButton != null)
            {
                intensityUpgradeText = intensityUpgradeButton.GetComponentInChildren<Text>(true);
            }

            if (rangeUpgradeText == null && rangeUpgradeButton != null)
            {
                rangeUpgradeText = rangeUpgradeButton.GetComponentInChildren<Text>(true);
            }

            if (intensityLevelText == null && intensityUpgradeButton != null)
            {
                intensityLevelText = FindChildText(intensityUpgradeButton.transform, "Level");
            }

            if (rangeLevelText == null && rangeUpgradeButton != null)
            {
                rangeLevelText = FindChildText(rangeUpgradeButton.transform, "Level");
            }
        }

        private void OnIntensityUpgradeClicked()
        {
            if (stageLightingBootstrap != null &&
                TrySpendUpgrade(baseIntensityUpgradeCost, stageLightingBootstrap.IntensityUpgradeLevel) &&
                stageLightingBootstrap.UpgradeIntensity())
            {
                RefreshUpgradeButtons();
            }
        }

        private void OnRangeUpgradeClicked()
        {
            if (stageLightingBootstrap != null &&
                TrySpendUpgrade(baseRangeUpgradeCost, stageLightingBootstrap.RangeUpgradeLevel) &&
                stageLightingBootstrap.UpgradeRange())
            {
                RefreshUpgradeButtons();
            }
        }

private bool TrySpendUpgrade(int baseCost, int level)
        {
            return coinInventory == null || coinInventory.TrySpend(CalculateUpgradeCost(baseCost, level));
        }

        private static int CalculateUpgradeCost(int baseCost, int level)
        {
            long cost = (long)Mathf.Max(0, baseCost) * (Mathf.Max(0, level) + 1);
            return cost > int.MaxValue ? int.MaxValue : (int)cost;
        }

private void RefreshUpgradeButtons()
        {
            if (stageLightingBootstrap == null)
            {
                return;
            }

            bool isDay = dayNightSystem == null || dayNightSystem.CurrentPhase == DayNightPhase.Day;
            bool canUpgradeIntensity = stageLightingBootstrap.IntensityUpgradeLevel <
                stageLightingBootstrap.MaximumIntensityUpgradeLevel;
            bool canUpgradeRange = stageLightingBootstrap.RangeUpgradeLevel <
                stageLightingBootstrap.MaximumRangeUpgradeLevel;
            int intensityCost = CalculateUpgradeCost(
                baseIntensityUpgradeCost,
                stageLightingBootstrap.IntensityUpgradeLevel);
            int rangeCost = CalculateUpgradeCost(
                baseRangeUpgradeCost,
                stageLightingBootstrap.RangeUpgradeLevel);

            SetUpgradeLabel(intensityUpgradeText, "质量升级：", intensityCost, canUpgradeIntensity);
            SetUpgradeLabel(rangeUpgradeText, "高度升级：", rangeCost, canUpgradeRange);
            SetLevelLabel(intensityLevelText, stageLightingBootstrap.IntensityUpgradeLevel);
            SetLevelLabel(rangeLevelText, stageLightingBootstrap.RangeUpgradeLevel);
            SetInteractable(intensityUpgradeButton, isDay && canUpgradeIntensity &&
                coinCount >= intensityCost);
            SetInteractable(rangeUpgradeButton, isDay && canUpgradeRange &&
                coinCount >= rangeCost);
        }

        private static void SetLevelLabel(Text label, int level)
        {
            if (label != null)
            {
                label.text = $"Lv：{Mathf.Max(0, level)}";
            }
        }

        private static Text FindChildText(Transform parent, string childName)
        {
            Transform child = parent == null ? null : parent.Find(childName);
            return child == null ? null : child.GetComponent<Text>();
        }

        private static void SetUpgradeLabel(Text label, string prefix, int cost, bool canUpgrade)
        {
            if (label != null)
            {
                label.text = canUpgrade ? $"{prefix}{cost}" : $"{prefix}已满级";
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

            if (intensityUpgradeButton != null)
            {
                intensityUpgradeButton.onClick.RemoveListener(OnIntensityUpgradeClicked);
                intensityUpgradeButton.onClick.AddListener(OnIntensityUpgradeClicked);
            }

            if (rangeUpgradeButton != null)
            {
                rangeUpgradeButton.onClick.RemoveListener(OnRangeUpgradeClicked);
                rangeUpgradeButton.onClick.AddListener(OnRangeUpgradeClicked);
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
