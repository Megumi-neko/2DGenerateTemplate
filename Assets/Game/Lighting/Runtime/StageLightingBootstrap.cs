using System.Collections.Generic;
using Game.DayNight;
using UnityEngine;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Stage Lighting Bootstrap")]
    [DisallowMultipleComponent]
    public sealed class StageLightingBootstrap : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float gameplayPlaneZ;
        [SerializeField, Range(0f, 1f)] private float darknessOpacity = 0.96f;

        [Header("Central Candle")]
        [Tooltip("Optional authored candle already placed in the scene. When assigned, no candle object or visual is generated at runtime.")]
        [SerializeField] private GameObject centralCandle;
        [Tooltip("Legacy prefab fallback used only by scenes that have not yet been migrated to an authored candle.")]
        [SerializeField] private GameObject candlePrefab;
        [SerializeField] private LightShape2D initialShape = LightShape2D.Sector;
        [SerializeField] private Vector2 candleWorldPosition;
        [SerializeField] private float candleWorldZ = -0.15f;
        [SerializeField, Min(0.01f)] private float baseRadius = 5f;
        [SerializeField, Range(1f, LightGeometry2D.FullCircleAngle)] private float sectorAngle = 90f;
        [SerializeField, Range(LightGeometry2D.DefaultMinimumSectorAngle, LightGeometry2D.FullCircleAngle)] private float minimumSectorAngle = LightGeometry2D.DefaultMinimumSectorAngle;
        [SerializeField, Min(0f)] private float baseIntensity = 1f;
        [SerializeField, Min(0f)] private float baseDamagePerSecond = 12f;
        [SerializeField, Min(1f)] private float maximumFocusMultiplier = 2.25f;
        [SerializeField, Min(0f)] private float edgeSoftness = 0.35f;
        [SerializeField, Range(0.01f, 0.99f)] private float innerRadiusMultiplier = 0.5f;

        [Header("Candle Upgrades")]
        [SerializeField, Min(0)] private int maximumRangeUpgradeLevel = 10;
        [SerializeField, Min(0f)] private float rangeUpgradeAmount = 0.25f;
        [SerializeField, Min(0)] private int maximumIntensityUpgradeLevel = 10;
        [SerializeField, Min(0f)] private float intensityUpgradeAmount = 0.075f;
        [SerializeField, Min(0f)] private float damageUpgradeAmount = 0.9f;
        [SerializeField] private KeyCode rangeUpgradeKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode intensityUpgradeKey = KeyCode.Alpha2;

        [Header("Candle Visual")]
        [Tooltip("Optional visual-only prefab. It takes priority over the Sprite fields.")]
        [SerializeField] private GameObject candleVisualPrefab;
        [Tooltip("Optional body sprite. Used when Candle Visual Prefab is empty.")]
        [SerializeField] private Sprite candleSprite;
        [Tooltip("Optional flame sprite. No flame is generated when this is empty.")]
        [SerializeField] private Sprite flameSprite;
        [SerializeField] private Vector3 candleVisualLocalPosition = new Vector3(0f, 0f, -0.06f);
        [SerializeField] private Vector3 candleVisualLocalEulerAngles;
        [SerializeField] private Vector3 candleVisualLocalScale = Vector3.one;
        [SerializeField] private int visualSortingOrder = 10;

        private readonly List<Material> runtimeMaterials = new List<Material>();
        private GameObject createdCandle;
        private GameObject candleVisualRoot;
        private Transform candleScaleTarget;
        private Vector3 candleBaseVisualScale;
        private float candleBaseRadius;
        private float candleBaseIntensity;
        private bool hasCapturedCandleScale;
        private bool usesAuthoredCandle;
        private LightEmitter2D candleEmitter;
        private StageLightingCameraFramer cameraFramer;
        private int rangeUpgradeLevel;
        private int intensityUpgradeLevel;

        public Camera TargetCamera => targetCamera;
        public float DarknessOpacity => darknessOpacity;
        public GameObject CentralCandle => centralCandle;
        public LightEmitter2D CandleEmitter => candleEmitter;
        public InnerCircleLight2D InnerCircle => candleEmitter == null
            ? null
            : candleEmitter.GetComponent<InnerCircleLight2D>();
        public StageLightingCameraFramer CameraFramer => cameraFramer;
        public int RangeUpgradeLevel => rangeUpgradeLevel;
        public int IntensityUpgradeLevel => intensityUpgradeLevel;
        public int MaximumRangeUpgradeLevel => maximumRangeUpgradeLevel;
        public int MaximumIntensityUpgradeLevel => maximumIntensityUpgradeLevel;
        private void Awake()
        {
            EnsureCameraAndOverlay();
            EnsureCandle();
            EnsureCameraFramer();
        }



        public bool UpgradeRange()
        {
            if (candleEmitter == null || rangeUpgradeLevel >= maximumRangeUpgradeLevel)
            {
                return false;
            }

            candleEmitter.BaseRadius += rangeUpgradeAmount;
            rangeUpgradeLevel++;
            RefreshCandleScale();
            cameraFramer?.ReframeImmediately();
            return true;
        }

        public bool UpgradeIntensity()
        {
            if (candleEmitter == null || intensityUpgradeLevel >= maximumIntensityUpgradeLevel)
            {
                return false;
            }

            candleEmitter.BaseIntensity += intensityUpgradeAmount;
            candleEmitter.BaseDamagePerSecond += damageUpgradeAmount;
            intensityUpgradeLevel++;
            RefreshCandleScale();
            cameraFramer?.ReframeImmediately();
            return true;
        }

        private void EnsureCameraFramer()
        {
            if (targetCamera == null || candleEmitter == null)
            {
                return;
            }

            cameraFramer = targetCamera.GetComponent<StageLightingCameraFramer>();
            if (cameraFramer == null)
            {
                cameraFramer = targetCamera.gameObject.AddComponent<StageLightingCameraFramer>();
            }

            cameraFramer.Initialize(targetCamera, candleEmitter, gameplayPlaneZ);
        }





        private void OnValidate()
        {
            if (!System.Enum.IsDefined(typeof(LightShape2D), initialShape))
            {
                initialShape = LightShape2D.Sector;
            }

            baseRadius = Mathf.Max(0.01f, baseRadius);
            innerRadiusMultiplier = Mathf.Clamp(innerRadiusMultiplier, 0.01f, 0.99f);
            maximumRangeUpgradeLevel = Mathf.Max(0, maximumRangeUpgradeLevel);
            rangeUpgradeAmount = Mathf.Max(0f, rangeUpgradeAmount);
            maximumIntensityUpgradeLevel = Mathf.Max(0, maximumIntensityUpgradeLevel);
            intensityUpgradeAmount = Mathf.Max(0f, intensityUpgradeAmount);
            damageUpgradeAmount = Mathf.Max(0f, damageUpgradeAmount);
        }

        private void OnDestroy()
        {
            if (candleVisualRoot != null)
            {
                DestroyRuntimeObject(candleVisualRoot);
                candleVisualRoot = null;
            }

            if (createdCandle != null)
            {
                DestroyRuntimeObject(createdCandle);
                createdCandle = null;
            }

            foreach (Material material in runtimeMaterials)
            {
                if (material != null)
                {
                    DestroyRuntimeObject(material);
                }
            }

            runtimeMaterials.Clear();
        }

        private void EnsureCameraAndOverlay()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                Debug.LogWarning(
                    $"[{nameof(StageLightingBootstrap)}] No target camera found; lighting overlay is disabled.",
                    this);
                return;
            }

            DarknessOverlayEffect overlay = targetCamera.GetComponent<DarknessOverlayEffect>();
            if (overlay == null)
            {
                overlay = targetCamera.gameObject.AddComponent<DarknessOverlayEffect>();
            }

            overlay.GameplayPlaneZ = gameplayPlaneZ;
            overlay.DarknessOpacity = darknessOpacity;
        }

        private void EnsureCandle()
        {
            GameObject existingCandle = centralCandle;
            bool isAuthoredCandle = existingCandle != null;
            if (existingCandle == null)
            {
                existingCandle = FindChildByName("Stage Central Candle");
                if (existingCandle == null)
                {
                    existingCandle = FindChildByName("Stage 1 Central Candle");
                }

                isAuthoredCandle = existingCandle != null;
                if (existingCandle == null && candlePrefab != null)
                {
                    createdCandle = Instantiate(candlePrefab, transform, false);
                    createdCandle.name = "Stage Central Candle";
                    existingCandle = createdCandle;
                }
            }

            if (existingCandle == null)
            {
                Debug.LogError(
                    $"[{nameof(StageLightingBootstrap)}] Assign an authored central candle " +
                    "or a legacy Candle Prefab.",
                    this);
                return;
            }

            if (isAuthoredCandle && existingCandle.scene != gameObject.scene)
            {
                Debug.LogError(
                    $"[{nameof(StageLightingBootstrap)}] Central Candle must be a scene object, " +
                    "not a prefab asset.",
                    this);
                return;
            }

            existingCandle.transform.position = new Vector3(
                candleWorldPosition.x,
                candleWorldPosition.y,
                candleWorldZ);

            candleEmitter = existingCandle.GetComponent<LightEmitter2D>();
            if (candleEmitter == null)
            {
                if (isAuthoredCandle)
                {
                    Debug.LogError(
                        $"[{nameof(StageLightingBootstrap)}] The authored Central Candle " +
                        "must contain a LightEmitter2D component.",
                        existingCandle);
                    return;
                }

                candleEmitter = existingCandle.AddComponent<LightEmitter2D>();
            }

            candleEmitter.Shape = initialShape;
            candleEmitter.BaseRadius = baseRadius;
            candleEmitter.MinimumSectorAngle = minimumSectorAngle;
            candleEmitter.SectorAngle = sectorAngle;
            candleEmitter.BaseIntensity = baseIntensity;
            candleEmitter.BaseDamagePerSecond = baseDamagePerSecond;
            candleEmitter.MaximumFocusMultiplier = maximumFocusMultiplier;
            candleEmitter.EdgeSoftness = edgeSoftness;
            candleEmitter.Direction = Vector2.right;
            DayNightSystem currentDayNightSystem = FindObjectOfType<DayNightSystem>();
            bool shouldEmit = currentDayNightSystem == null ||
                currentDayNightSystem.CurrentPhase == DayNightPhase.Night;
            candleEmitter.SetEmitting(shouldEmit);

            InnerCircleLight2D innerCircle = existingCandle.GetComponent<InnerCircleLight2D>();
            if (innerCircle == null)
            {
                if (isAuthoredCandle)
                {
                    Debug.LogError(
                        $"[{nameof(StageLightingBootstrap)}] The authored Central Candle " +
                        "must contain an InnerCircleLight2D component.",
                        existingCandle);
                    return;
                }

                innerCircle = existingCandle.AddComponent<InnerCircleLight2D>();
            }

            innerCircle.RadiusMultiplier = innerRadiusMultiplier;

            CandleFocusController focusController =
                existingCandle.GetComponent<CandleFocusController>();
            if (focusController == null)
            {
                if (isAuthoredCandle)
                {
                    Debug.LogError(
                        $"[{nameof(StageLightingBootstrap)}] The authored Central Candle " +
                        "must contain a CandleFocusController component.",
                        existingCandle);
                    return;
                }

                focusController = existingCandle.AddComponent<CandleFocusController>();
            }

            focusController.Initialize(targetCamera, candleEmitter);
            usesAuthoredCandle = isAuthoredCandle;
            CaptureCandleScale(existingCandle.transform);
            RefreshCandleScale();

            if (!isAuthoredCandle)
            {
                CreateCandleVisual(candleEmitter.transform);
            }
        }

        private void CaptureCandleScale(Transform candleTransform)
        {
            if (hasCapturedCandleScale)
            {
                return;
            }

            Transform visual = candleTransform.Find("Visual");
            candleScaleTarget = visual == null ? candleTransform : visual;
            candleBaseVisualScale = candleScaleTarget.localScale;
            candleBaseRadius = Mathf.Max(0.01f, candleEmitter.BaseRadius);
            candleBaseIntensity = Mathf.Max(0.01f, candleEmitter.BaseIntensity);
            hasCapturedCandleScale = true;
        }

        private void RefreshCandleScale()
        {
            if (!hasCapturedCandleScale || candleScaleTarget == null || candleEmitter == null)
            {
                return;
            }

            float heightMultiplier = Mathf.Max(0.01f, candleEmitter.BaseRadius / candleBaseRadius);
            float widthMultiplier = Mathf.Max(0.01f, candleEmitter.BaseIntensity / candleBaseIntensity);
            candleScaleTarget.localScale = new Vector3(
                candleBaseVisualScale.x * widthMultiplier,
                candleBaseVisualScale.y * heightMultiplier,
                candleBaseVisualScale.z);
        }

        public void RefreshCandleVisual()
        {
            if (candleEmitter != null && !usesAuthoredCandle)
            {
                CreateCandleVisual(candleEmitter.transform);
            }
        }

        public void SetCandleVisual(
            GameObject visualPrefab,
            Sprite bodySprite,
            Sprite fireSprite)
        {
            candleVisualPrefab = visualPrefab;
            candleSprite = bodySprite;
            flameSprite = fireSprite;
            RefreshCandleVisual();
        }

        private void CreateCandleVisual(Transform candleTransform)
        {
            if (candleVisualRoot != null)
            {
                DestroyRuntimeObject(candleVisualRoot);
                candleVisualRoot = null;
            }

            if (candleVisualPrefab != null)
            {
                candleVisualRoot = Instantiate(candleVisualPrefab, candleTransform, false);
                candleVisualRoot.name = "Stage 1 Central Candle Visual";
                ConfigureVisualTransform(candleVisualRoot.transform);
                ApplySortingOrder(candleVisualRoot);
                return;
            }

            if (candleSprite != null || flameSprite != null)
            {
                candleVisualRoot = new GameObject("Stage 1 Central Candle Visual");
                candleVisualRoot.transform.SetParent(candleTransform, false);
                ConfigureVisualTransform(candleVisualRoot.transform);

                if (candleSprite != null)
                {
                    CreateSpriteRenderer(
                        "Candle Body",
                        candleSprite,
                        candleVisualRoot.transform,
                        Vector3.zero,
                        Vector3.one,
                        visualSortingOrder);
                }

                if (flameSprite != null)
                {
                    CreateSpriteRenderer(
                        "Candle Flame",
                        flameSprite,
                        candleVisualRoot.transform,
                        new Vector3(0f, 0.55f, -0.01f),
                        Vector3.one,
                        visualSortingOrder + 1);
                }

                return;
            }

            CreateFallbackCandleVisual(candleTransform);
        }

        private void ConfigureVisualTransform(Transform visualTransform)
        {
            visualTransform.localPosition = candleVisualLocalPosition;
            visualTransform.localEulerAngles = candleVisualLocalEulerAngles;
            visualTransform.localScale = candleVisualLocalScale;
        }

        private void CreateFallbackCandleVisual(Transform candleTransform)
        {
            Material bodyMaterial = CreateMaterial(new Color(1f, 0.58f, 0.12f, 1f));
            Material flameMaterial = CreateMaterial(new Color(1f, 0.9f, 0.28f, 1f));

            candleVisualRoot = new GameObject("Stage 1 Central Candle Visual");
            candleVisualRoot.transform.SetParent(candleTransform, false);
            ConfigureVisualTransform(candleVisualRoot.transform);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Quad);
            body.name = "Candle Body";
            body.transform.SetParent(candleVisualRoot.transform, false);
            body.transform.localScale = new Vector3(0.7f, 0.85f, 1f);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
            RemoveCollider(body);

            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flame.name = "Candle Flame";
            flame.transform.SetParent(candleVisualRoot.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.56f, -0.01f);
            flame.transform.localScale = new Vector3(0.38f, 0.62f, 1f);
            flame.GetComponent<MeshRenderer>().sharedMaterial = flameMaterial;
            RemoveCollider(flame);
        }

        private static SpriteRenderer CreateSpriteRenderer(
            string objectName,
            Sprite sprite,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            int sortingOrder)
        {
            GameObject spriteObject = new GameObject(objectName);
            spriteObject.transform.SetParent(parent, false);
            spriteObject.transform.localPosition = localPosition;
            spriteObject.transform.localScale = localScale;
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ApplySortingOrder(GameObject visualObject)
        {
            SpriteRenderer[] renderers = visualObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = visualSortingOrder + i;
            }
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.DontSave
            };
            runtimeMaterials.Add(material);
            return material;
        }

        private GameObject FindChildByName(string objectName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child != transform && child.name == objectName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyRuntimeObject(collider);
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
