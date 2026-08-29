using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.BaseSystem.Tests
{
    public sealed class SceneManagerSystemTests
    {
        private GameObject managerObject;

        [TearDown]
        public void TearDown()
        {
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void SceneLoadTarget_NameAndIndexTargetsAreDistinct()
        {
            SceneLoadTarget nameTarget = new SceneLoadTarget("MainMenu", -1, LoadSceneMode.Single);
            SceneLoadTarget indexTarget = new SceneLoadTarget(string.Empty, 0, LoadSceneMode.Single);

            Assert.That(nameTarget.HasName, Is.True);
            Assert.That(indexTarget.HasName, Is.False);
            Assert.That(nameTarget, Is.Not.EqualTo(indexTarget));
        }

        [Test]
        public void SceneLoadTarget_EqualityIncludesMode()
        {
            SceneLoadTarget singleTarget = new SceneLoadTarget("Stage", -1, LoadSceneMode.Single);
            SceneLoadTarget additiveTarget = new SceneLoadTarget("Stage", -1, LoadSceneMode.Additive);

            Assert.That(singleTarget, Is.Not.EqualTo(additiveTarget));
            Assert.That(singleTarget.GetHashCode(), Is.Not.EqualTo(additiveTarget.GetHashCode()));
        }

        [Test]
        public void LoadScene_RejectsEmptyNameWithoutStartingLoad()
        {
            managerObject = new GameObject("Scene Manager Test");
            SceneManagerSystem system = managerObject.AddComponent<SceneManagerSystem>();

            SceneLoadRequestStatus result = system.LoadScene(" ");

            Assert.That(result, Is.EqualTo(SceneLoadRequestStatus.Rejected));
            Assert.That(system.IsLoading, Is.False);
            Assert.That(system.LoadingProgress, Is.EqualTo(0f));
        }

        [Test]
        public void LoadScene_RejectsInvalidBuildIndexWithoutStartingLoad()
        {
            managerObject = new GameObject("Scene Manager Test");
            SceneManagerSystem system = managerObject.AddComponent<SceneManagerSystem>();

            SceneLoadRequestStatus result = system.LoadScene(-1);

            Assert.That(result, Is.EqualTo(SceneLoadRequestStatus.Rejected));
            Assert.That(system.IsLoading, Is.False);
        }
    }
}
