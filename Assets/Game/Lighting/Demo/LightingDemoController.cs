using UnityEngine;

namespace Game.Lighting.Demo
{
    [DisallowMultipleComponent]
    public sealed class LightingDemoController : MonoBehaviour
    {
        private const float GameplayPlaneZ = 0f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private LightEmitter2D controlledLight;
        [SerializeField] private LightEmitter2D secondaryLight;
        [SerializeField, Min(1f)] private float angleStep = 10f;

        private Vector2 pointerWorldPosition;
        private IlluminationSample pointerSample;
        private bool hasPointerWorldPosition;

        public void Initialize(
            Camera cameraToUse,
            LightEmitter2D primary,
            LightEmitter2D secondary)
        {
            targetCamera = cameraToUse;
            controlledLight = primary;
            secondaryLight = secondary;
        }

        private void Update()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (controlledLight == null || targetCamera == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                controlledLight.ToggleShape();
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                controlledLight.SectorAngle -= scroll * angleStep;
            }

            if (Input.GetKeyDown(KeyCode.L) && secondaryLight != null)
            {
                secondaryLight.SetEmitting(!secondaryLight.IsEmitting);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                controlledLight.Shape = LightShape2D.Sector;
                controlledLight.SectorAngle = 90f;
                controlledLight.Direction = Vector2.right;
            }

            hasPointerWorldPosition = TryGetPointerWorldPosition(out pointerWorldPosition);
            if (hasPointerWorldPosition)
            {
                controlledLight.SetDirectionTowards(pointerWorldPosition);
                pointerSample = IlluminationSystem.Sample(pointerWorldPosition);
            }
            else
            {
                pointerSample = IlluminationSample.None;
            }
        }

        private void OnGUI()
        {
            if (controlledLight == null)
            {
                return;
            }

            const float width = 370f;
            const float height = 300f;
            GUILayout.BeginArea(new Rect(16f, 16f, width, height), GUI.skin.box);
            GUILayout.Label("2.5D Candle Lighting Demo");
            GUILayout.Space(4f);
            GUILayout.Label("Move mouse: aim sector");
            GUILayout.Label("Mouse wheel: change sector angle");
            GUILayout.Label("Space: circle / sector    L: secondary light    R: reset");
            GUILayout.Space(8f);
            GUILayout.Label($"Shape: {controlledLight.Shape}");
            GUILayout.Label($"Sector angle: {controlledLight.SectorAngle:F1} degrees");
            GUILayout.Label($"Effective range: {controlledLight.EffectiveRange:F2}");
            GUILayout.Label(
                $"Area: {controlledLight.EffectiveArea:F2} " +
                $"(baseline {controlledLight.BaselineArea:F2})");
            GUILayout.Label(
                $"Focus: {controlledLight.FocusMultiplier:F2}x    " +
                $"Intensity: {controlledLight.CurrentIntensity:F2}");
            GUILayout.Label($"Light damage: {controlledLight.CurrentDamagePerSecond:F2} DPS");

            if (hasPointerWorldPosition)
            {
                GUILayout.Label(
                    $"Pointer sample: {(pointerSample.IsLit ? "LIT" : "DARK")}, " +
                    $"sources {pointerSample.SourceCount}, DPS {pointerSample.DamagePerSecond:F2}");
            }

            GUILayout.EndArea();
        }

        private bool TryGetPointerWorldPosition(out Vector2 worldPosition)
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            float denominator = ray.direction.z;
            if (Mathf.Abs(denominator) <= 0.0001f)
            {
                worldPosition = default;
                return false;
            }

            float distance = (GameplayPlaneZ - ray.origin.z) / denominator;
            if (distance < 0f)
            {
                worldPosition = default;
                return false;
            }

            Vector3 point = ray.GetPoint(distance);
            worldPosition = new Vector2(point.x, point.y);
            return true;
        }
    }
}
