using UnityEngine;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Light Emitter 2D")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class LightEmitter2D : MonoBehaviour
    {
        private const float MinimumRadius = 0.01f;
        private const int GizmoSegments = 64;

        [Header("Shape")]
        [SerializeField] private LightShape2D shape = LightShape2D.Circle;
        [SerializeField, Min(MinimumRadius)] private float baseRadius = 4f;
        [SerializeField, Range(1f, LightGeometry2D.FullCircleAngle)] private float sectorAngle = 90f;
        [SerializeField, Range(1f, LightGeometry2D.FullCircleAngle)] private float minimumSectorAngle = 60f;
        [SerializeField] private Vector2 direction = Vector2.right;

        [Header("Output")]
        [SerializeField, Min(0f)] private float baseIntensity = 1f;
        [SerializeField, Min(0f)] private float baseDamagePerSecond = 10f;
        [SerializeField, Min(1f)] private float maximumFocusMultiplier = 2.25f;
        [SerializeField, Min(0f)] private float edgeSoftness = 0.35f;
        [SerializeField] private bool emitting = true;

        [Header("Editor")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.72f, 0.2f, 0.9f);

        public LightShape2D Shape
        {
            get => shape;
            set
            {
                if (shape == value)
                {
                    return;
                }

                shape = value;
                NotifyChanged();
            }
        }

        public float BaseRadius
        {
            get => baseRadius;
            set
            {
                float sanitizedValue = SanitizeAtLeast(value, MinimumRadius, MinimumRadius);
                if (Mathf.Approximately(baseRadius, sanitizedValue))
                {
                    return;
                }

                baseRadius = sanitizedValue;
                NotifyChanged();
            }
        }

        public float SectorAngle
        {
            get => sectorAngle;
            set
            {
                float sanitizedValue = LightGeometry2D.ClampSectorAngle(value, minimumSectorAngle);
                if (Mathf.Approximately(sectorAngle, sanitizedValue))
                {
                    return;
                }

                sectorAngle = sanitizedValue;
                NotifyChanged();
            }
        }

        public float MinimumSectorAngle
        {
            get => minimumSectorAngle;
            set
            {
                float sanitizedValue = LightGeometry2D.ClampMinimumSectorAngle(value);
                if (Mathf.Approximately(minimumSectorAngle, sanitizedValue))
                {
                    return;
                }

                minimumSectorAngle = sanitizedValue;
                sectorAngle = LightGeometry2D.ClampSectorAngle(sectorAngle, minimumSectorAngle);
                NotifyChanged();
            }
        }

        public Vector2 Direction
        {
            get => direction;
            set
            {
                Vector2 sanitizedValue = LightGeometry2D.NormalizeDirection(value, direction);
                if ((direction - sanitizedValue).sqrMagnitude <= 0.000001f)
                {
                    return;
                }

                direction = sanitizedValue;
                NotifyChanged();
            }
        }

        public float BaseIntensity
        {
            get => baseIntensity;
            set
            {
                float sanitizedValue = SanitizeAtLeast(value, 0f, 0f);
                if (Mathf.Approximately(baseIntensity, sanitizedValue))
                {
                    return;
                }

                baseIntensity = sanitizedValue;
                NotifyChanged();
            }
        }

        public float BaseDamagePerSecond
        {
            get => baseDamagePerSecond;
            set
            {
                float sanitizedValue = SanitizeAtLeast(value, 0f, 0f);
                if (Mathf.Approximately(baseDamagePerSecond, sanitizedValue))
                {
                    return;
                }

                baseDamagePerSecond = sanitizedValue;
                NotifyChanged();
            }
        }

        public float MaximumFocusMultiplier
        {
            get => maximumFocusMultiplier;
            set
            {
                float sanitizedValue = SanitizeAtLeast(value, 1f, 1f);
                if (Mathf.Approximately(maximumFocusMultiplier, sanitizedValue))
                {
                    return;
                }

                maximumFocusMultiplier = sanitizedValue;
                NotifyChanged();
            }
        }

        public float EdgeSoftness
        {
            get => edgeSoftness;
            set
            {
                float sanitizedValue = SanitizeAtLeast(value, 0f, 0f);
                if (Mathf.Approximately(edgeSoftness, sanitizedValue))
                {
                    return;
                }

                edgeSoftness = sanitizedValue;
                NotifyChanged();
            }
        }

        public bool IsEmitting => emitting;

        public bool IsOperational =>
            isActiveAndEnabled &&
            emitting &&
            baseRadius >= MinimumRadius &&
            baseIntensity > 0f;

        public Vector2 WorldPosition => new Vector2(transform.position.x, transform.position.y);

        public float Focus01 => LightGeometry2D.CalculateFocus01(shape, sectorAngle, minimumSectorAngle);

        public float FocusMultiplier => Mathf.Lerp(1f, maximumFocusMultiplier, Focus01);

        public float EffectiveRange => shape == LightShape2D.Circle
            ? baseRadius
            : LightGeometry2D.CalculateEqualAreaRange(baseRadius, sectorAngle);

        public float MaximumEffectiveRange =>
            LightGeometry2D.CalculateEqualAreaRange(baseRadius, minimumSectorAngle);

        public float BaselineArea => LightGeometry2D.CalculateCircleArea(baseRadius);

        public float EffectiveArea => shape == LightShape2D.Circle
            ? BaselineArea
            : LightGeometry2D.CalculateSectorArea(EffectiveRange, sectorAngle);

        public float CurrentIntensity => baseIntensity * FocusMultiplier;

        public float CurrentDamagePerSecond => baseDamagePerSecond * FocusMultiplier;

        public float DirectionAngleDegrees => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        private void OnEnable()
        {
            SanitizeSerializedFields();
            IlluminationSystem.Register(this);
        }

        private void OnDisable()
        {
            IlluminationSystem.Unregister(this);
        }

        private void OnValidate()
        {
            SanitizeSerializedFields();
            NotifyChanged();
        }

        public void SetEmitting(bool value)
        {
            if (emitting == value)
            {
                return;
            }

            emitting = value;
            NotifyChanged();
        }

        public void ToggleShape()
        {
            Shape = shape == LightShape2D.Circle
                ? LightShape2D.Sector
                : LightShape2D.Circle;
        }

        public void SetDirectionAngle(float angleDegrees)
        {
            if (float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees))
            {
                return;
            }

            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            Direction = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        }

        public void SetDirectionTowards(Vector2 worldPosition)
        {
            Vector2 offset = worldPosition - WorldPosition;
            if (offset.sqrMagnitude > 0.000001f)
            {
                Direction = offset;
            }
        }

        public bool Contains(Vector2 worldPosition)
        {
            return IsOperational && LightGeometry2D.Contains(
                worldPosition,
                WorldPosition,
                shape,
                direction,
                EffectiveRange,
                sectorAngle);
        }

        public float EvaluateInfluence(Vector2 worldPosition)
        {
            if (!IsOperational)
            {
                return 0f;
            }

            return LightGeometry2D.EvaluateInfluence(
                worldPosition,
                WorldPosition,
                shape,
                direction,
                EffectiveRange,
                sectorAngle,
                edgeSoftness);
        }

        public float EvaluateIntensity(Vector2 worldPosition)
        {
            return CurrentIntensity * EvaluateInfluence(worldPosition);
        }

        public float EvaluateDamagePerSecond(Vector2 worldPosition)
        {
            return CurrentDamagePerSecond * EvaluateInfluence(worldPosition);
        }

        private void OnDrawGizmosSelected()
        {
            SanitizeSerializedFields();
            Gizmos.color = gizmoColor;

            Vector3 origin = transform.position;
            float range = EffectiveRange;
            if (shape == LightShape2D.Circle || sectorAngle >= LightGeometry2D.FullCircleAngle - 0.001f)
            {
                DrawArc(origin, range, 0f, LightGeometry2D.FullCircleAngle, true);
                return;
            }

            float centerAngle = DirectionAngleDegrees;
            float startAngle = centerAngle - sectorAngle * 0.5f;
            float endAngle = centerAngle + sectorAngle * 0.5f;
            Vector3 startPoint = PointOnCircle(origin, range, startAngle);
            Vector3 endPoint = PointOnCircle(origin, range, endAngle);
            Gizmos.DrawLine(origin, startPoint);
            Gizmos.DrawLine(origin, endPoint);
            DrawArc(origin, range, startAngle, sectorAngle, false);
        }

        private static void DrawArc(
            Vector3 origin,
            float radius,
            float startAngle,
            float angle,
            bool closeLoop)
        {
            int segments = Mathf.Max(2, Mathf.CeilToInt(GizmoSegments * angle / 360f));
            Vector3 previous = PointOnCircle(origin, radius, startAngle);
            for (int i = 1; i <= segments; i++)
            {
                float stepAngle = startAngle + angle * i / segments;
                Vector3 current = PointOnCircle(origin, radius, stepAngle);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }

            if (closeLoop)
            {
                Gizmos.DrawLine(previous, PointOnCircle(origin, radius, startAngle));
            }
        }

        private static Vector3 PointOnCircle(Vector3 origin, float radius, float angleDegrees)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            return origin + new Vector3(
                Mathf.Cos(angleRadians) * radius,
                Mathf.Sin(angleRadians) * radius,
                0f);
        }

        private void SanitizeSerializedFields()
        {
            if (!System.Enum.IsDefined(typeof(LightShape2D), shape))
            {
                shape = LightShape2D.Circle;
            }

            baseRadius = SanitizeAtLeast(baseRadius, MinimumRadius, MinimumRadius);
            minimumSectorAngle = LightGeometry2D.ClampMinimumSectorAngle(minimumSectorAngle);
            sectorAngle = LightGeometry2D.ClampSectorAngle(sectorAngle, minimumSectorAngle);
            direction = LightGeometry2D.NormalizeDirection(direction, Vector2.right);
            baseIntensity = SanitizeAtLeast(baseIntensity, 0f, 0f);
            baseDamagePerSecond = SanitizeAtLeast(baseDamagePerSecond, 0f, 0f);
            maximumFocusMultiplier = SanitizeAtLeast(maximumFocusMultiplier, 1f, 1f);
            edgeSoftness = SanitizeAtLeast(edgeSoftness, 0f, 0f);
        }

        private void NotifyChanged()
        {
            IlluminationSystem.NotifyEmitterChanged(this);
        }

        private static float SanitizeAtLeast(float value, float minimum, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Max(minimum, value);
        }
    }
}
