using Game.Lighting;
using UnityEngine;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Build Placement Camera Controller")]
    [DisallowMultipleComponent]
    public sealed class BuildPlacementCameraController : MonoBehaviour
    {
        private const float MinimumFieldOfView = 1f;
        private const float MaximumFieldOfView = 179f;
        private const float MinimumRadius = 0.01f;
        private const float BoundaryEpsilon = 0.0001f;

        private static readonly Vector2[] ViewportCorners =
        {
            Vector2.zero,
            Vector2.right,
            Vector2.up,
            Vector2.one
        };

        [SerializeField] private Camera targetCamera;
        [SerializeField] private LightEmitter2D buildLight;
        [SerializeField] private CandleFocusController candleFocusController;
        [SerializeField] private StageLightingCameraFramer cameraFramer;
        [SerializeField] private float gameplayPlaneZ;
        [SerializeField, Range(1f, 89f)] private float minimumBuildFieldOfView = 25f;
        [SerializeField, Range(1f, 179f)] private float maximumBuildFieldOfView = 90f;
        [SerializeField, Min(0.1f)] private float zoomStep = 5f;
        [SerializeField, Min(0f)] private float movementSpeed = 8f;
        [SerializeField, Range(1f, 179f)] private float movementFieldOfViewThreshold = 55f;
        [SerializeField, Min(0f)] private float boundaryPadding = 0.1f;

        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedFieldOfView;
        private float savedOrthographicSize;
        private bool savedOrthographic;
        private bool savedFramerManualMode;
        private bool hasSavedCameraState;
        private bool isPlacing;
        private float placementMinimumFieldOfView;
        private float placementMaximumFieldOfView;
        private bool hasPlacementFieldOfViewBounds;

        public Camera TargetCamera => targetCamera;
        public LightEmitter2D BuildLight => buildLight;
        public bool IsPlacing => isPlacing;
        public float CurrentZoom => targetCamera == null ? 0f : targetCamera.fieldOfView;

        private void Awake()
        {
            ResolveReferences();
        }

        public bool BeginPlacement()
        {
            ResolveReferences();
            if (targetCamera == null || buildLight == null)
            {
                return false;
            }

            if (!isPlacing)
            {
                SaveCameraState();
                ConfigurePlacementFieldOfViewBounds();
                savedFramerManualMode = cameraFramer != null && cameraFramer.IsManualMode;
                isPlacing = true;
            }

            if (cameraFramer != null)
            {
                cameraFramer.SetManualMode(true);
            }

            if (candleFocusController != null)
            {
                candleFocusController.SetAimLockOverride(true);
            }

            return true;
        }

        public void UpdatePlacement()
        {
            if (!isPlacing || targetCamera == null)
            {
                return;
            }

            UpdateZoom();
            UpdateMovement();
        }

        public void EndPlacement()
        {
            if (!isPlacing && !hasSavedCameraState)
            {
                ClearPlacementFieldOfViewBounds();
                return;
            }

            isPlacing = false;
            RestoreCameraState();
            ClearPlacementFieldOfViewBounds();
            if (cameraFramer != null)
            {
                cameraFramer.SetManualMode(savedFramerManualMode);
            }

            UnlockAim();
        }

        public void SetReferences(
            Camera cameraToUse,
            LightEmitter2D emitter,
            CandleFocusController focusController,
            StageLightingCameraFramer framer,
            float planeZ)
        {
            targetCamera = cameraToUse;
            buildLight = emitter;
            candleFocusController = focusController;
            cameraFramer = framer;
            gameplayPlaneZ = planeZ;
        }

        internal void ConfigureForTests(
            Camera cameraToUse,
            LightEmitter2D emitter,
            float planeZ)
        {
            targetCamera = cameraToUse;
            buildLight = emitter;
            gameplayPlaneZ = planeZ;
        }

        internal void SetZoomConfigurationForTests(
            float minimumFov,
            float maximumFov,
            float step,
            float speed,
            float movementThreshold)
        {
            minimumBuildFieldOfView = minimumFov;
            maximumBuildFieldOfView = maximumFov;
            zoomStep = step;
            movementSpeed = speed;
            movementFieldOfViewThreshold = movementThreshold;
            SanitizeConfiguration();
        }

        internal bool IsCameraViewInsideBoundary(Vector3 position)
        {
            if (buildLight == null || targetCamera == null)
            {
                return false;
            }

            Vector3 originalPosition = targetCamera.transform.position;
            targetCamera.transform.position = position;
            float radius = Mathf.Max(
                MinimumRadius,
                buildLight.MaximumEffectiveRange - boundaryPadding);
            float radiusSquared = radius * radius;
            Vector2 origin = buildLight.WorldPosition;
            for (int i = 0; i < ViewportCorners.Length; i++)
            {
                if (!TryGetGameplayPlanePoint(ViewportCorners[i], out Vector2 point) ||
                    (point - origin).sqrMagnitude > radiusSquared + BoundaryEpsilon)
                {
                    targetCamera.transform.position = originalPosition;
                    return false;
                }
            }

            targetCamera.transform.position = originalPosition;
            return true;
        }

        private void UpdateZoom()
        {
            ApplyZoomDelta(Input.mouseScrollDelta.y);
        }

        internal void ApplyZoomDeltaForTests(float scroll)
        {
            ApplyZoomDelta(scroll);
        }

        private void ApplyZoomDelta(float scroll)
        {
            if (Mathf.Abs(scroll) <= BoundaryEpsilon ||
                targetCamera == null || targetCamera.orthographic)
            {
                return;
            }

            float minimumFov = hasPlacementFieldOfViewBounds
                ? placementMinimumFieldOfView
                : minimumBuildFieldOfView;
            float maximumFov = hasPlacementFieldOfViewBounds
                ? placementMaximumFieldOfView
                : maximumBuildFieldOfView;
            bool wasInsideBoundary = IsCameraViewInsideBoundary(
                targetCamera.transform.position);
            float previousFieldOfView = targetCamera.fieldOfView;
            targetCamera.fieldOfView = Mathf.Clamp(
                previousFieldOfView - scroll * zoomStep,
                minimumFov,
                maximumFov);
            if (wasInsideBoundary &&
                !IsCameraViewInsideBoundary(targetCamera.transform.position))
            {
                targetCamera.fieldOfView = previousFieldOfView;
            }
        }

        private bool IsMaximumLightRangeFullyVisible()
        {
            if (buildLight == null || targetCamera == null)
            {
                return false;
            }

            const int sampleCount = 16;
            Vector2 origin = buildLight.WorldPosition;
            float radius = buildLight.MaximumEffectiveRange;
            for (int i = 0; i < sampleCount; i++)
            {
                float angle = Mathf.PI * 2f * i / sampleCount;
                Vector3 point = new Vector3(
                    origin.x + Mathf.Cos(angle) * radius,
                    origin.y + Mathf.Sin(angle) * radius,
                    gameplayPlaneZ);
                Vector3 viewportPoint = targetCamera.WorldToViewportPoint(point);
                if (viewportPoint.z <= 0f ||
                    viewportPoint.x < 0f || viewportPoint.x > 1f ||
                    viewportPoint.y < 0f || viewportPoint.y > 1f)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector2 input = new Vector2(horizontal, vertical);
            if (input.sqrMagnitude <= BoundaryEpsilon)
            {
                return;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            Vector2 right = new Vector2(targetCamera.transform.right.x, targetCamera.transform.right.y);
            Vector2 up = new Vector2(targetCamera.transform.up.x, targetCamera.transform.up.y);
            Vector2 direction = right * input.x + up * input.y;
            if (direction.sqrMagnitude <= BoundaryEpsilon)
            {
                return;
            }

            direction.Normalize();
            Vector3 currentPosition = targetCamera.transform.position;
            Vector3 desiredPosition = currentPosition +
                new Vector3(direction.x, direction.y, 0f) * movementSpeed * Time.deltaTime;
            if (IsCameraViewInsideBoundary(desiredPosition))
            {
                targetCamera.transform.position = desiredPosition;
                return;
            }

            if (!IsCameraViewInsideBoundary(currentPosition))
            {
                if (IsCameraCenterInsideBoundary(desiredPosition))
                {
                    targetCamera.transform.position = desiredPosition;
                }
                return;
            }

            float low = 0f;
            float high = 1f;
            for (int i = 0; i < 8; i++)
            {
                float middle = (low + high) * 0.5f;
                Vector3 candidate = Vector3.Lerp(currentPosition, desiredPosition, middle);
                if (IsCameraViewInsideBoundary(candidate))
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            targetCamera.transform.position = Vector3.Lerp(
                currentPosition,
                desiredPosition,
                low);
        }

        private bool IsCameraCenterInsideBoundary(Vector3 position)
        {
            if (buildLight == null || targetCamera == null)
            {
                return false;
            }

            Vector3 originalPosition = targetCamera.transform.position;
            targetCamera.transform.position = position;
            bool hasCenter = TryGetGameplayPlanePoint(new Vector2(0.5f, 0.5f), out Vector2 center);
            targetCamera.transform.position = originalPosition;
            if (!hasCenter)
            {
                return false;
            }

            float radius = Mathf.Max(
                MinimumRadius,
                buildLight.MaximumEffectiveRange - boundaryPadding);
            return (center - buildLight.WorldPosition).sqrMagnitude <=
                radius * radius + BoundaryEpsilon;
        }

        private bool TryGetGameplayPlanePoint(Vector2 viewportPosition, out Vector2 point)
        {
            Ray ray = targetCamera.ViewportPointToRay(new Vector3(
                viewportPosition.x,
                viewportPosition.y,
                0f));
            if (Mathf.Abs(ray.direction.z) <= BoundaryEpsilon)
            {
                point = default;
                return false;
            }

            float distance = (gameplayPlaneZ - ray.origin.z) / ray.direction.z;
            if (distance < 0f || float.IsNaN(distance) || float.IsInfinity(distance))
            {
                point = default;
                return false;
            }

            Vector3 worldPoint = ray.GetPoint(distance);
            point = new Vector2(worldPoint.x, worldPoint.y);
            return true;
        }

        private void ConfigurePlacementFieldOfViewBounds()
        {
            placementMinimumFieldOfView = Mathf.Min(
                minimumBuildFieldOfView,
                savedFieldOfView);
            placementMaximumFieldOfView = Mathf.Max(
                maximumBuildFieldOfView,
                savedFieldOfView);
            hasPlacementFieldOfViewBounds = true;
        }

        private void ClearPlacementFieldOfViewBounds()
        {
            placementMinimumFieldOfView = 0f;
            placementMaximumFieldOfView = 0f;
            hasPlacementFieldOfViewBounds = false;
        }

        private void SaveCameraState()
        {
            savedPosition = targetCamera.transform.position;
            savedRotation = targetCamera.transform.rotation;
            savedFieldOfView = targetCamera.fieldOfView;
            savedOrthographicSize = targetCamera.orthographicSize;
            savedOrthographic = targetCamera.orthographic;
            hasSavedCameraState = true;
        }

        private void RestoreCameraState()
        {
            if (!hasSavedCameraState || targetCamera == null)
            {
                return;
            }

            targetCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            targetCamera.fieldOfView = savedFieldOfView;
            targetCamera.orthographicSize = savedOrthographicSize;
            targetCamera.orthographic = savedOrthographic;
            hasSavedCameraState = false;
        }

        private void UnlockAim()
        {
            if (candleFocusController != null)
            {
                candleFocusController.SetAimLockOverride(false);
                candleFocusController.SetAimLocked(false);
            }
        }

        private void ResolveReferences()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (buildLight == null)
            {
                StageLightingBootstrap bootstrap = FindObjectOfType<StageLightingBootstrap>();
                buildLight = bootstrap == null ? null : bootstrap.CandleEmitter;
            }

            if (candleFocusController == null && buildLight != null)
            {
                candleFocusController = buildLight.GetComponent<CandleFocusController>();
            }

            if (cameraFramer == null && targetCamera != null)
            {
                cameraFramer = targetCamera.GetComponent<StageLightingCameraFramer>();
            }
        }

        private void OnDisable()
        {
            EndPlacement();
        }

        private void OnValidate()
        {
            SanitizeConfiguration();
        }

        private void SanitizeConfiguration()
        {
            minimumBuildFieldOfView = Mathf.Clamp(
                minimumBuildFieldOfView,
                1f,
                89f);
            maximumBuildFieldOfView = Mathf.Clamp(
                maximumBuildFieldOfView,
                minimumBuildFieldOfView,
                179f);
            zoomStep = Mathf.Max(0.1f, zoomStep);
            movementSpeed = Mathf.Max(0f, movementSpeed);
            movementFieldOfViewThreshold = Mathf.Clamp(
                movementFieldOfViewThreshold,
                minimumBuildFieldOfView,
                maximumBuildFieldOfView);
            boundaryPadding = Mathf.Max(0f, boundaryPadding);
        }
    }
}
