using UnityEngine;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Inner Circle Flicker 2D")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(InnerCircleLight2D))]
    public sealed class InnerCircleFlicker2D : MonoBehaviour
    {
        private const float SeedRange = 64f;
        private const float FastSeedOffset = 17.3f;
        private const float RangeSeedOffset = 31.7f;

        [SerializeField] private InnerCircleLight2D innerCircle;
        [Header("Brightness")]
        [SerializeField, Min(0f)] private float slowSpeed = 1.4f;
        [SerializeField, Min(0f)] private float slowAmplitude = 0.18f;
        [SerializeField, Min(0f)] private float fastSpeed = 8f;
        [SerializeField, Min(0f)] private float fastAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float maxAmplitude = 0.22f;
        [Header("Breathing Range")]
        [SerializeField, Min(0f)] private float rangeSpeed = 1.1f;
        [SerializeField, Min(0f)] private float rangeAmplitude = 0.08f;

        private float noiseSeed;

        public InnerCircleLight2D InnerCircle => innerCircle;

        private void Awake()
        {
            EnsureInnerCircle();
            noiseSeed = Random.Range(0f, SeedRange);
        }

        private void OnEnable()
        {
            EnsureInnerCircle();
            ApplyNow();
        }

        private void OnDisable()
        {
            ResetVisualMultipliers();
        }

        private void LateUpdate()
        {
            ApplyNow();
        }

        private void OnValidate()
        {
            EnsureInnerCircle();
            slowSpeed = Mathf.Max(0f, slowSpeed);
            slowAmplitude = Mathf.Max(0f, slowAmplitude);
            fastSpeed = Mathf.Max(0f, fastSpeed);
            fastAmplitude = Mathf.Max(0f, fastAmplitude);
            maxAmplitude = Mathf.Max(0f, maxAmplitude);
            rangeSpeed = Mathf.Max(0f, rangeSpeed);
            rangeAmplitude = Mathf.Max(0f, rangeAmplitude);
        }

        public void ApplyNow()
        {
            EnsureInnerCircle();
            LightEmitter2D innerEmitter = innerCircle == null ? null : innerCircle.InnerEmitter;
            if (innerEmitter == null || !innerEmitter.IsEmitting)
            {
                ResetVisualMultipliers();
                return;
            }

            float time = Time.time;
            innerEmitter.VisualIntensityMultiplier = EvaluateIntensityMultiplier(time);
            innerEmitter.VisualRangeMultiplier = EvaluateRangeMultiplier(time);
        }

        public float EvaluateIntensityMultiplier(float time)
        {
            return EvaluateSignedNoise(
                time,
                slowSpeed,
                slowAmplitude,
                fastSpeed,
                fastAmplitude,
                maxAmplitude,
                noiseSeed,
                FastSeedOffset);
        }

        public float EvaluateRangeMultiplier(float time)
        {
            return EvaluateSignedNoise(
                time,
                rangeSpeed,
                rangeAmplitude,
                0f,
                0f,
                rangeAmplitude,
                noiseSeed + RangeSeedOffset,
                0f);
        }

        public float EvaluateMultiplier(float time)
        {
            return EvaluateIntensityMultiplier(time);
        }

        private static float EvaluateSignedNoise(
            float time,
            float primarySpeed,
            float primaryAmplitude,
            float secondarySpeed,
            float secondaryAmplitude,
            float clampAmplitude,
            float seed,
            float secondarySeedOffset)
        {
            float clampedMaxAmplitude = Mathf.Max(0f, clampAmplitude);
            if (clampedMaxAmplitude <= 0f)
            {
                return 1f;
            }

            float primary = (Mathf.PerlinNoise(time * Mathf.Max(0f, primarySpeed), seed) - 0.5f) *
                Mathf.Max(0f, primaryAmplitude);
            float secondary = (Mathf.PerlinNoise(
                    time * Mathf.Max(0f, secondarySpeed),
                    seed + secondarySeedOffset) - 0.5f) *
                Mathf.Max(0f, secondaryAmplitude);
            return Mathf.Clamp(
                1f + primary + secondary,
                1f - clampedMaxAmplitude,
                1f + clampedMaxAmplitude);
        }

        private void EnsureInnerCircle()
        {
            if (innerCircle == null)
            {
                innerCircle = GetComponent<InnerCircleLight2D>();
            }
        }

        private void ResetVisualMultipliers()
        {
            LightEmitter2D innerEmitter = innerCircle == null ? null : innerCircle.InnerEmitter;
            if (innerEmitter == null)
            {
                return;
            }

            innerEmitter.VisualIntensityMultiplier = 1f;
            innerEmitter.VisualRangeMultiplier = 1f;
        }
    }
}
