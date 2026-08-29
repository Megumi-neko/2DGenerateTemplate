using NUnit.Framework;
using UnityEngine;

namespace Game.Lighting.Tests
{
    public sealed class LightGeometry2DTests
    {
        private const float Radius = 4f;

        [TestCase(360f)]
        [TestCase(180f)]
        [TestCase(90f)]
        [TestCase(60f)]
        public void EqualAreaRange_PreservesBaselineCircleArea(float sectorAngle)
        {
            float range = LightGeometry2D.CalculateEqualAreaRange(Radius, sectorAngle);
            float circleArea = LightGeometry2D.CalculateCircleArea(Radius);
            float sectorArea = LightGeometry2D.CalculateSectorArea(range, sectorAngle);

            Assert.That(sectorArea, Is.EqualTo(circleArea).Within(0.0001f));
        }

        [Test]
        public void CircleContains_IncludesBoundaryAndRejectsOutsidePoint()
        {
            Assert.That(
                LightGeometry2D.Contains(
                    new Vector2(Radius, 0f),
                    Vector2.zero,
                    LightShape2D.Circle,
                    Vector2.right,
                    Radius,
                    360f),
                Is.True);
            Assert.That(
                LightGeometry2D.Contains(
                    new Vector2(Radius + 0.01f, 0f),
                    Vector2.zero,
                    LightShape2D.Circle,
                    Vector2.right,
                    Radius,
                    360f),
                Is.False);
        }

        [Test]
        public void SectorContains_UsesDirectionAndHalfAngle()
        {
            Assert.That(
                LightGeometry2D.Contains(
                    new Vector2(2f, 0f),
                    Vector2.zero,
                    LightShape2D.Sector,
                    Vector2.right,
                    Radius,
                    90f),
                Is.True);
            Assert.That(
                LightGeometry2D.Contains(
                    new Vector2(1f, 1f),
                    Vector2.zero,
                    LightShape2D.Sector,
                    Vector2.right,
                    Radius,
                    90f),
                Is.True);
            Assert.That(
                LightGeometry2D.Contains(
                    new Vector2(0f, 2f),
                    Vector2.zero,
                    LightShape2D.Sector,
                    Vector2.right,
                    Radius,
                    90f),
                Is.False);
        }

        [Test]
        public void NormalizeDirection_UsesFallbackForZeroVector()
        {
            Vector2 result = LightGeometry2D.NormalizeDirection(Vector2.zero, Vector2.up);

            Assert.That(result, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void EvaluateInfluence_FadesInsideRadialEdge()
        {
            float influence = LightGeometry2D.EvaluateInfluence(
                new Vector2(3.5f, 0f),
                Vector2.zero,
                LightShape2D.Circle,
                Vector2.right,
                Radius,
                360f,
                1f);

            Assert.That(influence, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void FocusAmount_ReachesOneAtConfiguredMinimumAngle()
        {
            float focus = LightGeometry2D.CalculateFocus01(
                LightShape2D.Sector,
                60f,
                60f);

            Assert.That(focus, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                LightGeometry2D.CalculateFocus01(LightShape2D.Circle, 60f, 60f),
                Is.Zero);
        }


[Test]
        public void AttenuatedRange_IsBoundedAndLosesAreaAtMinimumAngle()
        {
            float fullCircleRange = LightGeometry2D.CalculateAttenuatedRange(Radius, 360f);
            float minimumRange = LightGeometry2D.CalculateAttenuatedRange(Radius, 10f);
            float narrowArea = LightGeometry2D.CalculateSectorArea(minimumRange, 10f);
            float circleArea = LightGeometry2D.CalculateCircleArea(Radius);

            Assert.That(fullCircleRange, Is.EqualTo(Radius).Within(0.0001f));
            Assert.That(minimumRange, Is.EqualTo(Radius * 2f).Within(0.0001f));
            Assert.That(minimumRange, Is.LessThanOrEqualTo(Radius * 2f));
            Assert.That(narrowArea, Is.LessThan(circleArea));
        }

        [Test]
        public void ClampMinimumSectorAngle_UsesTenDegreeFloor()
        {
            Assert.That(LightGeometry2D.ClampMinimumSectorAngle(1f), Is.EqualTo(10f));
            Assert.That(LightGeometry2D.ClampSectorAngle(1f, 10f), Is.EqualTo(10f));
        }
}
}
