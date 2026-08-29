using UnityEngine;

namespace Game.Lighting
{
    public static class LightGeometry2D
    {
        public const float FullCircleAngle = 360f;
        public const float DefaultMinimumSectorAngle = 10f;

        private const float Epsilon = 0.0001f;

        public static float ClampMinimumSectorAngle(float minimumSectorAngle)
        {
            if (!IsFinite(minimumSectorAngle))
            {
                return DefaultMinimumSectorAngle;
            }

            return Mathf.Clamp(minimumSectorAngle, DefaultMinimumSectorAngle, FullCircleAngle);
        }

        public static float ClampSectorAngle(float sectorAngle, float minimumSectorAngle)
        {
            float minimum = ClampMinimumSectorAngle(minimumSectorAngle);
            if (!IsFinite(sectorAngle))
            {
                return minimum;
            }

            return Mathf.Clamp(sectorAngle, minimum, FullCircleAngle);
        }

        public static float CalculateEqualAreaRange(float baseRadius, float sectorAngle)
        {
            float radius = SanitizeNonNegative(baseRadius);
            float angle = Mathf.Clamp(
                IsFinite(sectorAngle) ? sectorAngle : FullCircleAngle,
                1f,
                FullCircleAngle);

            return radius * Mathf.Sqrt(FullCircleAngle / angle);
        }

        public static float CalculateAttenuatedRange(float baseRadius, float sectorAngle)
        {
            float radius = SanitizeNonNegative(baseRadius);
            float angle = Mathf.Clamp(
                IsFinite(sectorAngle) ? sectorAngle : FullCircleAngle,
                DefaultMinimumSectorAngle,
                FullCircleAngle);
            float focus01 = (FullCircleAngle - angle) /
                (FullCircleAngle - DefaultMinimumSectorAngle);
            float rangeMultiplier = Mathf.Lerp(1f, 2f, Mathf.Clamp01(focus01));
            return radius * rangeMultiplier;
        }

        public static float CalculateCircleArea(float radius)
        {
            float sanitizedRadius = SanitizeNonNegative(radius);
            return Mathf.PI * sanitizedRadius * sanitizedRadius;
        }

        public static float CalculateSectorArea(float range, float sectorAngle)
        {
            float sanitizedRange = SanitizeNonNegative(range);
            float angle = Mathf.Clamp(
                IsFinite(sectorAngle) ? sectorAngle : FullCircleAngle,
                0f,
                FullCircleAngle);

            return angle / FullCircleAngle * Mathf.PI * sanitizedRange * sanitizedRange;
        }

        public static float CalculateFocus01(
            LightShape2D shape,
            float sectorAngle,
            float minimumSectorAngle)
        {
            if (shape != LightShape2D.Sector)
            {
                return 0f;
            }

            float minimum = ClampMinimumSectorAngle(minimumSectorAngle);
            if (minimum >= FullCircleAngle - Epsilon)
            {
                return 0f;
            }

            float angle = ClampSectorAngle(sectorAngle, minimum);
            return Mathf.Clamp01((FullCircleAngle - angle) / (FullCircleAngle - minimum));
        }

        public static Vector2 NormalizeDirection(Vector2 direction, Vector2 fallback)
        {
            if (IsFinite(direction) && direction.sqrMagnitude > Epsilon * Epsilon)
            {
                return direction.normalized;
            }

            if (IsFinite(fallback) && fallback.sqrMagnitude > Epsilon * Epsilon)
            {
                return fallback.normalized;
            }

            return Vector2.right;
        }

        public static bool Contains(
            Vector2 point,
            Vector2 origin,
            LightShape2D shape,
            Vector2 direction,
            float range,
            float sectorAngle)
        {
            float sanitizedRange = SanitizeNonNegative(range);
            if (sanitizedRange <= Epsilon)
            {
                return false;
            }

            Vector2 offset = point - origin;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > sanitizedRange * sanitizedRange + Epsilon)
            {
                return false;
            }

            if (shape != LightShape2D.Sector || sectorAngle >= FullCircleAngle - Epsilon)
            {
                return true;
            }

            if (distanceSquared <= Epsilon * Epsilon)
            {
                return true;
            }

            Vector2 normalizedDirection = NormalizeDirection(direction, Vector2.right);
            float angle = Mathf.Clamp(sectorAngle, 1f, FullCircleAngle);
            float cosineBoundary = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
            float directionDot = Vector2.Dot(normalizedDirection, offset.normalized);
            return directionDot + Epsilon >= cosineBoundary;
        }

        public static float EvaluateInfluence(
            Vector2 point,
            Vector2 origin,
            LightShape2D shape,
            Vector2 direction,
            float range,
            float sectorAngle,
            float edgeSoftness)
        {
            float sanitizedRange = SanitizeNonNegative(range);
            if (!Contains(point, origin, shape, direction, sanitizedRange, sectorAngle))
            {
                return 0f;
            }

            Vector2 offset = point - origin;
            float distance = offset.magnitude;
            float softness = Mathf.Clamp(SanitizeNonNegative(edgeSoftness), 0f, sanitizedRange);
            float radialInfluence = softness <= Epsilon
                ? 1f
                : Mathf.Clamp01((sanitizedRange - distance) / softness);

            if (shape != LightShape2D.Sector ||
                sectorAngle >= FullCircleAngle - Epsilon ||
                distance <= Epsilon)
            {
                return radialInfluence;
            }

            CalculateAngularCosines(
                sectorAngle,
                softness,
                sanitizedRange,
                out float outerCosine,
                out float innerCosine);

            Vector2 normalizedDirection = NormalizeDirection(direction, Vector2.right);
            float directionDot = Vector2.Dot(normalizedDirection, offset / distance);
            float angularInfluence = innerCosine - outerCosine <= Epsilon
                ? 1f
                : Mathf.Clamp01((directionDot - outerCosine) / (innerCosine - outerCosine));

            return radialInfluence * angularInfluence;
        }

        public static void CalculateAngularCosines(
            float sectorAngle,
            float edgeSoftness,
            float range,
            out float outerCosine,
            out float innerCosine)
        {
            float angle = Mathf.Clamp(
                IsFinite(sectorAngle) ? sectorAngle : FullCircleAngle,
                DefaultMinimumSectorAngle,
                FullCircleAngle);
            float sanitizedRange = Mathf.Max(Epsilon, SanitizeNonNegative(range));
            float softness = Mathf.Clamp(SanitizeNonNegative(edgeSoftness), 0f, sanitizedRange);
            float halfAngle = angle * 0.5f;
            float featherAngle = Mathf.Min(
                halfAngle,
                softness / sanitizedRange * Mathf.Rad2Deg);
            float innerHalfAngle = Mathf.Max(0f, halfAngle - featherAngle);

            outerCosine = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            innerCosine = Mathf.Cos(innerHalfAngle * Mathf.Deg2Rad);
        }

        private static float SanitizeNonNegative(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }
    }
}
