using System.Collections.Generic;
using System.Reflection;
using Game.DayNight;
using Game.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Game.Building.Tests
{
    public sealed class BuildSystemTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly List<BuildPlaced> placedEvents = new List<BuildPlaced>();
        private readonly List<BuildPlacementFailed> failedEvents =
            new List<BuildPlacementFailed>();

        [SetUp]
        public void SetUp()
        {
            EventBus.Instance.Subscribe<BuildPlaced>(OnBuildPlaced);
            EventBus.Instance.Subscribe<BuildPlacementFailed>(OnPlacementFailed);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Instance.UnSubscribe<BuildPlaced>(OnBuildPlaced);
            EventBus.Instance.UnSubscribe<BuildPlacementFailed>(OnPlacementFailed);

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            placedEvents.Clear();
            failedEvents.Clear();
        }

        [Test]
        public void TryPlace_RegistersAllFourCellsAndSpendsCoins()
        {
            BuildFixture fixture = CreateFixture(20);

            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)), Is.True);
            Assert.That(fixture.inventory.Coins, Is.EqualTo(10));
            Assert.That(fixture.grid.OccupiedCells.Count, Is.EqualTo(4));
            Assert.That(fixture.system.Builds.Count, Is.EqualTo(1));
            Assert.That(placedEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryPlace_RejectsAnyOverlapWithoutSpendingAgain()
        {
            BuildFixture fixture = CreateFixture(30);
            Vector3Int position = new Vector3Int(2, 2, 0);

            Assert.That(fixture.system.TryPlace(fixture.definition, position), Is.True);
            Assert.That(fixture.system.TryPlace(fixture.definition, position), Is.False);

            Assert.That(fixture.inventory.Coins, Is.EqualTo(20));
            Assert.That(fixture.system.Builds.Count, Is.EqualTo(1));
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.Occupied));
        }

        [Test]
        public void TryPlace_RejectsInsufficientCoinsWithoutCreatingObject()
        {
            BuildFixture fixture = CreateFixture(9);

            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)), Is.False);

            Assert.That(fixture.inventory.Coins, Is.EqualTo(9));
            Assert.That(fixture.system.Builds.Count, Is.Zero);
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.InsufficientCoins));
        }

        [Test]
        public void TryPlace_RejectsNightPhase()
        {
            BuildFixture fixture = CreateFixture(20);
            fixture.dayNightSystem.EndDay();

            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)), Is.False);
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.WrongPhase));
        }

        [Test]
        public void TryPlace_AllowsDaytimeFootprintInsideMaximumLightRange()
        {
            BuildFixture fixture = CreateFixture(20);
            ConfigureBuildLight(fixture, 10f);

            Assert.That(
                fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)),
                Is.True);
            Assert.That(fixture.inventory.Coins, Is.EqualTo(10));
        }

        [Test]
        public void TryPlace_RejectsFootprintOutsideMaximumLightRangeWithoutSpending()
        {
            BuildFixture fixture = CreateFixture(20);
            ConfigureBuildLight(fixture, 1f);

            Assert.That(
                fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)),
                Is.False);
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.OutsideLightRange));
            Assert.That(fixture.inventory.Coins, Is.EqualTo(20));
            Assert.That(fixture.system.Builds, Is.Empty);
            Assert.That(failedEvents, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryPlace_RejectsFootprintThatStraddlesMaximumLightRange()
        {
            BuildFixture fixture = CreateFixture(20);
            ConfigureBuildLight(fixture, 2.5f);

            Assert.That(
                fixture.system.TryPlace(fixture.definition, new Vector3Int(1, 0, 0)),
                Is.False);
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.OutsideLightRange));
        }

        [Test]
        public void TryPlace_RejectsMissingBuildLight()
        {
            BuildFixture fixture = CreateFixture(20);
            fixture.system.ConfigureLightingForTests(null);

            Assert.That(
                fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)),
                Is.False);
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.MissingBuildLight));
            Assert.That(fixture.inventory.Coins, Is.EqualTo(20));
        }

        [Test]
        public void LookoutDefinition_ScalesCostAndLightWithUpgradeLevels()
        {
            BuildDefinition definition = ScriptableObject.CreateInstance<BuildDefinition>();
            createdObjects.Add(definition);

            Assert.That(definition.GetCoinCost(0, 0), Is.EqualTo(10));
            Assert.That(definition.GetCoinCost(1, 0), Is.EqualTo(10));
            Assert.That(definition.GetCoinCost(0, 1), Is.EqualTo(10));
            Assert.That(definition.GetCoinCost(2, 0), Is.EqualTo(11));
            Assert.That(definition.GetCoinCost(5, 5), Is.EqualTo(19));
            Assert.That(definition.GetCoinCost(10, 10), Is.EqualTo(29));

            Assert.That(definition.GetLightRadius(0), Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(definition.GetLightRadius(1), Is.EqualTo(2.75f).Within(0.0001f));
            Assert.That(definition.GetLightRadius(5), Is.EqualTo(3.75f).Within(0.0001f));
            Assert.That(definition.GetLightRadius(10), Is.EqualTo(5f).Within(0.0001f));

            Assert.That(definition.GetLightIntensity(0), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(definition.GetLightIntensity(1), Is.EqualTo(0.875f).Within(0.0001f));
            Assert.That(definition.GetLightIntensity(5), Is.EqualTo(1.175f).Within(0.0001f));
            Assert.That(definition.GetLightIntensity(10), Is.EqualTo(1.55f).Within(0.0001f));

            Assert.That(definition.GetLightDamagePerSecond(0), Is.EqualTo(3f).Within(0.0001f));
            Assert.That(definition.GetLightDamagePerSecond(1), Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(definition.GetLightDamagePerSecond(5), Is.EqualTo(5.5f).Within(0.0001f));
            Assert.That(definition.GetLightDamagePerSecond(10), Is.EqualTo(8f).Within(0.0001f));
            Assert.That(definition.GetLightDamagePerSecond(10), Is.LessThanOrEqualTo(definition.LightDamageCap));
        }

        [Test]
        public void FactoryDefinition_IgnoresUpgradeLevels()
        {
            BuildDefinition definition = ScriptableObject.CreateInstance<BuildDefinition>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "emitsNightLight", false);
            SetPrivateField(definition, "coinCost", 20);
            SetPrivateField(definition, "lightRadius", 0.01f);
            SetPrivateField(definition, "lightIntensity", 0f);
            SetPrivateField(definition, "lightDamagePerSecond", 0f);
            SetPrivateField(definition, "lightDamageCap", 0f);

            Assert.That(definition.GetCoinCost(10, 10), Is.EqualTo(20));
            Assert.That(definition.GetLightRadius(10), Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(definition.GetLightIntensity(10), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(definition.GetLightDamagePerSecond(10), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TryPlace_KeepsBaseLookoutCostAfterFirstUpgrade()
        {
            BuildFixture fixture = CreateFixture(20);
            StageLightingBootstrap bootstrap = CreateLightingBootstrap();
            Assert.That(bootstrap.UpgradeIntensity(), Is.True);

            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)), Is.True);
            Assert.That(fixture.inventory.Coins, Is.EqualTo(10));
        }

        [Test]
        public void TryPlace_SpendsScaledLookoutCostAfterSecondUpgrade()
        {
            BuildFixture fixture = CreateFixture(20);
            StageLightingBootstrap bootstrap = CreateLightingBootstrap();
            Assert.That(bootstrap.UpgradeIntensity(), Is.True);
            Assert.That(bootstrap.UpgradeIntensity(), Is.True);

            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)), Is.True);
            Assert.That(fixture.inventory.Coins, Is.EqualTo(9));
        }

        [Test]
        public void TryPlace_RejectsInsufficientScaledLookoutCostWithoutCreatingObject()
        {
            BuildFixture fixture = CreateFixture(10);
            StageLightingBootstrap bootstrap = CreateLightingBootstrap();
            Assert.That(bootstrap.UpgradeIntensity(), Is.True);
            Assert.That(bootstrap.UpgradeIntensity(), Is.True);

            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)), Is.False);
            Assert.That(fixture.inventory.Coins, Is.EqualTo(10));
            Assert.That(fixture.system.Builds, Is.Empty);
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.InsufficientCoins));
        }

        [Test]
        public void PlacedLookout_RefreshesLightStatsWhenLightingUpgrades()
        {
            BuildFixture fixture = CreateFixture(20);
            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(2, 2, 0)), Is.True);

            LightEmitter2D emitter = fixture.system.Builds[0].LightEmitter;
            Assert.That(emitter.BaseRadius, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(emitter.BaseIntensity, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(emitter.BaseDamagePerSecond, Is.EqualTo(3f).Within(0.0001f));

            StageLightingBootstrap bootstrap = CreateLightingBootstrap();
            Assert.That(bootstrap.UpgradeIntensity(), Is.True);
            Assert.That(emitter.BaseRadius, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(emitter.BaseIntensity, Is.EqualTo(0.875f).Within(0.0001f));
            Assert.That(emitter.BaseDamagePerSecond, Is.EqualTo(3.5f).Within(0.0001f));

            Assert.That(bootstrap.UpgradeRange(), Is.True);
            Assert.That(emitter.BaseRadius, Is.EqualTo(2.75f).Within(0.0001f));
            Assert.That(emitter.BaseIntensity, Is.EqualTo(0.875f).Within(0.0001f));
            Assert.That(emitter.BaseDamagePerSecond, Is.EqualTo(3.5f).Within(0.0001f));
        }

        [Test]
        public void TryPlace_RejectsPositionOutsideBuildBounds()
        {
            BuildFixture fixture = CreateFixture(20);

            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(3, 3, 0)), Is.False);
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.OutsideBuildBounds));
            Assert.That(fixture.inventory.Coins, Is.EqualTo(20));
        }

        [Test]
        public void PlacementCamera_EndRestoresPoseAndUnlocksAim()
        {
            GameObject cameraObject = CreateObject("Placement Camera");
            GameObject lightObject = CreateObject("Placement Light");
            GameObject controllerObject = CreateObject("Placement Camera Controller");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(1f, -6.38f, -7f),
                Quaternion.Euler(-30f, 0f, 0f));
            camera.fieldOfView = 80f;
            LightEmitter2D emitter = lightObject.AddComponent<LightEmitter2D>();
            CandleFocusController focus = lightObject.AddComponent<CandleFocusController>();
            focus.Initialize(camera, emitter);
            BuildPlacementCameraController controller =
                controllerObject.AddComponent<BuildPlacementCameraController>();
            controller.SetReferences(camera, emitter, focus, null, 0f);
            Vector3 originalPosition = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;

            Assert.That(controller.BeginPlacement(), Is.True);
            Assert.That(focus.IsAimLocked, Is.True);
            camera.transform.SetPositionAndRotation(Vector3.one, Quaternion.identity);
            camera.fieldOfView = 30f;
            controller.EndPlacement();

            Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(originalRotation));
            Assert.That(camera.fieldOfView, Is.EqualTo(80f));
            Assert.That(focus.IsAimLocked, Is.False);
        }

        [Test]
        public void PlacementCamera_ScrollRoundTripReturnsToInitialFieldOfView()
        {
            GameObject cameraObject = CreateObject("Round Trip Camera");
            GameObject lightObject = CreateObject("Round Trip Light");
            GameObject controllerObject = CreateObject("Round Trip Controller");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.aspect = 16f / 9f;
            camera.fieldOfView = 80f;
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, -6.38f, -7f),
                Quaternion.Euler(-30f, 0f, 0f));
            LightEmitter2D emitter = lightObject.AddComponent<LightEmitter2D>();
            emitter.BaseRadius = 100f;
            CandleFocusController focus = lightObject.AddComponent<CandleFocusController>();
            focus.Initialize(camera, emitter);
            BuildPlacementCameraController controller =
                controllerObject.AddComponent<BuildPlacementCameraController>();
            controller.SetReferences(camera, emitter, focus, null, 0f);
            controller.SetZoomConfigurationForTests(25f, 60f, 5f, 8f, 55f);
            Vector3 initialPosition = camera.transform.position;
            Quaternion initialRotation = camera.transform.rotation;

            Assert.That(controller.BeginPlacement(), Is.True);
            for (int i = 0; i < 5; i++)
            {
                controller.ApplyZoomDeltaForTests(1f);
                controller.ApplyZoomDeltaForTests(-1f);
            }

            Assert.That(camera.fieldOfView, Is.EqualTo(80f).Within(0.0001f));
            Assert.That(camera.transform.position, Is.EqualTo(initialPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(initialRotation));

            controller.EndPlacement();

            Assert.That(camera.fieldOfView, Is.EqualTo(80f).Within(0.0001f));
            Assert.That(camera.transform.position, Is.EqualTo(initialPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(initialRotation));
            Assert.That(focus.IsAimLocked, Is.False);
        }

        [Test]
        public void PlacementCamera_EndWhileIdlePreservesManualAimLock()
        {
            GameObject cameraObject = CreateObject("Idle Placement Camera");
            GameObject lightObject = CreateObject("Idle Placement Light");
            GameObject controllerObject = CreateObject("Idle Placement Controller");
            Camera camera = cameraObject.AddComponent<Camera>();
            LightEmitter2D emitter = lightObject.AddComponent<LightEmitter2D>();
            CandleFocusController focus = lightObject.AddComponent<CandleFocusController>();
            focus.Initialize(camera, emitter);
            focus.SetAimLocked(true);
            BuildPlacementCameraController controller =
                controllerObject.AddComponent<BuildPlacementCameraController>();
            controller.SetReferences(camera, emitter, focus, null, 0f);

            controller.EndPlacement();

            Assert.That(focus.IsAimLocked, Is.True);
        }

        [Test]
        public void PlacementCamera_EndRestoresFramerManualMode()
        {
            GameObject cameraObject = CreateObject("Manual Framing Camera");
            GameObject lightObject = CreateObject("Manual Framing Light");
            GameObject controllerObject = CreateObject("Manual Framing Controller");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, -6.38f, -7f),
                Quaternion.Euler(-30f, 0f, 0f));
            LightEmitter2D emitter = lightObject.AddComponent<LightEmitter2D>();
            StageLightingCameraFramer framer =
                cameraObject.AddComponent<StageLightingCameraFramer>();
            framer.Initialize(camera, emitter, 0f);
            framer.SetManualMode(true);
            BuildPlacementCameraController controller =
                controllerObject.AddComponent<BuildPlacementCameraController>();
            controller.SetReferences(camera, emitter, null, framer, 0f);

            Assert.That(controller.BeginPlacement(), Is.True);
            controller.EndPlacement();

            Assert.That(framer.IsManualMode, Is.True);
        }

        [Test]
        public void PlacementCamera_BoundaryIncludesEntireViewport()
        {
            GameObject cameraObject = CreateObject("Boundary Camera");
            GameObject lightObject = CreateObject("Boundary Light");
            GameObject controllerObject = CreateObject("Boundary Camera Controller");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.aspect = 1f;
            camera.fieldOfView = 25f;
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -5f),
                Quaternion.identity);
            LightEmitter2D emitter = lightObject.AddComponent<LightEmitter2D>();
            emitter.BaseRadius = 10f;
            emitter.MinimumSectorAngle = 360f;
            emitter.SectorAngle = 360f;
            BuildPlacementCameraController controller =
                controllerObject.AddComponent<BuildPlacementCameraController>();
            controller.ConfigureForTests(camera, emitter, 0f);

            Assert.That(
                controller.IsCameraViewInsideBoundary(camera.transform.position),
                Is.True);
            Assert.That(
                controller.IsCameraViewInsideBoundary(new Vector3(9.5f, 0f, -5f)),
                Is.False);
        }

        private BuildFixture CreateFixture(int coins)
        {
            GameObject root = CreateObject("Build Test Root");
            Grid gridComponent = root.AddComponent<Grid>();
            BuildGrid buildGrid = root.AddComponent<BuildGrid>();
            buildGrid.ConfigureForTests(
                gridComponent,
                new BoundsInt(0, 0, 0, 4, 4, 1));

            CoinInventory inventory = root.AddComponent<CoinInventory>();
            inventory.InitializeForTests(coins);

            DayNightSystem dayNightSystem = root.AddComponent<DayNightSystem>();

            GameObject prefab = CreateObject("Lookout Tower Test Prefab");
            prefab.SetActive(false);
            prefab.AddComponent<BuildInstance>();

            BuildDefinition definition =
                ScriptableObject.CreateInstance<BuildDefinition>();
            createdObjects.Add(definition);
            SetPrivatePrefab(definition, prefab);

            BuildSystem buildSystem = root.AddComponent<BuildSystem>();
            buildSystem.ConfigureForTests(buildGrid, dayNightSystem, inventory);
            GameObject lightObject = CreateObject("Default Build Range Light");
            LightEmitter2D buildLight = lightObject.AddComponent<LightEmitter2D>();
            buildLight.BaseRadius = 100f;
            buildLight.MinimumSectorAngle = 360f;
            buildLight.SectorAngle = 360f;
            buildSystem.ConfigureLightingForTests(buildLight);

            return new BuildFixture(
                buildSystem,
                buildGrid,
                dayNightSystem,
                inventory,
                definition);
        }

        private void ConfigureBuildLight(BuildFixture fixture, float radius)
        {
            GameObject lightObject = CreateObject("Build Range Light");
            LightEmitter2D emitter = lightObject.AddComponent<LightEmitter2D>();
            emitter.Shape = LightShape2D.Sector;
            emitter.BaseRadius = radius;
            emitter.MinimumSectorAngle = 360f;
            emitter.SectorAngle = 360f;
            fixture.system.ConfigureLightingForTests(emitter);
        }

        private GameObject CreateObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetPrivatePrefab(
            BuildDefinition definition,
            GameObject prefab)
        {
            SetPrivateField(definition, "prefab", prefab);
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

        private StageLightingBootstrap CreateLightingBootstrap()
        {
            GameObject bootstrapObject = CreateObject("Lookout Lighting Bootstrap");
            StageLightingBootstrap bootstrap = bootstrapObject.AddComponent<StageLightingBootstrap>();
            GameObject candle = CreateObject("Lookout Central Candle");
            candle.transform.SetParent(bootstrapObject.transform, false);
            candle.AddComponent<LightEmitter2D>();
            candle.AddComponent<InnerCircleLight2D>();
            candle.AddComponent<CandleFocusController>();
            SetPrivateField(bootstrap, "centralCandle", candle);
            SetPrivateField(bootstrap, "initialShape", LightShape2D.Circle);
            SetPrivateField(bootstrap, "baseRadius", 100f);
            InvokePrivateMethod(bootstrap, "EnsureCandle");
            return bootstrap;
        }

        private void OnBuildPlaced(BuildPlaced placed)
        {
            placedEvents.Add(placed);
        }

        private void OnPlacementFailed(BuildPlacementFailed failed)
        {
            failedEvents.Add(failed);
        }

        private readonly struct BuildFixture
        {
            public readonly BuildSystem system;
            public readonly BuildGrid grid;
            public readonly DayNightSystem dayNightSystem;
            public readonly CoinInventory inventory;
            public readonly BuildDefinition definition;

            public BuildFixture(
                BuildSystem system,
                BuildGrid grid,
                DayNightSystem dayNightSystem,
                CoinInventory inventory,
                BuildDefinition definition)
            {
                this.system = system;
                this.grid = grid;
                this.dayNightSystem = dayNightSystem;
                this.inventory = inventory;
                this.definition = definition;
            }
        }
    }
}
