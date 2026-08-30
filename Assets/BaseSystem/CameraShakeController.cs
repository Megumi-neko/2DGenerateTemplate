using UnityEngine;

namespace Game.BaseSystem
{
    [AddComponentMenu("Game/Camera/Camera Shake Controller")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class CameraShakeController : MonoBehaviour
    {
        private const float PositionComparisonEpsilon = 0.000001f;

        [SerializeField, Min(0f)] private float defaultAmplitude = 0.12f;
        [SerializeField, Min(0f)] private float defaultDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float frequency = 28f;

        private Vector3 appliedOffset;
        private Vector3 lastOutputLocalPosition;
        private float remaining;
        private float duration;
        private float amplitude;
        private float noiseTime;
        private bool hasAppliedOffset;

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
            amplitude = Mathf.Max(amplitude, shakeAmplitude);
            duration = Mathf.Max(duration, shakeDuration);
            remaining = Mathf.Max(remaining, shakeDuration);
            noiseTime = 0f;
        }

        private void LateUpdate()
        {
            UpdateShake(Time.unscaledDeltaTime);
        }

        private void UpdateShake(float deltaTime)
        {
            Vector3 basePosition = ResolveBasePosition();
            if (remaining <= 0f)
            {
                ResetShakeState();
                return;
            }

            remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, deltaTime));
            noiseTime += Mathf.Max(0f, deltaTime) * Mathf.Max(0.01f, frequency);
            float normalizedTime = duration <= 0f ? 0f : Mathf.Clamp01(remaining / duration);
            float envelope = normalizedTime * normalizedTime;
            Vector2 noise = new Vector2(
                Mathf.PerlinNoise(noiseTime, 0.37f) * 2f - 1f,
                Mathf.PerlinNoise(0.73f, noiseTime) * 2f - 1f);
            appliedOffset = (Vector3)(noise * amplitude * envelope);
            transform.localPosition = basePosition + appliedOffset;
            lastOutputLocalPosition = transform.localPosition;
            hasAppliedOffset = true;
        }

        private Vector3 ResolveBasePosition()
        {
            Vector3 currentPosition = transform.localPosition;
            if (!hasAppliedOffset)
            {
                return currentPosition;
            }

            bool stillAtLastOutput =
                (currentPosition - lastOutputLocalPosition).sqrMagnitude <=
                PositionComparisonEpsilon * PositionComparisonEpsilon;
            Vector3 basePosition = stillAtLastOutput
                ? currentPosition - appliedOffset
                : currentPosition;
            if (stillAtLastOutput)
            {
                transform.localPosition = basePosition;
            }

            appliedOffset = Vector3.zero;
            hasAppliedOffset = false;
            return basePosition;
        }

        private void ResetShakeState()
        {
            remaining = 0f;
            duration = 0f;
            amplitude = 0f;
            noiseTime = 0f;
        }

        private void OnDisable()
        {
            ResolveBasePosition();
            ResetShakeState();
        }

        private void OnValidate()
        {
            defaultAmplitude = Mathf.Max(0f, defaultAmplitude);
            defaultDuration = Mathf.Max(0f, defaultDuration);
            frequency = Mathf.Max(0.01f, frequency);
        }
    }
}
