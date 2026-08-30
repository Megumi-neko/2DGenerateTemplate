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
        public void MinimumSectorAngle_AppliesMaximumFocusMultiplierAndAttenuatesArea()
        {
            LightEmitter2D emitter = CreateEmitter(Vector2.zero, 4f, 1f, 10f);
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 10f;
            emitter.SectorAngle = 10f;
            emitter.MaximumFocusMultiplier = 2.5f;

            Assert.That(emitter.FocusMultiplier, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(emitter.CurrentDamagePerSecond, Is.EqualTo(25f).Within(0.0001f));
            Assert.That(emitter.EffectiveRange, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(emitter.EffectiveArea, Is.LessThan(emitter.BaselineArea));
        }

[Test]
        public void MaximumEffectiveRange_UsesBoundedMinimumSectorAngle()
        {
            LightEmitter2D emitter = CreateEmitter(Vector2.zero, 5f, 1f, 10f);
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 10f;
            emitter.SectorAngle = 90f;

            Assert.That(emitter.MaximumEffectiveRange, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(emitter.MaximumEffectiveRange, Is.GreaterThan(emitter.EffectiveRange));
            Assert.That(emitter.MaximumEffectiveRange, Is.LessThanOrEqualTo(10f));
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
        public void CandleFocusController_LockFreezesSectorAngleInput()
        {
            GameObject controllerObject = new GameObject("Test Locked Focus Controller");
            createdObjects.Add(controllerObject);
            LightEmitter2D emitter = controllerObject.AddComponent<LightEmitter2D>();
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 36f;
            emitter.SectorAngle = 90f;
            CandleFocusController controller =
                controllerObject.AddComponent<CandleFocusController>();
            controller.Initialize(null, emitter);

            controller.SetAimLocked(true);
            controller.ApplySectorAngleInput(1f);
            Assert.That(emitter.SectorAngle, Is.EqualTo(90f));

            controller.SetAimLocked(false);
            controller.ApplySectorAngleInput(1f);
            Assert.That(emitter.SectorAngle, Is.EqualTo(80f));
        }

        [Test]
        public void StageLightingBootstrap_UsesAuthoredCandleAndConfiguresInitialCircleRadius()
        {
            GameObject bootstrapObject = new GameObject("Test Authored Stage Lighting");
            createdObjects.Add(bootstrapObject);
            StageLightingBootstrap bootstrap =
                bootstrapObject.AddComponent<StageLightingBootstrap>();
            GameObject candle = CreateAuthoredCandle(bootstrapObject.transform);
            SetPrivateField(bootstrap, "centralCandle", candle);
            SetPrivateField(bootstrap, "initialShape", LightShape2D.Circle);
            SetPrivateField(bootstrap, "baseRadius", 7.5f);
            SetPrivateField(bootstrap, "innerRadiusMultiplier", 0.4f);

            InvokePrivateMethod(bootstrap, "EnsureCandle");

            Assert.That(bootstrap.CandleEmitter.gameObject, Is.SameAs(candle));
            Assert.That(bootstrap.CandleEmitter.Shape, Is.EqualTo(LightShape2D.Circle));
            Assert.That(bootstrap.CandleEmitter.BaseRadius, Is.EqualTo(7.5f));
            Assert.That(bootstrap.CandleEmitter.EffectiveRange, Is.EqualTo(7.5f));
            Assert.That(bootstrap.InnerCircle.InnerRadius, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(candle.transform.Find("Visual"), Is.Not.Null);
            Assert.That(
                candle.transform.Find("Stage 1 Central Candle Visual"),
                Is.Null);
        }

        [Test]
        public void StageLightingBootstrap_DoesNotDuplicateAuthoredVisual()
        {
            GameObject bootstrapObject = new GameObject("Test Authored Candle Visual");
            createdObjects.Add(bootstrapObject);
            StageLightingBootstrap bootstrap =
                bootstrapObject.AddComponent<StageLightingBootstrap>();
            GameObject candle = CreateAuthoredCandle(bootstrapObject.transform);
            Transform authoredVisual = candle.transform.Find("Visual");
            SetPrivateField(bootstrap, "centralCandle", candle);
            SetPrivateField(bootstrap, "candleSprite", Resources.Load<Sprite>(
                "PowerTexture/09416f3344d521839bd708038ebc7229"));

            InvokePrivateMethod(bootstrap, "EnsureCandle");
            bootstrap.RefreshCandleVisual();

            Assert.That(candle.transform.Find("Visual"), Is.SameAs(authoredVisual));
            Assert.That(
                candle.GetComponentsInChildren<SpriteRenderer>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                candle.transform.Find("Stage 1 Central Candle Visual"),
                Is.Null);
        }

        [Test]
        public void StageLightingBootstrap_UpgradesRangeAndIntensityIndependently()
        {
            GameObject bootstrapObject = new GameObject("Test Stage Lighting Upgrades");
            createdObjects.Add(bootstrapObject);
            StageLightingBootstrap bootstrap = bootstrapObject.AddComponent<StageLightingBootstrap>();
            GameObject candle = CreateAuthoredCandle(bootstrapObject.transform);
            SetPrivateField(bootstrap, "centralCandle", candle);
            InvokePrivateMethod(bootstrap, "EnsureCandle");

            float initialRadius = bootstrap.CandleEmitter.BaseRadius;
            float initialIntensity = bootstrap.CandleEmitter.BaseIntensity;
            float initialDamage = bootstrap.CandleEmitter.BaseDamagePerSecond;

            Assert.That(bootstrap.UpgradeRange(), Is.True);
            Assert.That(bootstrap.CandleEmitter.BaseRadius, Is.EqualTo(initialRadius + 0.25f));
            Assert.That(bootstrap.CandleEmitter.BaseIntensity, Is.EqualTo(initialIntensity));
            Assert.That(bootstrap.CandleEmitter.BaseDamagePerSecond, Is.EqualTo(initialDamage));
            Assert.That(bootstrap.RangeUpgradeLevel, Is.EqualTo(1));
            Assert.That(bootstrap.IntensityUpgradeLevel, Is.Zero);

            Assert.That(bootstrap.UpgradeIntensity(), Is.True);
            Assert.That(bootstrap.CandleEmitter.BaseIntensity, Is.EqualTo(initialIntensity + 0.075f));
            Assert.That(bootstrap.CandleEmitter.BaseDamagePerSecond, Is.EqualTo(initialDamage + 0.9f));
            Assert.That(bootstrap.IntensityUpgradeLevel, Is.EqualTo(1));
        }

        [Test]
        public void StageLightingBootstrap_StopsUpgradesAtConfiguredMaximum()
        {
            GameObject bootstrapObject = new GameObject("Test Stage Lighting Upgrade Limits");
            createdObjects.Add(bootstrapObject);
            StageLightingBootstrap bootstrap = bootstrapObject.AddComponent<StageLightingBootstrap>();
            GameObject candle = CreateAuthoredCandle(bootstrapObject.transform);
            SetPrivateField(bootstrap, "centralCandle", candle);
            InvokePrivateMethod(bootstrap, "EnsureCandle");

            for (int i = 0; i < 10; i++)
            {
                Assert.That(bootstrap.UpgradeRange(), Is.True);
                Assert.That(bootstrap.UpgradeIntensity(), Is.True);
            }
            float radiusAtLimit = bootstrap.CandleEmitter.BaseRadius;
            float intensityAtLimit = bootstrap.CandleEmitter.BaseIntensity;
            float damageAtLimit = bootstrap.CandleEmitter.BaseDamagePerSecond;

            Assert.That(bootstrap.UpgradeRange(), Is.False);
            Assert.That(bootstrap.UpgradeIntensity(), Is.False);
            Assert.That(bootstrap.CandleEmitter.BaseRadius, Is.EqualTo(radiusAtLimit));
            Assert.That(bootstrap.CandleEmitter.BaseIntensity, Is.EqualTo(intensityAtLimit));
            Assert.That(bootstrap.CandleEmitter.BaseDamagePerSecond, Is.EqualTo(damageAtLimit));
            Assert.That(bootstrap.RangeUpgradeLevel, Is.EqualTo(10));
            Assert.That(bootstrap.IntensityUpgradeLevel, Is.EqualTo(10));
        }

[Test]
        public void StageLightingCameraFramer_RisesAndExpandsTargetAfterRangeUpgrade()
        {
            GameObject cameraObject = new GameObject("Test Framing Camera");
            GameObject emitterObject = new GameObject("Test Framing Emitter");
            createdObjects.Add(cameraObject);
            createdObjects.Add(emitterObject);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, -6.38f, -7f),
                Quaternion.Euler(-30f, 0f, 0f));
            LightEmitter2D emitter = emitterObject.AddComponent<LightEmitter2D>();
            emitter.BaseRadius = 4f;
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 10f;
            emitter.SectorAngle = 90f;
            emitter.Direction = Vector2.right;

            StageLightingCameraFramer framer =
                cameraObject.AddComponent<StageLightingCameraFramer>();
            framer.Initialize(camera, emitter, 0f);
            Vector3 initialTarget = framer.TargetPosition;

            emitter.BaseRadius = 7f;
            framer.ReframeImmediately();

            Assert.That(framer.TargetPosition.y, Is.LessThanOrEqualTo(initialTarget.y + 0.0001f));
            Assert.That(framer.TargetPosition.z, Is.LessThan(initialTarget.z));
        }

        [Test]
        public void StageLightingCameraFramer_UsesCurrentSectorDirection()
        {
            GameObject cameraObject = new GameObject("Test Direction Framing Camera");
            GameObject emitterObject = new GameObject("Test Direction Framing Emitter");
            createdObjects.Add(cameraObject);
            createdObjects.Add(emitterObject);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, -6.38f, -7f),
                Quaternion.Euler(-30f, 0f, 0f));
            LightEmitter2D emitter = emitterObject.AddComponent<LightEmitter2D>();
            emitter.BaseRadius = 4f;
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 60f;
            emitter.SectorAngle = 90f;
            emitter.Direction = Vector2.right;

            StageLightingCameraFramer framer =
                cameraObject.AddComponent<StageLightingCameraFramer>();
            framer.Initialize(camera, emitter, 0f);
            float rightTargetX = framer.TargetPosition.x;

            emitter.Direction = Vector2.left;
            framer.ReframeImmediately();

            Assert.That(framer.TargetPosition.x, Is.LessThan(rightTargetX));
        }

        [Test]
        public void StageLightingCameraFramer_ContainsSectorAndInnerCircleAfterManyDownwardUpgrades()
        {
            GameObject cameraObject = new GameObject("Test High Upgrade Framing Camera");
            GameObject emitterObject = new GameObject("Test High Upgrade Framing Emitter");
            createdObjects.Add(cameraObject);
            createdObjects.Add(emitterObject);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.aspect = 16f / 9f;
            camera.fieldOfView = 80f;
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, -6.38f, -7f),
                Quaternion.Euler(-30f, 0f, 0f));
            LightEmitter2D emitter = emitterObject.AddComponent<LightEmitter2D>();
            emitter.BaseRadius = 5f;
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 60f;
            emitter.SectorAngle = 60f;
            emitter.Direction = Vector2.down;
            InnerCircleLight2D innerCircle = emitterObject.AddComponent<InnerCircleLight2D>();
            innerCircle.RadiusMultiplier = 0.5f;
            innerCircle.SynchronizeNow();

            for (int i = 0; i < 6; i++)
            {
                emitter.BaseRadius += 1f;
            }

            StageLightingCameraFramer framer =
                cameraObject.AddComponent<StageLightingCameraFramer>();
            framer.Initialize(camera, emitter, 0f);
            camera.transform.position = framer.TargetPosition;
            camera.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);

            FieldInfo pointsField = typeof(StageLightingCameraFramer).GetField(
                "boundaryPoints",
                BindingFlags.Instance | BindingFlags.NonPublic);
            List<Vector3> points = pointsField.GetValue(framer) as List<Vector3>;
            Assert.That(points, Is.Not.Null);

            foreach (Vector3 point in points)
            {
                Vector3 viewportPoint = camera.WorldToViewportPoint(point);
                Assert.That(viewportPoint.z, Is.GreaterThan(0f));
                Assert.That(viewportPoint.x, Is.InRange(
                    framer.ScreenPadding - 0.001f,
                    1f - framer.ScreenPadding + 0.001f));
                Assert.That(viewportPoint.y, Is.InRange(
                    framer.ScreenPadding - 0.001f,
                    1f - framer.ScreenPadding + 0.001f));
            }
        }

        [Test]
        public void StageLightingCameraFramer_ContainsSectorAndInnerCircleInSafeViewport()
        {
            GameObject cameraObject = new GameObject("Test Combined Framing Camera");
            GameObject emitterObject = new GameObject("Test Combined Framing Emitter");
            createdObjects.Add(cameraObject);
            createdObjects.Add(emitterObject);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.aspect = 16f / 9f;
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, -6.38f, -7f),
                Quaternion.Euler(-30f, 0f, 0f));
            LightEmitter2D emitter = emitterObject.AddComponent<LightEmitter2D>();
            emitter.BaseRadius = 5f;
            emitter.Shape = LightShape2D.Sector;
            emitter.MinimumSectorAngle = 60f;
            emitter.SectorAngle = 60f;
            emitter.Direction = Vector2.right;
            InnerCircleLight2D innerCircle = emitterObject.AddComponent<InnerCircleLight2D>();
            innerCircle.RadiusMultiplier = 0.5f;
            innerCircle.SynchronizeNow();

            StageLightingCameraFramer framer =
                cameraObject.AddComponent<StageLightingCameraFramer>();
            framer.Initialize(camera, emitter, 0f);
            camera.transform.position = framer.TargetPosition;
            camera.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);

            FieldInfo pointsField = typeof(StageLightingCameraFramer).GetField(
                "boundaryPoints",
                BindingFlags.Instance | BindingFlags.NonPublic);
            List<Vector3> points = pointsField.GetValue(framer) as List<Vector3>;
            Assert.That(points, Is.Not.Null);
            Assert.That(points.Count, Is.GreaterThan(36));

            foreach (Vector3 point in points)
            {
                Vector3 viewportPoint = camera.WorldToViewportPoint(point);
                Assert.That(viewportPoint.z, Is.GreaterThan(0f));
                Assert.That(viewportPoint.x, Is.GreaterThanOrEqualTo(framer.ScreenPadding - 0.001f));
                Assert.That(viewportPoint.x, Is.LessThanOrEqualTo(1f - framer.ScreenPadding + 0.001f));
                Assert.That(viewportPoint.y, Is.GreaterThanOrEqualTo(framer.ScreenPadding - 0.001f));
                Assert.That(viewportPoint.y, Is.LessThanOrEqualTo(1f - framer.ScreenPadding + 0.001f));
            }
        }

        [Test]
        public void StageLightingCameraFramer_RepeatedTargetCalculationDoesNotDrift()
        {
            GameObject cameraObject = new GameObject("Test Stable Framing Camera");
            GameObject emitterObject = new GameObject("Test Stable Framing Emitter");
            createdObjects.Add(cameraObject);
            createdObjects.Add(emitterObject);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, -6.38f, -7f),
                Quaternion.Euler(-30f, 0f, 0f));
            LightEmitter2D emitter = emitterObject.AddComponent<LightEmitter2D>();
            emitter.Shape = LightShape2D.Sector;
            emitter.SectorAngle = 60f;
            emitter.MinimumSectorAngle = 60f;
            emitter.Direction = Vector2.up;

            StageLightingCameraFramer framer =
                cameraObject.AddComponent<StageLightingCameraFramer>();
            framer.Initialize(camera, emitter, 0f);
            Vector3 firstTarget = framer.CalculateTargetPosition();
            Vector3 secondTarget = framer.CalculateTargetPosition();
            Vector3 thirdTarget = framer.CalculateTargetPosition();

            Assert.That((secondTarget - firstTarget).sqrMagnitude, Is.LessThan(0.000001f));
            Assert.That((thirdTarget - firstTarget).sqrMagnitude, Is.LessThan(0.000001f));
        }

        [Test]
        public void StageLightingBootstrap_UsesSpriteVisualInsteadOfGeneratedQuads()
        {
            GameObject bootstrapObject = new GameObject("Test Stage Lighting Bootstrap");
            createdObjects.Add(bootstrapObject);
            StageLightingBootstrap bootstrap = bootstrapObject.AddComponent<StageLightingBootstrap>();
            GameObject candle = CreateAuthoredCandle(bootstrapObject.transform);
            SetPrivateField(bootstrap, "centralCandle", candle);
            Sprite candleSprite = Resources.Load<Sprite>("PowerTexture/09416f3344d521839bd708038ebc7229");

            Assert.That(candleSprite, Is.Not.Null);
            SpriteRenderer authoredRenderer = candle.transform.Find("Visual")
                .GetComponent<SpriteRenderer>();
            authoredRenderer.sprite = candleSprite;
            authoredRenderer.sortingOrder = 10;

            SetPrivateField(bootstrap, "candleSprite", candleSprite);
            SetPrivateField(bootstrap, "flameSprite", null);
            InvokePrivateMethod(bootstrap, "EnsureCandle");

            Transform visualRoot = bootstrap.CandleEmitter.transform.Find("Visual");
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
            GameObject candle = CreateAuthoredCandle(bootstrapObject.transform);
            SetPrivateField(bootstrap, "centralCandle", candle);
            Sprite candleSprite = Resources.Load<Sprite>("PowerTexture/09416f3344d521839bd708038ebc7229");

            Assert.That(candleSprite, Is.Not.Null);
            SpriteRenderer authoredRenderer = candle.transform.Find("Visual")
                .GetComponent<SpriteRenderer>();
            authoredRenderer.sprite = candleSprite;
            authoredRenderer.sortingOrder = 10;

            SetPrivateField(bootstrap, "candleSprite", candleSprite);
            SetPrivateField(bootstrap, "flameSprite", null);
            InvokePrivateMethod(bootstrap, "EnsureCandle");

            bootstrap.RefreshCandleVisual();

            Transform secondVisualRoot = bootstrap.CandleEmitter.transform.Find(
                "Visual");
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

        private GameObject CreateAuthoredCandle(Transform parent)
        {
            GameObject candle = new GameObject("Stage Central Candle");
            candle.transform.SetParent(parent, false);
            candle.AddComponent<LightEmitter2D>();
            candle.AddComponent<InnerCircleLight2D>();
            candle.AddComponent<CandleFocusController>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(candle.transform, false);
            visual.AddComponent<SpriteRenderer>();
            return candle;
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
