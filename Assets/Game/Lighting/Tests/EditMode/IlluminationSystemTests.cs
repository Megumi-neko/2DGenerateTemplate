using System.Collections.Generic;
using System.Reflection;
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

        [Test]
        public void InnerCircle_SynchronizesBaseOutputButStaysCircle()
        {
            LightEmitter2D source = CreateEmitter(Vector2.zero, 4f, 0.8f, 10f);
            source.Shape = LightShape2D.Sector;
            source.MinimumSectorAngle = 60f;
            source.SectorAngle = 60f;
            source.MaximumFocusMultiplier = 2.5f;

            InnerCircleLight2D innerCircle = source.gameObject.AddComponent<InnerCircleLight2D>();
            innerCircle.RadiusMultiplier = 0.5f;
            innerCircle.SynchronizeNow();
            LightEmitter2D inner = innerCircle.InnerEmitter;

            Assert.That(inner, Is.Not.Null);
            Assert.That(inner.Shape, Is.EqualTo(LightShape2D.Circle));
            Assert.That(inner.EffectiveRange, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(inner.BaseIntensity, Is.EqualTo(source.BaseIntensity).Within(0.0001f));
            Assert.That(
                inner.BaseDamagePerSecond,
                Is.EqualTo(source.BaseDamagePerSecond).Within(0.0001f));
            Assert.That(inner.CurrentIntensity, Is.EqualTo(source.BaseIntensity).Within(0.0001f));

            source.SetEmitting(false);
            innerCircle.SynchronizeNow();
            Assert.That(inner.IsEmitting, Is.False);
        }

        [Test]
        public void InnerCircle_RadiusMultiplierIsAlwaysSmallerThanOne()
        {
            LightEmitter2D source = CreateEmitter(Vector2.zero, 4f, 1f, 10f);
            InnerCircleLight2D innerCircle = source.gameObject.AddComponent<InnerCircleLight2D>();

            innerCircle.RadiusMultiplier = 2f;
            innerCircle.SynchronizeNow();

            Assert.That(innerCircle.RadiusMultiplier, Is.LessThan(1f));
            Assert.That(innerCircle.InnerRadius, Is.LessThan(source.BaseRadius));
        }

        [Test]
        public void CandleFocusController_TogglesAimLockState()
        {
            GameObject controllerObject = new GameObject("Test Focus Controller");
            createdObjects.Add(controllerObject);
            CandleFocusController controller = controllerObject.AddComponent<CandleFocusController>();

            Assert.That(controller.IsAimLocked, Is.False);
            controller.ToggleAimLock();
            Assert.That(controller.IsAimLocked, Is.True);
            controller.ToggleAimLock();
            Assert.That(controller.IsAimLocked, Is.False);
        }

        [Test]
        public void StageLightingBootstrap_UsesSpriteVisualInsteadOfGeneratedQuads()
        {
            GameObject bootstrapObject = new GameObject("Test Stage Lighting Bootstrap");
            createdObjects.Add(bootstrapObject);
            StageLightingBootstrap bootstrap = bootstrapObject.AddComponent<StageLightingBootstrap>();
            Sprite candleSprite = Resources.Load<Sprite>("PowerTexture/09416f3344d521839bd708038ebc7229");

            Assert.That(candleSprite, Is.Not.Null);

            SetPrivateField(bootstrap, "candleSprite", candleSprite);
            SetPrivateField(bootstrap, "flameSprite", null);
            InvokePrivateMethod(bootstrap, "EnsureCandle");

            Transform visualRoot = bootstrap.CandleEmitter.transform.Find("Stage 1 Central Candle Visual");
            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(visualRoot.parent, Is.SameAs(bootstrap.CandleEmitter.transform));

            SpriteRenderer[] spriteRenderers =
                visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(spriteRenderers, Has.Length.EqualTo(1));
            Assert.That(spriteRenderers[0].sprite, Is.SameAs(candleSprite));
            Assert.That(spriteRenderers[0].sortingOrder, Is.EqualTo(10));
            Assert.That(visualRoot.GetComponentsInChildren<MeshRenderer>(true), Is.Empty);
            Assert.That(bootstrap.CandleEmitter, Is.Not.Null);
            Assert.That(bootstrap.InnerCircle, Is.Not.Null);
        }

        [Test]
        public void StageLightingBootstrap_RefreshesSpriteVisualWithoutDuplicates()
        {
            GameObject bootstrapObject = new GameObject("Test Stage Lighting Bootstrap");
            createdObjects.Add(bootstrapObject);
            StageLightingBootstrap bootstrap = bootstrapObject.AddComponent<StageLightingBootstrap>();
            Sprite candleSprite = Resources.Load<Sprite>("PowerTexture/09416f3344d521839bd708038ebc7229");

            Assert.That(candleSprite, Is.Not.Null);

            SetPrivateField(bootstrap, "candleSprite", candleSprite);
            SetPrivateField(bootstrap, "flameSprite", null);
            InvokePrivateMethod(bootstrap, "EnsureCandle");

            bootstrap.RefreshCandleVisual();

            Transform secondVisualRoot = bootstrap.CandleEmitter.transform.Find(
                "Stage 1 Central Candle Visual");
            SpriteRenderer[] spriteRenderers =
                bootstrap.CandleEmitter.GetComponentsInChildren<SpriteRenderer>(true);

            Assert.That(secondVisualRoot, Is.Not.Null);
            Assert.That(spriteRenderers, Has.Length.EqualTo(1));
            Assert.That(spriteRenderers[0].sprite, Is.SameAs(candleSprite));
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod<T>(T target, string methodName)
        {
            MethodInfo method = typeof(T).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
            method.Invoke(target, null);
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
