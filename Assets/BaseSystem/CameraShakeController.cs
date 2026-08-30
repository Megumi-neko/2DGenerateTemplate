using UnityEngine;

namespace Game.BaseSystem
{
    [AddComponentMenu("Game/Camera/Camera Shake Controller")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class CameraShakeController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float defaultAmplitude = 0.12f;
        [SerializeField, Min(0f)] private float defaultDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float frequency = 28f;

        private Vector3 baseLocalPosition;
        private float remaining;
        private float duration;
        private float amplitude;
        private float noiseTime;

        public static void ShakeMainCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            CameraShakeController controller = camera.GetComponent<CameraShakeController>();
            if (controller == null) controller = camera.gameObject.AddComponent<CameraShakeController>();
            controller.Shake();
        }

        public void Shake()
        {
            Shake(defaultAmplitude, defaultDuration);
        }

        public void Shake(float shakeAmplitude, float shakeDuration)
        {
            if (shakeAmplitude <= 0f || shakeDuration <= 0f) return;
            baseLocalPosition = transform.localPosition;
            amplitude = Mathf.Max(amplitude, shakeAmplitude);
            duration = Mathf.Max(duration, shakeDuration);
            remaining = Mathf.Max(remaining, shakeDuration);
            noiseTime = 0f;
        }

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            if (remaining <= 0f)
            {
                transform.localPosition = baseLocalPosition;
                amplitude = 0f;
                return;
            }

            remaining -= Time.unscaledDeltaTime;
            noiseTime += Time.unscaledDeltaTime * Mathf.Max(0.01f, frequency);
            float normalizedTime = duration <= 0f ? 0f : Mathf.Clamp01(remaining / duration);
            float envelope = normalizedTime * normalizedTime;
            Vector2 noise = new Vector2(
                Mathf.PerlinNoise(noiseTime, 0.37f) * 2f - 1f,
                Mathf.PerlinNoise(0.73f, noiseTime) * 2f - 1f);
            transform.localPosition = baseLocalPosition +
                (Vector3)(noise * amplitude * envelope);
        }

        private void OnDisable()
        {
            remaining = 0f;
            amplitude = 0f;
            transform.localPosition = baseLocalPosition;
        }

        private void OnValidate()
        {
            defaultAmplitude = Mathf.Max(0f, defaultAmplitude);
            defaultDuration = Mathf.Max(0f, defaultDuration);
            frequency = Mathf.Max(0.01f, frequency);
        }
    }
}
