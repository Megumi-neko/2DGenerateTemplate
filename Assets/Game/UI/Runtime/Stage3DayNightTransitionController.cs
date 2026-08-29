using DG.Tweening;
using Game.BaseSystem;
using Game.DayNight;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.UI
{
    /// <summary>
    /// Presents the authored day/night video followed by a short dusk or dawn curtain.
    /// </summary>
    [AddComponentMenu("Game/UI/Stage 3 Day Night Transition")]
    [DisallowMultipleComponent]
    public sealed class Stage3DayNightTransitionController : MonoBehaviour
    {
        private const float MinimumDuration = 0.05f;
        private const float DefaultVideoTimeout = 12f;

        [SerializeField] private DayNightSystem dayNightSystem;
        [SerializeField] private VideoClip dayTurnsIntoNightClip;
        [SerializeField] private VideoClip nightTurnsIntoDayClip;
        [SerializeField, Min(MinimumDuration)] private float transitionDuration = 1.8f;
        [SerializeField, Min(MinimumDuration)] private float videoTimeout = DefaultVideoTimeout;
        [SerializeField, Range(0f, 1f)] private float nightOverlayAlpha = 0.62f;
        [SerializeField] private Color duskColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color nightColor = Color.black;
        [SerializeField] private Color dawnColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private int sortingOrder = 32000;

        private Canvas transitionCanvas;
        private CanvasGroup transitionGroup;
        private RawImage transitionImage;
        private VideoPlayer videoPlayer;
        private Tween transitionTween;
        private VideoClip pendingVideoClip;
        private bool pendingTargetNight;
        private bool awaitingVideo;
        private float videoRequestTime;
        private bool subscribed;
        private bool hasAppliedState;
        private bool appliedNight;
        private float savedTimeScale = 1f;
        private bool ownsTimePause;
        private bool savedDayNightEnabled;
        private bool pausedDayNightSystem;

        private void Awake()
        {
            ResolveReferences();
            EnsureTransitionOverlay();
            ApplyImmediate(IsNight());
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureTransitionOverlay();
            if (!subscribed)
            {
                EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
                subscribed = true;
            }

            if (!hasAppliedState)
            {
                ApplyImmediate(IsNight());
            }
        }

        private void Update()
        {
            if (awaitingVideo &&
                Time.unscaledTime - videoRequestTime >= Mathf.Max(MinimumDuration, videoTimeout))
            {
                FinishVideoAndPlayAnimation();
            }
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
                subscribed = false;
            }

            CancelTransition();
        }

        private void OnDestroy()
        {
            CancelTransition();
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.frameReady -= OnVideoFrameReady;
                videoPlayer.loopPointReached -= OnVideoFinished;
                videoPlayer.errorReceived -= OnVideoError;
            }

            if (transitionCanvas != null)
            {
                DestroyRuntimeObject(transitionCanvas.gameObject);
                transitionCanvas = null;
                transitionGroup = null;
                transitionImage = null;
                videoPlayer = null;
            }
        }

        private void OnValidate()
        {
            transitionDuration = Mathf.Max(MinimumDuration, transitionDuration);
            videoTimeout = Mathf.Max(MinimumDuration, videoTimeout);
            nightOverlayAlpha = Mathf.Clamp01(nightOverlayAlpha);
            sortingOrder = Mathf.Max(0, sortingOrder);
        }

        private void OnDayNightStateChanged(DayNightStateChanged state)
        {
            bool targetNight = state.Phase == DayNightPhase.Night;
            if (hasAppliedState && appliedNight == targetNight && !awaitingVideo)
            {
                return;
            }

            BeginTransition(targetNight);
        }

        private void BeginTransition(bool targetNight)
        {
            EnsureTransitionOverlay();
            if (transitionGroup == null || transitionImage == null)
            {
                return;
            }

            CancelTransition();
            PauseGameTime();
            pendingTargetNight = targetNight;
            pendingVideoClip = targetNight
                ? dayTurnsIntoNightClip
                : nightTurnsIntoDayClip;
            transitionGroup.blocksRaycasts = true;
            transitionImage.raycastTarget = true;
            transitionImage.texture = Texture2D.whiteTexture;
            transitionGroup.alpha = 1f;
            transitionImage.color = Color.black;

            if (pendingVideoClip == null || videoPlayer == null)
            {
                FinishVideoAndPlayAnimation();
                return;
            }

            if (targetNight)
            {
                PlayGreenToBlueLeadIn();
                return;
            }

            PrepareVideo();
        }

        private void PlayGreenToBlueLeadIn()
        {
            KillTransitionTween();
            transitionImage.color = new Color(180f / 255f, 204f / 255f, 102f / 255f, 1f);
            transitionTween = DOTween.Sequence()
                .Append(TweenImageColor(
                    new Color(60f / 255f, 176f / 255f, 238f / 255f, 1f),
                    0.6f))
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    transitionTween = null;
                    PrepareVideo();
                });
        }

        private void PrepareVideo()
        {
            if (pendingVideoClip == null || videoPlayer == null)
            {
                FinishVideoAndPlayAnimation();
                return;
            }

            transitionImage.texture = Texture2D.whiteTexture;
            transitionImage.color = Color.black;
            awaitingVideo = true;
            videoRequestTime = Time.unscaledTime;
            videoPlayer.clip = pendingVideoClip;
            videoPlayer.sendFrameReadyEvents = true;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.Prepare();
        }

        private void OnVideoPrepared(VideoPlayer player)
        {
            if (!awaitingVideo || player != videoPlayer || player.clip != pendingVideoClip)
            {
                return;
            }

            player.Play();
        }

        private void OnVideoFrameReady(VideoPlayer player, long frameIndex)
        {
            if (!awaitingVideo || player != videoPlayer || player.clip != pendingVideoClip ||
                player.texture == null)
            {
                return;
            }

            transitionImage.texture = player.texture;
            transitionImage.color = Color.white;
            player.sendFrameReadyEvents = false;
        }

        private void OnVideoFinished(VideoPlayer player)
        {
            if (!awaitingVideo || player != videoPlayer || player.clip != pendingVideoClip)
            {
                return;
            }

            FinishVideoAndPlayAnimation();
        }

        private void OnVideoError(VideoPlayer player, string message)
        {
            if (!awaitingVideo || player != videoPlayer || player.clip != pendingVideoClip)
            {
                return;
            }

            Debug.LogWarning(
                $"[{nameof(Stage3DayNightTransitionController)}] Unable to play transition video: " +
                message,
                this);
            FinishVideoAndPlayAnimation();
        }

        private void FinishVideoAndPlayAnimation()
        {
            if (!awaitingVideo && transitionTween != null)
            {
                return;
            }

            awaitingVideo = false;
            if (videoPlayer != null)
            {
                videoPlayer.sendFrameReadyEvents = false;
                videoPlayer.Stop();
            }

            transitionImage.texture = Texture2D.whiteTexture;
            PlayTransitionAnimation(pendingTargetNight);
        }

        private void PlayTransitionAnimation(bool targetNight)
        {
            if (transitionGroup == null || transitionImage == null)
            {
                ReleaseGameTimePause();
                return;
            }

            KillTransitionTween();
            transitionGroup.alpha = 1f;
            transitionImage.color = targetNight ? duskColor : nightColor;

            float duration = Mathf.Max(MinimumDuration, transitionDuration);
            Sequence sequence = DOTween.Sequence();
            if (targetNight)
            {
                sequence.Append(TweenImageColor(nightColor, duration * 0.65f));
                sequence.Append(TweenOverlayAlpha(nightOverlayAlpha, duration * 0.35f));
            }
            else
            {
                sequence.Append(TweenImageColor(dawnColor, duration * 0.35f));
                sequence.Append(TweenImageColor(Color.white, duration * 0.3f));
                sequence.Append(TweenOverlayAlpha(0f, duration * 0.35f));
            }

            transitionTween = sequence
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    transitionTween = null;
                    transitionGroup.blocksRaycasts = false;
                    transitionImage.raycastTarget = false;
                    ReleaseGameTimePause();
                });
            appliedNight = targetNight;
            hasAppliedState = true;
        }

        private void ApplyImmediate(bool night)
        {
            if (transitionGroup == null || transitionImage == null)
            {
                return;
            }

            CancelTransition();
            transitionImage.texture = Texture2D.whiteTexture;
            transitionImage.color = night ? nightColor : Color.white;
            transitionGroup.alpha = night ? nightOverlayAlpha : 0f;
            transitionGroup.blocksRaycasts = false;
            transitionImage.raycastTarget = false;
            appliedNight = night;
            hasAppliedState = true;
        }

        private Tweener TweenImageColor(Color targetColor, float duration)
        {
            return DOTween.To(
                () => transitionImage.color,
                value => transitionImage.color = value,
                targetColor,
                duration);
        }

        private Tweener TweenOverlayAlpha(float targetAlpha, float duration)
        {
            return DOTween.To(
                () => transitionGroup.alpha,
                value => transitionGroup.alpha = value,
                targetAlpha,
                duration);
        }

        private bool IsNight()
        {
            return dayNightSystem != null &&
                dayNightSystem.CurrentPhase == DayNightPhase.Night;
        }

        private void ResolveReferences()
        {
            if (dayNightSystem == null)
            {
                dayNightSystem = FindObjectOfType<DayNightSystem>();
            }
        }

        private void EnsureTransitionOverlay()
        {
            if (transitionCanvas != null)
            {
                transitionCanvas.sortingOrder = sortingOrder;
                return;
            }

            GameObject canvasObject = new GameObject(
                "Stage3 Day Night Transition",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            transitionCanvas = canvasObject.GetComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.overrideSorting = true;
            transitionCanvas.sortingOrder = sortingOrder;
            transitionCanvas.pixelPerfect = false;

            transitionGroup = canvasObject.GetComponent<CanvasGroup>();
            transitionGroup.interactable = false;
            transitionGroup.blocksRaycasts = false;

            GameObject imageObject = new GameObject(
                "Transition Curtain",
                typeof(RectTransform),
                typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            transitionImage = imageObject.GetComponent<RawImage>();
            transitionImage.texture = Texture2D.whiteTexture;
            transitionImage.raycastTarget = false;

            videoPlayer = canvasObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.isLooping = false;
            videoPlayer.skipOnDrop = false;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.frameReady += OnVideoFrameReady;
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.errorReceived += OnVideoError;
        }

        private void PauseGameTime()
        {
            if (ownsTimePause)
            {
                return;
            }

            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (dayNightSystem != null && dayNightSystem.UseUnscaledTime && dayNightSystem.enabled)
            {
                savedDayNightEnabled = true;
                dayNightSystem.enabled = false;
                pausedDayNightSystem = true;
            }

            ownsTimePause = true;
        }

        private void ReleaseGameTimePause()
        {
            if (!ownsTimePause)
            {
                return;
            }

            Time.timeScale = savedTimeScale;
            if (pausedDayNightSystem && dayNightSystem != null)
            {
                dayNightSystem.enabled = savedDayNightEnabled;
                pausedDayNightSystem = false;
                savedDayNightEnabled = false;
            }

            ownsTimePause = false;
        }

        private void CancelTransition()
        {
            awaitingVideo = false;
            pendingVideoClip = null;
            if (videoPlayer != null)
            {
                videoPlayer.sendFrameReadyEvents = false;
                videoPlayer.Stop();
            }

            KillTransitionTween();
            ReleaseGameTimePause();
            if (transitionGroup != null)
            {
                transitionGroup.blocksRaycasts = false;
                if (transitionImage != null)
                {
                    transitionImage.raycastTarget = false;
                }
            }
        }

        private void KillTransitionTween()
        {
            if (transitionTween != null)
            {
                transitionTween.Kill();
                transitionTween = null;
            }
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
