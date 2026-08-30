using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.BaseSystem.Tests
{
    public sealed class CameraShakeControllerTests
    {
        private static readonly MethodInfo UpdateShakeMethod =
            typeof(CameraShakeController).GetMethod(
                "UpdateShake",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject cameraObject;
        private CameraShakeController controller;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Camera Shake Test");
            controller = cameraObject.AddComponent<CameraShakeController>();
            Assert.That(UpdateShakeMethod, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void IdleUpdate_DoesNotOverwriteExternalCameraPosition()
        {
            Vector3 externalPosition = new Vector3(3f, -4f, -12f);
            cameraObject.transform.localPosition = externalPosition;

            UpdateShake(0.1f);

            Assert.That(cameraObject.transform.localPosition, Is.EqualTo(externalPosition));
        }

        [Test]
        public void ActiveShake_DoesNotAccumulateItsPreviousOffset()
        {
            Vector3 basePosition = new Vector3(1f, 2f, -7f);
            cameraObject.transform.localPosition = basePosition;
            controller.Shake(0.2f, 10f);
            UpdateShake(0.1f);
            Vector3 firstOutput = cameraObject.transform.localPosition;

            for (int i = 0; i < 20; i++)
            {
                UpdateShake(0f);
            }

            AssertVectorApproximately(cameraObject.transform.localPosition, firstOutput);
            Assert.That(
                Vector3.Distance(cameraObject.transform.localPosition, basePosition),
                Is.LessThanOrEqualTo(0.2f * Mathf.Sqrt(2f) + 0.0001f));
        }

        [Test]
        public void ActiveShake_UsesPositionWrittenByExternalControllerAsNewBase()
        {
            Vector3 initialBase = new Vector3(0f, -3f, -7f);
            cameraObject.transform.localPosition = initialBase;
            controller.Shake(0.2f, 10f);
            UpdateShake(0.1f);
            Vector3 firstOffset = cameraObject.transform.localPosition - initialBase;
            Vector3 externalBase = new Vector3(4f, -8f, -20f);
            cameraObject.transform.localPosition = externalBase;

            UpdateShake(0f);

            AssertVectorApproximately(
                cameraObject.transform.localPosition,
                externalBase + firstOffset);
        }

        [Test]
        public void Disable_PreservesPositionWrittenByExternalController()
        {
            cameraObject.transform.localPosition = new Vector3(0f, -3f, -7f);
            controller.Shake(0.2f, 10f);
            UpdateShake(0.1f);
            Vector3 externalPosition = new Vector3(-5f, 6f, -18f);
            cameraObject.transform.localPosition = externalPosition;

            controller.enabled = false;

            Assert.That(cameraObject.transform.localPosition, Is.EqualTo(externalPosition));
        }

        private void UpdateShake(float deltaTime)
        {
            UpdateShakeMethod.Invoke(controller, new object[] { deltaTime });
        }

        private static void AssertVectorApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.000001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.000001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.000001f));
        }
    }
}
