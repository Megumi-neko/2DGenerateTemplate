using System.Globalization;
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
        private const int CanvasSortingOrder = 25;
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
            SetVisible(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            if (labelText.fontSize != fontSize)
            {
                labelText.fontSize = fontSize;
            }

            string intensityText = FormatIntensity(emitter.CurrentIntensity);
            if (labelText.text != intensityText)
            {
                labelText.text = intensityText;
            }

            Vector2 labelPosition = CalculatePosition(
                emitter.WorldPosition,
                emitter.Direction,
                innerCircle.InnerRadius,
                edgeInset);
            canvasTransform.position = new Vector3(
                labelPosition.x,
                labelPosition.y,
                emitter.transform.position.z);
            canvasTransform.rotation = targetCamera.transform.rotation;
            canvasTransform.localScale = Vector3.one * CalculateSize(
                initialSize,
                growthMultiplier,
                initialCameraPosition,
                targetCamera.transform.position,
                initialCameraForward,
                initialCameraUp);
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

            return labelCanvas != null && canvasTransform != null && labelText != null;
        }

        private void CreateLabel()
        {
            Transform existing = transform.Find(CanvasName);
            GameObject canvasObject = existing == null
                ? new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas))
                : existing.gameObject;
            if (existing == null)
            {
                canvasObject.transform.SetParent(transform, false);
            }

            labelCanvas = canvasObject.GetComponent<Canvas>();
            labelCanvas.renderMode = RenderMode.WorldSpace;
            labelCanvas.worldCamera = targetCamera;
            labelCanvas.overrideSorting = true;
            labelCanvas.sortingOrder = CanvasSortingOrder;
            canvasTransform = canvasObject.GetComponent<RectTransform>();
            canvasTransform.sizeDelta = new Vector2(128f, 48f);

            Transform textTransform = canvasTransform.Find("Intensity");
            GameObject textObject = textTransform == null
                ? new GameObject("Intensity", typeof(RectTransform), typeof(Text))
                : textTransform.gameObject;
            if (textTransform == null)
            {
                textObject.transform.SetParent(canvasTransform, false);
            }

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            labelText = textObject.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
