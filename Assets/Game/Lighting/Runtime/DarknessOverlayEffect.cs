using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Darkness Overlay Effect")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class DarknessOverlayEffect : MonoBehaviour
    {
        public const int MaximumSupportedLights = 32;
        private const string DefaultShaderName = "Hidden/Game/Lighting/DarknessOverlay";
        private const string DefaultShaderResourcePath = "Lighting/DarknessOverlay";

        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_InverseViewProjection");
        private static readonly int GameplayPlaneZId = Shader.PropertyToID("_GameplayPlaneZ");
        private static readonly int DarknessColorId = Shader.PropertyToID("_DarknessColor");
        private static readonly int DarknessOpacityId = Shader.PropertyToID("_DarknessOpacity");
        private static readonly int LightCountId = Shader.PropertyToID("_LightCount");
        private static readonly int LightPositionRangeIntensityId =
            Shader.PropertyToID("_LightPositionRangeIntensity");
        private static readonly int LightDirectionShapeSoftnessId =
            Shader.PropertyToID("_LightDirectionShapeSoftness");
        private static readonly int LightAngleCosinesId = Shader.PropertyToID("_LightAngleCosines");

        [SerializeField] private Shader darknessShader;
        [SerializeField] private Color darknessColor = new Color(0.01f, 0.015f, 0.025f, 1f);
        [SerializeField, Range(0f, 1f)] private float darknessOpacity = 0.98f;
        [SerializeField] private float gameplayPlaneZ;
        [SerializeField, Range(1, MaximumSupportedLights)] private int maximumVisibleLights = MaximumSupportedLights;
        [SerializeField] private bool warnWhenLightLimitExceeded = true;

        private readonly List<LightEmitter2D> candidates = new List<LightEmitter2D>();
        private readonly Vector4[] lightPositionRangeIntensity = new Vector4[MaximumSupportedLights];
        private readonly Vector4[] lightDirectionShapeSoftness = new Vector4[MaximumSupportedLights];
        private readonly Vector4[] lightAngleCosines = new Vector4[MaximumSupportedLights];

        private Camera targetCamera;
        private Material material;
        private Comparison<LightEmitter2D> relevanceComparison;
        private Vector2 sortOrigin;
        private bool hasWarnedAboutLimit;

        public Color DarknessColor
        {
            get => darknessColor;
            set => darknessColor = value;
        }

        public float DarknessOpacity
        {
            get => darknessOpacity;
            set => darknessOpacity = Mathf.Clamp01(value);
        }

        public float GameplayPlaneZ
        {
            get => gameplayPlaneZ;
            set => gameplayPlaneZ = IsFinite(value) ? value : 0f;
        }

        public int MaximumVisibleLights
        {
            get => maximumVisibleLights;
            set => maximumVisibleLights = Mathf.Clamp(value, 1, MaximumSupportedLights);
        }

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            relevanceComparison = CompareByViewRelevance;
            EnsureMaterial();
        }

        private void OnDisable()
        {
            ReleaseMaterial();
        }

        private void OnValidate()
        {
            darknessOpacity = Mathf.Clamp01(darknessOpacity);
            gameplayPlaneZ = IsFinite(gameplayPlaneZ) ? gameplayPlaneZ : 0f;
            maximumVisibleLights = Mathf.Clamp(maximumVisibleLights, 1, MaximumSupportedLights);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetCamera == null || !EnsureMaterial())
            {
                Graphics.Blit(source, destination);
                return;
            }

            int lightCount = CollectRenderLights();
            Matrix4x4 inverseViewProjection =
                (targetCamera.projectionMatrix * targetCamera.worldToCameraMatrix).inverse;

            material.SetMatrix(InverseViewProjectionId, inverseViewProjection);
            material.SetFloat(GameplayPlaneZId, gameplayPlaneZ);
            material.SetColor(DarknessColorId, darknessColor);
            material.SetFloat(DarknessOpacityId, darknessOpacity);
            material.SetInt(LightCountId, lightCount);
            material.SetVectorArray(LightPositionRangeIntensityId, lightPositionRangeIntensity);
            material.SetVectorArray(LightDirectionShapeSoftnessId, lightDirectionShapeSoftness);
            material.SetVectorArray(LightAngleCosinesId, lightAngleCosines);

            Graphics.Blit(source, destination, material, 0);
        }

        private int CollectRenderLights()
        {
            IlluminationSystem.GetOperationalEmittersNonAlloc(candidates);
            sortOrigin = GetViewCenterOnGameplayPlane();
            relevanceComparison ??= CompareByViewRelevance;
            candidates.Sort(relevanceComparison);

            int lightLimit = Mathf.Min(maximumVisibleLights, MaximumSupportedLights);
            int lightCount = Mathf.Min(candidates.Count, lightLimit);
            for (int i = 0; i < lightCount; i++)
            {
                LightEmitter2D emitter = candidates[i];
                float range = emitter.EffectiveRange;
                float softness = Mathf.Clamp(emitter.EdgeSoftness, 0f, range);
                bool rendersAsSector =
                    emitter.Shape == LightShape2D.Sector &&
                    emitter.SectorAngle < LightGeometry2D.FullCircleAngle - 0.001f;

                LightGeometry2D.CalculateAngularCosines(
                    emitter.SectorAngle,
                    softness,
                    range,
                    out float outerCosine,
                    out float innerCosine);

                Vector2 position = emitter.WorldPosition;
                Vector2 direction = emitter.Direction;
                lightPositionRangeIntensity[i] = new Vector4(
                    position.x,
                    position.y,
                    range,
                    emitter.CurrentIntensity);
                lightDirectionShapeSoftness[i] = new Vector4(
                    direction.x,
                    direction.y,
                    rendersAsSector ? 1f : 0f,
                    softness);
                lightAngleCosines[i] = new Vector4(outerCosine, innerCosine, 0f, 0f);
            }

            if (warnWhenLightLimitExceeded && candidates.Count > lightLimit && !hasWarnedAboutLimit)
            {
                hasWarnedAboutLimit = true;
                Debug.LogWarning(
                    $"[{nameof(DarknessOverlayEffect)}] {candidates.Count} active lights found, " +
                    $"but only the {lightLimit} most relevant lights can be rendered. " +
                    "Gameplay illumination still evaluates every light.",
                    this);
            }

            return lightCount;
        }

        private Vector2 GetViewCenterOnGameplayPlane()
        {
            Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float denominator = ray.direction.z;
            if (Mathf.Abs(denominator) <= 0.0001f)
            {
                return new Vector2(targetCamera.transform.position.x, targetCamera.transform.position.y);
            }

            float distance = (gameplayPlaneZ - ray.origin.z) / denominator;
            if (distance < 0f)
            {
                return new Vector2(targetCamera.transform.position.x, targetCamera.transform.position.y);
            }

            Vector3 point = ray.GetPoint(distance);
            return new Vector2(point.x, point.y);
        }

        private int CompareByViewRelevance(LightEmitter2D left, LightEmitter2D right)
        {
            float leftScore = CalculateRelevanceScore(left);
            float rightScore = CalculateRelevanceScore(right);
            int scoreComparison = leftScore.CompareTo(rightScore);
            return scoreComparison != 0
                ? scoreComparison
                : left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private float CalculateRelevanceScore(LightEmitter2D emitter)
        {
            float distanceSquared = (emitter.WorldPosition - sortOrigin).sqrMagnitude;
            float range = emitter.EffectiveRange;
            return distanceSquared - range * range;
        }

        private bool EnsureMaterial()
        {
            if (material != null)
            {
                return true;
            }

            if (darknessShader == null)
            {
                darknessShader = Resources.Load<Shader>(DefaultShaderResourcePath);
            }

            if (darknessShader == null)
            {
                darknessShader = Shader.Find(DefaultShaderName);
            }

            if (darknessShader == null || !darknessShader.isSupported)
            {
                return false;
            }

            material = new Material(darknessShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return true;
        }

        private void ReleaseMaterial()
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }

            material = null;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
