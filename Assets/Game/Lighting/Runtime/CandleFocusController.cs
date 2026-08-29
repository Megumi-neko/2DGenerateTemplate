using UnityEngine;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Candle Focus Controller")]
    [DisallowMultipleComponent]
    public sealed class CandleFocusController : MonoBehaviour
    {
        private const float GameplayPlaneEpsilon = 0.0001f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private LightEmitter2D controlledEmitter;
        [SerializeField] private float gameplayPlaneZ;
        [SerializeField] private KeyCode aimLockKey = KeyCode.F;
        [SerializeField, Min(0.1f)] private float sectorAngleStep = 10f;
        [SerializeField] private bool allowShapeToggle = true;
        [SerializeField] private bool allowSectorAngleInput = true;

        private bool isAimLocked;
        private bool forceAimLock;
        private Vector2 pointerWorldPosition;
        private bool hasPointerWorldPosition;

        public Camera TargetCamera => targetCamera;
        public LightEmitter2D ControlledEmitter => controlledEmitter;
        public bool IsAimLocked => isAimLocked || forceAimLock;
        public bool HasPointerWorldPosition => hasPointerWorldPosition;
        public Vector2 PointerWorldPosition => pointerWorldPosition;

        private void Awake()
        {
            if (controlledEmitter == null)
            {
                controlledEmitter = GetComponent<LightEmitter2D>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

private void Update()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (controlledEmitter == null || targetCamera == null)
            {
                hasPointerWorldPosition = false;
                return;
            }

            if (Input.GetKeyDown(aimLockKey))
            {
                ToggleAimLock();
            }

            if (allowShapeToggle && Input.GetKeyDown(KeyCode.Space))
            {
                controlledEmitter.ToggleShape();
            }

            if (allowSectorAngleInput)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) <= GameplayPlaneEpsilon)
                {
                    scroll = Input.GetAxisRaw("Mouse ScrollWheel");
                }

                ApplySectorAngleInput(scroll);
            }

            hasPointerWorldPosition = TryGetPointerWorldPosition(out pointerWorldPosition);
            if (hasPointerWorldPosition && !IsAimLocked)
            {
                controlledEmitter.SetDirectionTowards(pointerWorldPosition);
            }
        }

        internal void ApplySectorAngleInput(float scroll)
        {
            if (controlledEmitter == null || IsAimLocked ||
                Mathf.Abs(scroll) <= GameplayPlaneEpsilon)
            {
                return;
            }

            controlledEmitter.SectorAngle -= scroll * sectorAngleStep;
        }

        public void ToggleAimLock()
        {
            if (forceAimLock)
            {
                return;
            }

            isAimLocked = !isAimLocked;
        }

        public void SetAimLocked(bool value)
        {
            isAimLocked = value;
        }

        public void SetAimLockOverride(bool value)
        {
            forceAimLock = value;
        }

        public void Initialize(Camera cameraToUse, LightEmitter2D emitterToControl)
        {
            targetCamera = cameraToUse;
            controlledEmitter = emitterToControl;
        }

        public bool TryGetPointerWorldPosition(out Vector2 worldPosition)
        {
            if (targetCamera == null)
            {
                worldPosition = default;
                return false;
            }

            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            float denominator = ray.direction.z;
            if (Mathf.Abs(denominator) <= GameplayPlaneEpsilon)
            {
                worldPosition = default;
                return false;
            }

            float distance = (gameplayPlaneZ - ray.origin.z) / denominator;
            if (distance < 0f)
            {
                worldPosition = default;
                return false;
            }

            Vector3 point = ray.GetPoint(distance);
            worldPosition = new Vector2(point.x, point.y);
            return true;
        }

        private void OnValidate()
        {
            sectorAngleStep = Mathf.Max(0.1f, sectorAngleStep);
            gameplayPlaneZ = IsFinite(gameplayPlaneZ) ? gameplayPlaneZ : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
