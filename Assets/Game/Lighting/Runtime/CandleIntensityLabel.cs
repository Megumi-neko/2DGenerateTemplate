using System.Globalization;
using Game.BaseSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Candle Intensity Label")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class CandleIntensityLabel : MonoBehaviour
    {
        private const float MinimumSize = 0.001f;
        private const float ReferenceWorldSize = 0.01f;
        private const int CanvasSortingOrder = 210;
        private const string CanvasName = "Candle Intensity Label Canvas";
        private static readonly Color LabelColor = new Color(0.2f, 1f, 0.35f, 0.95f);

        [SerializeField] private StageLightingBootstrap stageLighting;
        [SerializeField, Min(MinimumSize)] private float initialSize = 0.01f;
        [SerializeField, Range(1, 200)] private int fontSize = 36;
        [SerializeField, Min(0f)] private float growthMultiplier = 0.12f;
        [SerializeField, Min(0f)] private float edgeInset = 0.25f;

        private Camera targetCamera;
        private LightEmitter2D emitter;
        private InnerCircleLight2D innerCircle;
        private Canvas labelCanvas;
        private RectTransform canvasTransform;
        private RectTransform labelRect;
        private Text labelText;
        private Vector3 initialCameraPosition;
        private Vector3 initialCameraForward;
        private Vector3 initialCameraUp;
        private bool hasCameraBaseline;

        public Canvas LabelCanvas => labelCanvas;
        public Text LabelText => labelText;
        public float InitialSize => initialSize;
        public float GrowthMultiplier => growthMultiplier;

        private void Start()
        {
            RefreshNow();
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (!TryInitialize())
            {
                SetVisible(false);
                return;
            }

            bool shouldShow = emitter.isActiveAndEnabled &&
                emitter.IsEmitting &&
                emitter.Shape == LightShape2D.Sector &&
                emitter.CurrentIntensity > 0f;
            if (!shouldShow)
            {
                SetVisible(false);
                return;
            }

            Vector2 labelPosition = CalculatePosition(
                emitter.WorldPosition,
                emitter.Direction,
                innerCircle.InnerRadius,
                edgeInset);
            Vector3 screenPosition = targetCamera.WorldToScreenPoint(new Vector3(
                labelPosition.x,
                labelPosition.y,
                emitter.transform.position.z));
            bool isVisible = screenPosition.z > 0f;
            Vector2 localPosition = default;
            if (isVisible)
            {
                isVisible = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasTransform,
                    screenPosition,
                    null,
                    out localPosition);
            }
            SetVisible(isVisible);
            if (!isVisible)
            {
                return;
            }

            labelRect.anchoredPosition = localPosition;
            labelRect.localScale = Vector3.one * CalculateOverlayScale(CalculateSize(
                initialSize,
                growthMultiplier,
                initialCameraPosition,
                targetCamera.transform.position,
                initialCameraForward,
                initialCameraUp));

            if (labelText.fontSize != fontSize)
            {
                labelText.fontSize = fontSize;
            }

            string intensityText = FormatIntensity(emitter.CurrentIntensity);
            if (labelText.text != intensityText)
            {
                labelText.text = intensityText;
            }
        }

        public static string FormatIntensity(float intensity)
        {
            return Mathf.Max(0f, intensity).ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static Vector2 CalculatePosition(
            Vector2 origin,
            Vector2 direction,
            float radius,
            float inset)
        {
            Vector2 normalizedDirection = LightGeometry2D.NormalizeDirection(
                direction,
                Vector2.right);
            float distance = Mathf.Max(0f, radius - Mathf.Max(0f, inset));
            return origin + normalizedDirection * distance;
        }

        public static float CalculateSize(
            float baseSize,
            float multiplier,
            Vector3 baseCameraPosition,
            Vector3 currentCameraPosition,
            Vector3 cameraForward,
            Vector3 cameraUp)
        {
            Vector3 displacement = currentCameraPosition - baseCameraPosition;
            Vector3 normalizedForward = cameraForward.sqrMagnitude > 0.000001f
                ? cameraForward.normalized
                : Vector3.forward;
            Vector3 normalizedUp = cameraUp.sqrMagnitude > 0.000001f
                ? cameraUp.normalized
                : Vector3.up;
            float rise = Mathf.Max(0f, Vector3.Dot(displacement, normalizedUp));
            float backAway = Mathf.Max(0f, -Vector3.Dot(displacement, normalizedForward));
            float travel = Mathf.Sqrt(rise * rise + backAway * backAway);
            return Mathf.Max(MinimumSize, baseSize) *
                (1f + Mathf.Max(0f, multiplier) * travel);
        }

        private static float CalculateOverlayScale(float worldSize)
        {
            return Mathf.Max(MinimumSize, worldSize) / ReferenceWorldSize;
        }

        private bool TryInitialize()
        {
            if (stageLighting == null)
            {
                stageLighting = GetComponent<StageLightingBootstrap>();
            }

            if (stageLighting != null)
            {
                if (targetCamera == null) targetCamera = stageLighting.TargetCamera;
                if (emitter == null) emitter = stageLighting.CandleEmitter;
                if (innerCircle == null) innerCircle = stageLighting.InnerCircle;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null || emitter == null || innerCircle == null)
            {
                return false;
            }

            if (labelCanvas == null)
            {
                CreateLabel();
            }

            if (!hasCameraBaseline)
            {
                initialCameraPosition = targetCamera.transform.position;
                initialCameraForward = targetCamera.transform.forward;
                initialCameraUp = targetCamera.transform.up;
                hasCameraBaseline = true;
            }

            return labelCanvas != null && canvasTransform != null && labelRect != null &&
                labelText != null;
        }

        private void CreateLabel()
        {
            Transform existing = transform.Find(CanvasName);
            GameObject canvasObject = existing == null
                ? new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas))
                : existing.gameObject;
            canvasObject.transform.SetParent(null, false);

            labelCanvas = canvasObject.GetComponent<Canvas>();
            labelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            labelCanvas.worldCamera = null;
            labelCanvas.overrideSorting = true;
            labelCanvas.sortingOrder = CanvasSortingOrder;
            labelCanvas.pixelPerfect = false;
            canvasTransform = canvasObject.GetComponent<RectTransform>();
            canvasTransform.anchorMin = Vector2.zero;
            canvasTransform.anchorMax = Vector2.one;
            canvasTransform.offsetMin = Vector2.zero;
            canvasTransform.offsetMax = Vector2.zero;

            Transform textTransform = canvasTransform.Find("Intensity");
            GameObject textObject = textTransform == null
                ? new GameObject("Intensity", typeof(RectTransform), typeof(Text))
                : textTransform.gameObject;
            if (textTransform == null)
            {
                textObject.transform.SetParent(canvasTransform, false);
            }

            labelRect = textObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(128f, 48f);
            labelRect.anchoredPosition = Vector2.zero;
            labelText = textObject.GetComponent<Text>();
            labelText.font = GameUiFont.Load();
            labelText.fontSize = fontSize;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = LabelColor;
            labelText.raycastTarget = false;
        }

        private void SetVisible(bool value)
        {
            if (labelCanvas != null)
            {
                labelCanvas.enabled = value;
            }
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (labelCanvas == null)
            {
                return;
            }

            GameObject canvasObject = labelCanvas.gameObject;
            labelCanvas = null;
            canvasTransform = null;
            labelRect = null;
            labelText = null;
            if (Application.isPlaying)
            {
                Destroy(canvasObject);
            }
            else
            {
                DestroyImmediate(canvasObject);
            }
        }

        private void OnValidate()
        {
            initialSize = Mathf.Max(MinimumSize, initialSize);
            fontSize = Mathf.Clamp(fontSize, 1, 200);
            growthMultiplier = Mathf.Max(0f, growthMultiplier);
            edgeInset = Mathf.Max(0f, edgeInset);
        }
    }
}
