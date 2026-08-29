using System.Collections.Generic;
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
            typeof(BuildDefinition)
                .GetField("prefab", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(definition, prefab);
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
