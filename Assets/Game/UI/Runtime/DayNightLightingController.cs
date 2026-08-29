using Game.BaseSystem;
using Game.DayNight;
using Game.Lighting;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Connects the day/night state to stage lighting and the daytime build range.
    /// It is installed by StageUIController so existing scenes need no manual wiring.
    /// </summary>
    [AddComponentMenu("Game/UI/Day Night Lighting Controller")]
    [DisallowMultipleComponent]
    public sealed class DayNightLightingController : MonoBehaviour
    {
        private const int CircleSegments = 96;
        private static readonly Color BuildRangeColor = new Color(0.25f, 0.85f, 1f, 0.7f);

        [SerializeField] private DayNightSystem dayNightSystem;
        [SerializeField] private StageLightingBootstrap lightingBootstrap;
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private float circleZ = -0.05f;

        private DarknessOverlayEffect overlay;
        private LineRenderer rangeRenderer;
        private Material rangeMaterial;
        private float nightDarknessOpacity;
        private bool hasCapturedOpacity;
        private bool subscribed;
        private bool stateApplied;
        private DayNightPhase appliedPhase;
        private LightEmitter2D appliedCandle;
        private LightEmitter2D rangeEmitter;
        private float rangeRadius = -1f;
        private Vector2 rangeCenter;

        private void Awake()
        {
            ResolveReferences();
            EnsureRangeRenderer();
            CaptureOverlayOpacity();
        }

        private void OnEnable()
        {
            if (!subscribed)
            {
                EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
                IlluminationSystem.SourcesChanged += OnSourcesChanged;
                subscribed = true;
            }

            ResolveReferences();
            ApplyCurrentState();
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
                IlluminationSystem.SourcesChanged -= OnSourcesChanged;
                subscribed = false;
            }

            SetRangeVisible(false);
        }

        private void OnDestroy()
        {
            if (rangeMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(rangeMaterial);
                }
                else
                {
                    DestroyImmediate(rangeMaterial);
                }
            }
        }

        private void Update()
        {
            ResolveReferences();
            DayNightPhase phase = dayNightSystem == null
                ? DayNightPhase.Day
                : dayNightSystem.CurrentPhase;
            LightEmitter2D currentCandle = lightingBootstrap == null
                ? null
                : lightingBootstrap.CandleEmitter;
            bool expectedEmitting = phase == DayNightPhase.Night;
            StageLightingCameraFramer currentFramer = lightingBootstrap == null
                ? null
                : lightingBootstrap.CameraFramer;
            bool framerNeedsReconciliation = currentFramer != null &&
                currentFramer.IsManualMode == expectedEmitting;
            if (!stateApplied || phase != appliedPhase || currentCandle != appliedCandle ||
                currentCandle != null && currentCandle.IsEmitting != expectedEmitting ||
                framerNeedsReconciliation)
            {
                ApplyState(phase);
            }
            else if (phase == DayNightPhase.Day)
            {
                RefreshRange();
            }
        }

        private void OnDayNightStateChanged(DayNightStateChanged state)
        {
            ApplyState(state.Phase);
        }

        private void OnSourcesChanged()
        {
            if (dayNightSystem == null || dayNightSystem.CurrentPhase != DayNightPhase.Day)
            {
                return;
            }

            rangeEmitter = null;
            rangeRadius = -1f;
            RefreshRange();
        }

        private void ApplyCurrentState()
        {
            ApplyState(dayNightSystem == null ? DayNightPhase.Day : dayNightSystem.CurrentPhase);
        }

        private void ApplyState(DayNightPhase phase)
        {
            bool isNight = phase == DayNightPhase.Night;
            if (lightingBootstrap != null && lightingBootstrap.CameraFramer != null)
            {
                StageLightingCameraFramer framer = lightingBootstrap.CameraFramer;
                framer.SetManualMode(!isNight);
                if (!isNight)
                {
                    framer.ResetToInitialPose();
                }
                else
                {
                    framer.ReframeImmediately();
                }
            }

            if (lightingBootstrap != null && lightingBootstrap.CandleEmitter != null)
            {
                LightEmitter2D candle = lightingBootstrap.CandleEmitter;
                if (appliedCandle != candle || candle.IsEmitting != isNight)
                {
                    candle.SetEmitting(isNight);
                    appliedCandle = candle;
                }
            }

            CaptureOverlayOpacity();
            if (overlay != null)
            {
                overlay.DarknessOpacity = isNight ? nightDarknessOpacity : 0f;
            }

            SetRangeVisible(!isNight && phase == DayNightPhase.Day);
            if (!isNight && phase == DayNightPhase.Day)
            {
                RefreshRange();
            }

            appliedPhase = phase;
            stateApplied = true;
        }

        private void RefreshRange()
        {
            LightEmitter2D emitter = IlluminationSystem.GetLongestSectorEmitter(true);
            if (emitter == null)
            {
                SetRangeVisible(false);
                return;
            }

            float radius = emitter.MaximumEffectiveRange;
            Vector2 center = emitter.WorldPosition;
            if (rangeEmitter != emitter || !Mathf.Approximately(rangeRadius, radius) ||
                (rangeCenter - center).sqrMagnitude > 0.0001f)
            {
                rangeEmitter = emitter;
                rangeRadius = radius;
                rangeCenter = center;
                RebuildRange(center, radius);
            }

            SetRangeVisible(radius > 0f);
        }

        private void RebuildRange(Vector2 center, float radius)
        {
            if (rangeRenderer == null)
            {
                return;
            }

            rangeRenderer.positionCount = CircleSegments;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / CircleSegments;
                rangeRenderer.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius,
                    circleZ));
            }
        }

        private void EnsureRangeRenderer()
        {
            if (rangeRenderer != null)
            {
                return;
            }

            Transform child = transform.Find("Daytime Build Range");
            GameObject rangeObject = child == null
                ? new GameObject("Daytime Build Range")
                : child.gameObject;
            if (child == null)
            {
                rangeObject.transform.SetParent(transform, false);
            }

            rangeRenderer = rangeObject.GetComponent<LineRenderer>();
            if (rangeRenderer == null)
            {
                rangeRenderer = rangeObject.AddComponent<LineRenderer>();
            }
            rangeRenderer.useWorldSpace = true;
            rangeRenderer.loop = true;
            rangeRenderer.positionCount = CircleSegments;
            rangeRenderer.widthMultiplier = Mathf.Max(0.001f, lineWidth);
            rangeRenderer.startColor = BuildRangeColor;
            rangeRenderer.endColor = BuildRangeColor;
            rangeRenderer.numCornerVertices = 2;
            rangeRenderer.numCapVertices = 2;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                rangeMaterial = new Material(shader)
                {
                    color = BuildRangeColor,
                    hideFlags = HideFlags.DontSave
                };
                rangeRenderer.sharedMaterial = rangeMaterial;
            }
        }

        private void ResolveReferences()
        {
            if (dayNightSystem == null)
            {
                dayNightSystem = FindObjectOfType<DayNightSystem>();
            }

            if (lightingBootstrap == null)
            {
                lightingBootstrap = FindObjectOfType<StageLightingBootstrap>();
            }

            if (overlay == null && lightingBootstrap != null && lightingBootstrap.TargetCamera != null)
            {
                overlay = lightingBootstrap.TargetCamera.GetComponent<DarknessOverlayEffect>();
            }

            if (overlay == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    overlay = mainCamera.GetComponent<DarknessOverlayEffect>();
                }
            }
        }

        private void CaptureOverlayOpacity()
        {
            if (lightingBootstrap != null)
            {
                nightDarknessOpacity = lightingBootstrap.DarknessOpacity;
                hasCapturedOpacity = true;
                return;
            }

            if (hasCapturedOpacity || overlay == null)
            {
                return;
            }

            nightDarknessOpacity = overlay.DarknessOpacity;
            hasCapturedOpacity = true;
        }

        private void SetRangeVisible(bool visible)
        {
            if (rangeRenderer != null)
            {
                rangeRenderer.enabled = visible;
            }
        }
    }
}
