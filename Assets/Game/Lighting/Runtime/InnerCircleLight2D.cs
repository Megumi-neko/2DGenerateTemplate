using UnityEngine;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Inner Circle Light 2D")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(LightEmitter2D))]
    public sealed class InnerCircleLight2D : MonoBehaviour
    {
        private const float MinimumRadiusMultiplier = 0.01f;
        private const float MaximumRadiusMultiplier = 0.99f;

        [SerializeField] private LightEmitter2D sourceEmitter;
        [SerializeField, Range(MinimumRadiusMultiplier, MaximumRadiusMultiplier)]
        private float radiusMultiplier = 0.5f;

        private LightEmitter2D innerEmitter;

        public LightEmitter2D SourceEmitter => sourceEmitter;
        public LightEmitter2D InnerEmitter => innerEmitter;
        public float RadiusMultiplier
        {
            get => radiusMultiplier;
            set => radiusMultiplier = Mathf.Clamp(value, MinimumRadiusMultiplier, MaximumRadiusMultiplier);
        }

        public float InnerRadius => sourceEmitter == null
            ? 0f
            : Mathf.Max(0.01f, sourceEmitter.BaseRadius * radiusMultiplier);

        private void Awake()
        {
            EnsureSourceEmitter();
            EnsureInnerEmitter();
            Synchronize();
        }

        private void OnEnable()
        {
            EnsureSourceEmitter();
            EnsureInnerEmitter();
            Synchronize();
        }

        private void LateUpdate()
        {
            Synchronize();
        }

        public void SynchronizeNow()
        {
            Synchronize();
        }

        private void OnValidate()
        {
            EnsureSourceEmitter();
            radiusMultiplier = Mathf.Clamp(
                radiusMultiplier,
                MinimumRadiusMultiplier,
                MaximumRadiusMultiplier);
        }

        private void OnDestroy()
        {
            if (innerEmitter == null || innerEmitter.gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(innerEmitter.gameObject);
            }
            else
            {
                DestroyImmediate(innerEmitter.gameObject);
            }

            innerEmitter = null;
        }

        private void EnsureSourceEmitter()
        {
            if (sourceEmitter == null)
            {
                sourceEmitter = GetComponent<LightEmitter2D>();
            }
        }

        private void EnsureInnerEmitter()
        {
            if (innerEmitter != null)
            {
                return;
            }

            GameObject innerObject = new GameObject("Inner Circle Light");
            innerObject.hideFlags = HideFlags.HideAndDontSave;
            innerObject.transform.SetParent(transform, false);
            innerObject.transform.localPosition = Vector3.zero;
            innerObject.transform.localRotation = Quaternion.identity;
            innerObject.transform.localScale = Vector3.one;
            innerEmitter = innerObject.AddComponent<LightEmitter2D>();
        }

        private void Synchronize()
        {
            if (sourceEmitter == null)
            {
                EnsureSourceEmitter();
            }

            if (sourceEmitter == null)
            {
                if (innerEmitter != null)
                {
                    innerEmitter.SetEmitting(false);
                }

                return;
            }

            if (innerEmitter == null)
            {
                EnsureInnerEmitter();
            }

            if (innerEmitter == null)
            {
                return;
            }

            innerEmitter.Shape = LightShape2D.Circle;
            innerEmitter.BaseRadius = InnerRadius;
            innerEmitter.BaseIntensity = sourceEmitter.BaseIntensity;
            innerEmitter.BaseDamagePerSecond = sourceEmitter.BaseDamagePerSecond;
            innerEmitter.EdgeSoftness = sourceEmitter.EdgeSoftness;
            innerEmitter.MaximumFocusMultiplier = sourceEmitter.MaximumFocusMultiplier;
            innerEmitter.Direction = sourceEmitter.Direction;
            innerEmitter.SetEmitting(sourceEmitter.IsEmitting && sourceEmitter.isActiveAndEnabled);
        }
    }
}
