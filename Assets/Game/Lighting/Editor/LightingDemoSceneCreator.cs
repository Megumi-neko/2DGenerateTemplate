using Game.Lighting.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Lighting.Editor
{
    public static class LightingDemoSceneCreator
    {
        public const string ScenePath = "Assets/Scenes/LightingDemo.unity";

        [MenuItem("Tools/Game Lighting/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            CreateAndSaveScene();
        }

        public static void CreateDemoSceneBatch()
        {
            CreateAndSaveScene();
        }

        private static void CreateAndSaveScene()
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene demoScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(demoScene);

            try
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.transform.SetPositionAndRotation(
                    new Vector3(0f, -6f, -14f),
                    Quaternion.Euler(-20f, 0f, 0f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
                camera.fieldOfView = 55f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.allowHDR = true;

                DarknessOverlayEffect overlay = cameraObject.AddComponent<DarknessOverlayEffect>();
                overlay.GameplayPlaneZ = 0f;
                overlay.DarknessOpacity = 0.96f;

                GameObject demoObject = new GameObject("Lighting Demo");
                demoObject.AddComponent<LightingDemoController>();
                demoObject.AddComponent<LightingDemoBootstrap>();

                EditorSceneManager.MarkSceneDirty(demoScene);
                if (!EditorSceneManager.SaveScene(demoScene, ScenePath))
                {
                    throw new System.InvalidOperationException(
                        $"Failed to save lighting demo scene at {ScenePath}.");
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"Created lighting demo scene without modifying the active scene: {ScenePath}");
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                if (demoScene.IsValid() && demoScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(demoScene, true);
                }
            }
        }
    }
}
