using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Lighting.Tests
{
    public sealed class IlluminationSystemTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            IlluminationSystem.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
            IlluminationSystem.ResetForTests();
        }

        [Test]
        public void Sample_UsesStrongestIntensityAndAddsDamage()
        {
            LightEmitter2D first = CreateEmitter(Vector2.zero, 5f, 0.5f, 10f);
            LightEmitter2D second = CreateEmitter(Vector2.zero, 5f, 1f, 4f);

            IlluminationSample sample = IlluminationSystem.Sample(Vector2.zero);

            Assert.That(sample.IsLit, Is.True);
            Assert.That(sample.SourceCount, Is.EqualTo(2));
            Assert.That(sample.Intensity, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(sample.DamagePerSecond, Is.EqualTo(14f).Within(0.0001f));
            Assert.That(sample.StrongestSource, Is.SameAs(second));
            Assert.That(first, Is.Not.Null);
        }

        [Test]
        public void DisabledEmitter_IsRemovedFromQueries()
        {
            LightEmitter2D emitter = CreateEmitter(Vector2.zero, 5f, 1f, 10f);
            Assert.That(IlluminationSystem.IsLit(Vector2.zero), Is.True);

            emitter.enabled = false;

            Assert.That(IlluminationSystem.IsLit(Vector2.zero), Is.False);
            Assert.That(IlluminationSystem.RegisteredEmitters.Count, Is.Zero);
        }

        [Test]
        public void StoppedEmitter_RemainsRegisteredButDoesNotContribute()
        {
            LightEmitter2D emitter = CreateEmitter(Vector2.zero, 5f, 1f, 10f);

            emitter.SetEmitting(false);

            Assert.That(IlluminationSystem.RegisteredEmitters.Count, Is.EqualTo(1));
            Assert.That(IlluminationSystem.Sample(Vector2.zero).IsLit, Is.False);
        }

        [Test]
        public void DestroyedEmitter_IsUnregistered()
        {
            LightEmitter2D emitter = CreateEmitter(Vector2.zero, 5f, 1f, 10f);
            GameObject emitterObject = emitter.gameObject;

            Object.DestroyImmediate(emitterObject);
            createdObjects.Remove(emitterObject);

            Assert.That(IlluminationSystem.RegisteredEmitters.Count, Is.Zero);
            Assert.That(IlluminationSystem.Sample(Vector2.zero).IsLit, Is.False);
        }

        [Test]
        public void MinimumSectorAngle_AppliesMaximumFocusMultiplierAndPreservesArea()
        {
            LightEmitter2D emitter = CreateEmitter(Vector2.zero, 4f, 1f, 10f);
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 60f;
            emitter.SectorAngle = 60f;
            emitter.MaximumFocusMultiplier = 2.5f;

            Assert.That(emitter.FocusMultiplier, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(emitter.CurrentDamagePerSecond, Is.EqualTo(25f).Within(0.0001f));
            Assert.That(emitter.EffectiveArea, Is.EqualTo(emitter.BaselineArea).Within(0.0001f));
        }

        [Test]
        public void SettingZeroDirection_PreservesLastValidDirection()
        {
            LightEmitter2D emitter = CreateEmitter(Vector2.zero, 4f, 1f, 10f);
            emitter.Direction = Vector2.up;

            emitter.Direction = Vector2.zero;

            Assert.That(emitter.Direction, Is.EqualTo(Vector2.up));
        }

        private LightEmitter2D CreateEmitter(
            Vector2 position,
            float radius,
            float intensity,
            float damagePerSecond)
        {
            GameObject emitterObject = new GameObject("Test Light Emitter");
            createdObjects.Add(emitterObject);
            emitterObject.transform.position = new Vector3(position.x, position.y, 0f);

            LightEmitter2D emitter = emitterObject.AddComponent<LightEmitter2D>();
            emitter.Shape = LightShape2D.Circle;
            emitter.BaseRadius = radius;
            emitter.BaseIntensity = intensity;
            emitter.BaseDamagePerSecond = damagePerSecond;
            emitter.EdgeSoftness = 0f;
            emitter.SetEmitting(true);
            return emitter;
        }
    }
}
