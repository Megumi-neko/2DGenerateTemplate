using System.Collections.Generic;
using UnityEngine;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Stage Lighting Camera Framer")]
    [DisallowMultipleComponent]
    public sealed class StageLightingCameraFramer : MonoBehaviour
    {
        private const float MinimumPadding = 0.01f;
        private const float MaximumPadding = 0.45f;
        private const float MinimumDistance = 0.01f;
        private const int DefaultBoundarySegments = 32;

        [Header("Framing")]
        [SerializeField, Range(MinimumPadding, MaximumPadding)]
        private float screenPadding = 0.1f;
        [SerializeField, Range(0f, 1f)]
        private float framingCenterBias = 0.12f;
        [SerializeField, Min(0f)] private float boundaryPadding = 0.2f;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.2f;
        [SerializeField, Min(0f)] private float targetChangeThreshold = 0.05f;
        [SerializeField, Min(4)] private int boundarySegments = DefaultBoundarySegments;

        [Header("Safety Limits")]
        [SerializeField, Min(0f)] private float maximumFramingOffset = 6f;
        [SerializeField, Min(0f)] private float maximumCameraRise = 1.5f;
        [SerializeField, Min(0f)] private float maximumCameraBackstep = 80f;
        [SerializeField] private bool enforceSafeViewportDuringMotion = true;

        private readonly List<Vector3> boundaryPoints = new List<Vector3>(DefaultBoundarySegments + 40);
        private Camera targetCamera;
        private LightEmitter2D emitter;
        private InnerCircleLight2D innerCircle;
        private float gameplayPlaneZ;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 positionVelocity;
        private Vector3 targetPosition;
        private bool initialized;

        public Camera TargetCamera => targetCamera;
        public LightEmitter2D Emitter => emitter;
        public Vector3 InitialPosition => initialPosition;
        public Vector3 TargetPosition => targetPosition;
        public float ScreenPadding => screenPadding;

        public void Initialize(Camera cameraToUse, LightEmitter2D emitterToFrame, float planeZ)
        {
            targetCamera = cameraToUse;
            emitter = emitterToFrame;
            innerCircle = emitter == null
                ? null
                : emitter.GetComponent<InnerCircleLight2D>();
            gameplayPlaneZ = IsFinite(planeZ) ? planeZ : 0f;

            if (targetCamera == null)
            {
                initialized = false;
                return;
            }

            if (!initialized)
            {
                initialPosition = targetCamera.transform.position;
                initialRotation = targetCamera.transform.rotation;
                initialized = true;
            }

            targetCamera.transform.rotation = initialRotation;
            ReframeImmediately();
        }

        public void ReframeImmediately()
        {
            if (!initialized || targetCamera == null)
            {
                return;
            }

            targetPosition = CalculateTargetPosition();
            positionVelocity = Vector3.zero;
        }

        public void ResetToInitialPose()
        {
            if (!initialized || targetCamera == null)
            {
                return;
            }

            targetCamera.transform.SetPositionAndRotation(initialPosition, initialRotation);
            targetPosition = initialPosition;
            positionVelocity = Vector3.zero;
        }

        public Vector3 CalculateTargetPosition()
        {
            if (!initialized || targetCamera == null || emitter == null)
            {
                return initialPosition;
            }

            CollectBoundaryPoints();
            if (boundaryPoints.Count == 0)
            {
                return initialPosition;
            }

            Vector3 right = initialRotation * Vector3.right;
            Vector3 up = initialRotation * Vector3.up;
            Vector3 forward = initialRotation * Vector3.forward;
            Vector3 framingOffset = Vector3.zero;
            bool isSector = emitter.Shape == LightShape2D.Sector &&
                emitter.SectorAngle < LightGeometry2D.FullCircleAngle - 0.001f;
            if (isSector)
            {
                Vector3 sectorDirection = new Vector3(
                    emitter.Direction.x,
                    emitter.Direction.y,
                    0f);
                float framingDistance =
                    (emitter.EffectiveRange + boundaryPadding) * Mathf.Clamp01(framingCenterBias);
                framingOffset = sectorDirection * framingDistance;
                framingOffset = right * Vector3.Dot(framingOffset, right) +
                    up * Vector3.Dot(framingOffset, up);
            }

            framingOffset = Vector3.ClampMagnitude(framingOffset, maximumFramingOffset);
            float screenOffsetX = Vector3.Dot(framingOffset, right);
            float screenOffsetY = 0f;
            screenOffsetY = Mathf.Clamp(
                screenOffsetY,
                -maximumCameraRise / Mathf.Max(MinimumDistance, Mathf.Abs(up.y)),
                maximumCameraRise / Mathf.Max(MinimumDistance, Mathf.Abs(up.y)));

            float backstep = CalculateRequiredBackstep(
                screenOffsetX,
                screenOffsetY);
            Vector3 result = initialPosition + right * screenOffsetX +
                up * screenOffsetY - forward * backstep;
            return IsFinite(result) ? result : initialPosition;
        }

        private void LateUpdate()
        {
            if (!initialized || targetCamera == null || emitter == null)
            {
                return;
            }

            Vector3 calculatedTarget = CalculateTargetPosition();
            if ((calculatedTarget - targetPosition).sqrMagnitude >= targetChangeThreshold * targetChangeThreshold)
            {
                targetPosition = calculatedTarget;
            }

            targetCamera.transform.rotation = initialRotation;
            targetCamera.transform.position = Vector3.SmoothDamp(
                targetCamera.transform.position,
                targetPosition,
                ref positionVelocity,
                smoothTime);
            if (enforceSafeViewportDuringMotion)
            {
                targetCamera.transform.position = CalculateSafeCurrentPosition(
                    targetCamera.transform.position);
            }
        }

        private Vector3 CalculateSafeCurrentPosition(Vector3 currentPosition)
        {
            if (targetCamera.orthographic || boundaryPoints.Count == 0)
            {
                return currentPosition;
            }

            Vector3 right = initialRotation * Vector3.right;
            Vector3 up = initialRotation * Vector3.up;
            Vector3 forward = initialRotation * Vector3.forward;
            float padding = Mathf.Clamp(screenPadding, MinimumPadding, MaximumPadding);
            float safeExtentFactor = Mathf.Max(MinimumDistance, 1f - 2f * padding);
            float horizontalScale = Mathf.Max(
                MinimumDistance,
                safeExtentFactor * targetCamera.aspect * Mathf.Tan(
                    targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float verticalScale = Mathf.Max(
                MinimumDistance,
                safeExtentFactor * Mathf.Tan(
                    targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float requiredBackstep = 0f;
            for (int i = 0; i < boundaryPoints.Count; i++)
            {
                Vector3 offset = boundaryPoints[i] - currentPosition;
                float depth = Vector3.Dot(offset, forward);
                float x = Mathf.Abs(Vector3.Dot(offset, right));
                float y = Mathf.Abs(Vector3.Dot(offset, up));
                requiredBackstep = Mathf.Max(
                    requiredBackstep,
                    x / horizontalScale - depth,
                    y / verticalScale - depth,
                    MinimumDistance - depth);
            }

            if (requiredBackstep <= 0f)
            {
                return currentPosition;
            }

            return currentPosition - forward * Mathf.Min(requiredBackstep, maximumCameraBackstep);
        }

        private float CalculateRequiredBackstep(
            float screenOffsetX,
            float screenOffsetY)
        {
            float padding = Mathf.Clamp(screenPadding, MinimumPadding, MaximumPadding);
            float safeExtentFactor = Mathf.Max(MinimumDistance, 1f - 2f * padding);
            float requiredBackstep = 0f;

            if (targetCamera.orthographic)
            {
                float halfHeight = targetCamera.orthographicSize * safeExtentFactor;
                float halfWidth = targetCamera.orthographicSize * targetCamera.aspect * safeExtentFactor;
                for (int i = 0; i < boundaryPoints.Count; i++)
                {
                    Vector3 offset = boundaryPoints[i] - initialPosition;
                    Vector3 coordinates = new Vector3(
                        Vector3.Dot(offset, initialRotation * Vector3.right),
                        Vector3.Dot(offset, initialRotation * Vector3.up),
                        Vector3.Dot(offset, initialRotation * Vector3.forward));
                    requiredBackstep = Mathf.Max(
                        requiredBackstep,
                        Mathf.Abs(coordinates.x - screenOffsetX) - halfWidth,
                        Mathf.Abs(coordinates.y - screenOffsetY) - halfHeight);
                }

                return 0f;
            }

            float verticalTangent = Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontalTangent = verticalTangent * targetCamera.aspect;
            if (verticalTangent <= MinimumDistance || horizontalTangent <= MinimumDistance)
            {
                return 0f;
            }

            float horizontalScale = safeExtentFactor * horizontalTangent;
            float verticalScale = safeExtentFactor * verticalTangent;
            for (int i = 0; i < boundaryPoints.Count; i++)
            {
                Vector3 offset = boundaryPoints[i] - initialPosition;
                Vector3 coordinates = new Vector3(
                    Vector3.Dot(offset, initialRotation * Vector3.right),
                    Vector3.Dot(offset, initialRotation * Vector3.up),
                    Vector3.Dot(offset, initialRotation * Vector3.forward));
                requiredBackstep = Mathf.Max(
                    requiredBackstep,
                    Mathf.Abs(coordinates.x - screenOffsetX) / horizontalScale - coordinates.z,
                    Mathf.Abs(coordinates.y - screenOffsetY) / verticalScale - coordinates.z,
                    MinimumDistance - coordinates.z);
            }

            return Mathf.Clamp(Mathf.Max(0f, requiredBackstep), 0f, maximumCameraBackstep);
        }

        private void CollectBoundaryPoints()
        {
            boundaryPoints.Clear();
            Vector2 origin2D = emitter.WorldPosition;
            Vector3 origin = new Vector3(origin2D.x, origin2D.y, gameplayPlaneZ);
            AddLightBoundaryPoints(origin, emitter.EffectiveRange + boundaryPadding, emitter);

            if (innerCircle != null)
            {
                float innerRange = innerCircle.InnerRadius + boundaryPadding;
                AddCircleBoundaryPoints(origin, innerRange);
            }
        }

        private void AddLightBoundaryPoints(Vector3 origin, float range, LightEmitter2D light)
        {
            range = Mathf.Max(MinimumDistance, range);
            bool circle = light.Shape == LightShape2D.Circle ||
                light.SectorAngle >= LightGeometry2D.FullCircleAngle - 0.001f;
            if (circle)
            {
                AddCircleBoundaryPoints(origin, range);
                return;
            }

            float halfAngle = light.SectorAngle * 0.5f;
            int arcSegments = Mathf.Max(2, Mathf.CeilToInt(
                boundarySegments * light.SectorAngle / LightGeometry2D.FullCircleAngle));
            float centerAngle = light.DirectionAngleDegrees;
            AddBoundaryPoint(origin, range, centerAngle - halfAngle);
            AddBoundaryPoint(origin, range, centerAngle + halfAngle);
            for (int i = 0; i <= arcSegments; i++)
            {
                float angle = centerAngle - halfAngle + light.SectorAngle * i / arcSegments;
                AddBoundaryPoint(origin, range, angle);
            }

            boundaryPoints.Add(origin);
        }

        private void AddCircleBoundaryPoints(Vector3 origin, float range)
        {
            int segments = Mathf.Max(4, boundarySegments);
            for (int i = 0; i < segments; i++)
            {
                AddBoundaryPoint(origin, range, i * LightGeometry2D.FullCircleAngle / segments);
            }
        }

        private void AddBoundaryPoint(Vector3 origin, float range, float angleDegrees)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            boundaryPoints.Add(origin + new Vector3(
                Mathf.Cos(angleRadians) * range,
                Mathf.Sin(angleRadians) * range,
                0f));
        }

        private void OnValidate()
        {
            screenPadding = Mathf.Clamp(screenPadding, MinimumPadding, MaximumPadding);
            framingCenterBias = Mathf.Clamp01(framingCenterBias);
            boundaryPadding = Mathf.Max(0f, boundaryPadding);
            smoothTime = Mathf.Max(0.01f, smoothTime);
            targetChangeThreshold = Mathf.Max(0f, targetChangeThreshold);
            boundarySegments = Mathf.Max(4, boundarySegments);
            maximumFramingOffset = Mathf.Max(0f, maximumFramingOffset);
            maximumCameraRise = Mathf.Max(0f, maximumCameraRise);
            maximumCameraBackstep = Mathf.Max(0f, maximumCameraBackstep);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
