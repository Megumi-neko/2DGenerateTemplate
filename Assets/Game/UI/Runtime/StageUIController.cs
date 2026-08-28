using System;
using Game.Building;
using Game.DayNight;
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
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject constructPanel;

        private int coinCount;
        private bool eventsSubscribed;

        public int CoinCount => coinCount;
        public GameObject ConstructPanel => constructPanel;
        public bool IsSettingsOpen => settingsPanel != null && settingsPanel.activeSelf;
        public bool IsConstructOpen => constructPanel != null && constructPanel.activeSelf;

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
            ConfigureButtonListeners();
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
            if (dayNightSystem == null || dayNightSystem.CurrentPhase != DayNightPhase.Day)
            {
                return;
            }

            dayNightSystem.EndDay();
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
                sunMoonButton.interactable = isDay;
            }

            if (constructButton != null)
            {
                constructButton.interactable = isDay;
                if (!isDay && IsConstructOpen)
                {
                    CloseConstruct();
                }
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
