using Game.BaseSystem;
using Game.Building;
using Game.DayNight;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [AddComponentMenu("Game/UI/Stage Audio Controller")]
    [DisallowMultipleComponent]
    public sealed class StageAudioController : MonoBehaviour
    {
        private const string AudioResourceRoot = "AudioResource/";
        private const float BgmFadeDuration = 0.35f;

        [Header("References")]
        [SerializeField] private DayNightSystem dayNightSystem;
        [SerializeField] private StageUIController stageUIController;

        [Header("Background Music")]
        [SerializeField] private AudioClip dayMusic;
        [SerializeField] private AudioClip nightMusic;

        [Header("Sound Effects")]
        [SerializeField] private AudioClip buttonSound;
        [SerializeField] private AudioClip purchaseSound;
        [SerializeField] private AudioClip buildingPlacedSound;

        private AudioClip activeBgm;
        private bool eventsSubscribed;
        private bool buttonsBound;

        private void Awake()
        {
            ResolveReferences();
            LoadDefaultClips();
        }

        private void OnEnable()
        {
            ResolveReferences();
            LoadDefaultClips();
            BindButtons();
            SubscribeToEvents();
            PlayCurrentPhaseMusic(false);
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnsubscribeFromEvents();
            StopOwnedBgm();
        }

        private void ResolveReferences()
        {
            if (dayNightSystem == null)
            {
                dayNightSystem = FindObjectOfType<DayNightSystem>();
            }

            if (stageUIController == null)
            {
                stageUIController = FindObjectOfType<StageUIController>();
            }
        }

        private void LoadDefaultClips()
        {
            if (dayMusic == null)
            {
                dayMusic = Resources.Load<AudioClip>($"{AudioResourceRoot}白天音乐");
            }

            if (nightMusic == null)
            {
                nightMusic = Resources.Load<AudioClip>($"{AudioResourceRoot}夜晚音乐");
            }

            if (buttonSound == null)
            {
                buttonSound = Resources.Load<AudioClip>($"{AudioResourceRoot}按钮");
            }

            if (purchaseSound == null)
            {
                purchaseSound = Resources.Load<AudioClip>($"{AudioResourceRoot}购买");
            }

            if (buildingPlacedSound == null)
            {
                buildingPlacedSound = Resources.Load<AudioClip>($"{AudioResourceRoot}放置建筑");
            }
        }

        private void SubscribeToEvents()
        {
            if (eventsSubscribed)
            {
                return;
            }

            EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
            EventBus.Instance.Subscribe<BuildPlaced>(OnBuildPlaced);
            eventsSubscribed = true;
        }

        private void UnsubscribeFromEvents()
        {
            if (!eventsSubscribed)
            {
                return;
            }

            EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
            EventBus.Instance.UnSubscribe<BuildPlaced>(OnBuildPlaced);
            eventsSubscribed = false;
        }

        private void OnDayNightStateChanged(DayNightStateChanged state)
        {
            PlayBgm(state.Phase == DayNightPhase.Night ? nightMusic : dayMusic, true);
        }

        private void OnBuildPlaced(BuildPlaced placed)
        {
            PlaySfx(buildingPlacedSound);
        }

        private void PlayCurrentPhaseMusic(bool fade)
        {
            bool isNight = dayNightSystem != null &&
                dayNightSystem.CurrentPhase == DayNightPhase.Night;
            PlayBgm(isNight ? nightMusic : dayMusic, fade);
        }

        private void PlayBgm(AudioClip clip, bool fade)
        {
            if (clip == null || !AudioManager.HasInstance)
            {
                return;
            }

            activeBgm = clip;
            AudioManager.Instance.PlayBgm(clip, true, fade ? BgmFadeDuration : 0f);
        }

        private void StopOwnedBgm()
        {
            if (activeBgm != null && AudioManager.HasInstance &&
                AudioManager.Instance.CurrentBgm == activeBgm)
            {
                AudioManager.Instance.StopBgm(BgmFadeDuration);
            }

            activeBgm = null;
        }

        private void BindButtons()
        {
            if (buttonsBound || stageUIController == null)
            {
                return;
            }

            Button[] buttons = stageUIController.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                if (button.name == "IntensityUp" ||
                    button.name == "LengthUp" ||
                    button.name == "BuidLookout")
                {
                    AddSfxListener(button, purchaseSound);
                }
                else if (button.name == "Setting" ||
                    button.name == "Sun/Moon" ||
                    button.name == "Tool" ||
                    button.name == "Close" ||
                    button.name == "BackToMainMenu" ||
                    button.name == "Exit")
                {
                    AddSfxListener(button, buttonSound);
                }
            }

            buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!buttonsBound || stageUIController == null)
            {
                return;
            }

            Button[] buttons = stageUIController.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveListener(PlayButtonSound);
                button.onClick.RemoveListener(PlayPurchaseSound);
            }

            buttonsBound = false;
        }

        private void AddSfxListener(Button button, AudioClip clip)
        {
            button.onClick.RemoveListener(PlayButtonSound);
            button.onClick.RemoveListener(PlayPurchaseSound);
            button.onClick.AddListener(clip == purchaseSound
                ? PlayPurchaseSound
                : PlayButtonSound);
        }

        private void PlayButtonSound()
        {
            PlaySfx(buttonSound);
        }

        private void PlayPurchaseSound()
        {
            PlaySfx(purchaseSound);
        }

        private static void PlaySfx(AudioClip clip)
        {
            if (clip != null && AudioManager.HasInstance)
            {
                AudioManager.Instance.PlaySfx(clip);
            }
        }
    }
}
