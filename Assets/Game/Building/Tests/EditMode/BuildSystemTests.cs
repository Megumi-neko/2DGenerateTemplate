using System.Collections.Generic;
using Game.DayNight;
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
        public void TryPlace_RejectsPositionOutsideBuildBounds()
        {
            BuildFixture fixture = CreateFixture(20);

            Assert.That(fixture.system.TryPlace(fixture.definition, new Vector3Int(3, 3, 0)), Is.False);
            Assert.That(fixture.system.LastFailureReason,
                Is.EqualTo(BuildPlacementFailureReason.OutsideBuildBounds));
            Assert.That(fixture.inventory.Coins, Is.EqualTo(20));
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

            return new BuildFixture(
                buildSystem,
                buildGrid,
                dayNightSystem,
                inventory,
                definition);
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
