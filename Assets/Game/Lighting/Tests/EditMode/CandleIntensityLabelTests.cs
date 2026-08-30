using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Lighting.Tests
{
    public sealed class CandleIntensityLabelTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            IlluminationSystem.ResetForTests();
        }

        [Test]
        public void RefreshNow_CreatesGreenWorldSpaceLabelAndUpdatesIntensity()
        {
            Fixture fixture = CreateFixture();
            fixture.emitter.SectorAngle = 90f;

            fixture.label.RefreshNow();

            Assert.That(fixture.label.LabelCanvas, Is.Not.Null);
            Assert.That(fixture.label.LabelCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(fixture.label.LabelCanvas.sortingOrder, Is.EqualTo(25));
            Text text = fixture.label.LabelText;
            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Is.EqualTo(CandleIntensityLabel.FormatIntensity(
                fixture.emitter.CurrentIntensity)));
            Assert.That(text.color.r, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(text.color.g, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(text.color.b, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(text.raycastTarget, Is.False);
            string initialText = text.text;

            fixture.emitter.SectorAngle = fixture.emitter.MinimumSectorAngle;
            fixture.label.RefreshNow();

            Assert.That(text.text, Is.Not.EqualTo(initialText));
            Assert.That(text.text, Is.EqualTo(CandleIntensityLabel.FormatIntensity(
                fixture.emitter.CurrentIntensity)));
        }

        [Test]
        public void RefreshNow_PositionsLabelOnInnerCircleEdgeInAimDirection()
        {
            Fixture fixture = CreateFixture();
            fixture.emitter.transform.position = new Vector3(2f, 3f, -0.15f);
            fixture.emitter.BaseRadius = 4f;
            fixture.innerCircle.RadiusMultiplier = 0.5f;
            fixture.emitter.Direction = Vector2.up;

            fixture.label.RefreshNow();

            Vector3 position = fixture.label.LabelCanvas.transform.position;
            Assert.That(position.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(4.75f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(-0.15f).Within(0.0001f));

            fixture.emitter.Direction = Vector2.left;
            fixture.label.RefreshNow();

            position = fixture.label.LabelCanvas.transform.position;
            Assert.That(position.x, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void CalculateSize_GrowsForRiseAndBackstepButNotLateralMotion()
        {
            float unchanged = CandleIntensityLabel.CalculateSize(
                0.01f,
                0.2f,
                Vector3.zero,
                new Vector3(10f, 0f, 0f),
                Vector3.forward,
                Vector3.up);
            float grown = CandleIntensityLabel.CalculateSize(
                0.01f,
                0.2f,
                Vector3.zero,
                new Vector3(10f, 2f, -3f),
                Vector3.forward,
                Vector3.up);

            Assert.That(unchanged, Is.EqualTo(0.01f).Within(0.000001f));
            Assert.That(
                grown,
                Is.EqualTo(0.01f * (1f + 0.2f * Mathf.Sqrt(13f))).Within(0.000001f));
        }

        [Test]
        public void RefreshNow_ShowsOnlyOperationalSectorLight()
        {
            Fixture fixture = CreateFixture();

            fixture.label.RefreshNow();
            Assert.That(fixture.label.LabelCanvas.enabled, Is.True);

            fixture.emitter.SetEmitting(false);
            fixture.label.RefreshNow();
            Assert.That(fixture.label.LabelCanvas.enabled, Is.False);

            fixture.emitter.SetEmitting(true);
            fixture.emitter.Shape = LightShape2D.Circle;
            fixture.label.RefreshNow();
            Assert.That(fixture.label.LabelCanvas.enabled, Is.False);
        }

        private Fixture CreateFixture()
        {
            IlluminationSystem.ResetForTests();
            GameObject cameraObject = new GameObject("Intensity Label Camera");
            createdObjects.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, -3f, -7f),
                Quaternion.Euler(-20f, 0f, 0f));

            GameObject emitterObject = new GameObject("Intensity Label Emitter");
            createdObjects.Add(emitterObject);
            LightEmitter2D emitter = emitterObject.AddComponent<LightEmitter2D>();
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 20f;
            emitter.SectorAngle = 90f;
            emitter.BaseRadius = 4f;
            emitter.BaseIntensity = 1f;
            emitter.MaximumFocusMultiplier = 2.25f;
            emitter.SetEmitting(true);
            InnerCircleLight2D innerCircle = emitterObject.AddComponent<InnerCircleLight2D>();
            innerCircle.RadiusMultiplier = 0.5f;

            GameObject labelObject = new GameObject("Intensity Label Host");
            createdObjects.Add(labelObject);
            CandleIntensityLabel label = labelObject.AddComponent<CandleIntensityLabel>();
            SetPrivateField(label, "targetCamera", camera);
            SetPrivateField(label, "emitter", emitter);
            SetPrivateField(label, "innerCircle", innerCircle);
            return new Fixture(label, emitter, innerCircle);
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private readonly struct Fixture
        {
            public readonly CandleIntensityLabel label;
            public readonly LightEmitter2D emitter;
            public readonly InnerCircleLight2D innerCircle;

            public Fixture(
                CandleIntensityLabel label,
                LightEmitter2D emitter,
                InnerCircleLight2D innerCircle)
            {
                this.label = label;
                this.emitter = emitter;
                this.innerCircle = innerCircle;
            }
        }
    }
}
