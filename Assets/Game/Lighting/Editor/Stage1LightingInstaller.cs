using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Lighting.Editor
{
    public static class Stage1LightingInstaller
    {
        public const string Stage1ScenePath = "Assets/Scenes/Stage 1.unity";

        [MenuItem("Tools/Game Lighting/Install Stage 1 Lighting")]
        public static void InstallStage1Lighting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Stop Play Mode before installing Stage 1 lighting.");
                return;
            }

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene stage1Scene = SceneManager.GetSceneByPath(Stage1ScenePath);
            bool openedByInstaller = false;

            try
            {
                if (!stage1Scene.IsValid() || !stage1Scene.isLoaded)
                {
                    stage1Scene = EditorSceneManager.OpenScene(Stage1ScenePath, OpenSceneMode.Additive);
                    openedByInstaller = true;
                }

                if (stage1Scene.isDirty)
                {
                    Debug.LogError(
                        $"Cannot install lighting because {Stage1ScenePath} has unsaved changes. " +
                        "Save or revert that scene first.");
                    return;
                }

                SceneManager.SetActiveScene(stage1Scene);
                GameObject cameraObject = FindRootObject(stage1Scene, "Main Camera");
                GameObject gridObject = FindRootObject(stage1Scene, "Grid");
                if (cameraObject == null || gridObject == null)
                {
                    throw new System.InvalidOperationException(
                        "Stage 1 must contain root objects named 'Main Camera' and 'Grid'.");
                }

                Camera camera = cameraObject.GetComponent<Camera>();
                if (camera == null)
                {
                    throw new System.InvalidOperationException("Stage 1 Main Camera has no Camera component.");
                }

                DarknessOverlayEffect overlay = camera.GetComponent<DarknessOverlayEffect>();
                if (overlay == null)
                {
                    overlay = camera.gameObject.AddComponent<DarknessOverlayEffect>();
                }

                overlay.GameplayPlaneZ = 0f;
                overlay.DarknessOpacity = 0.96f;

                StageLightingBootstrap bootstrap = gridObject.GetComponent<StageLightingBootstrap>();
                if (bootstrap == null)
                {
                    bootstrap = gridObject.AddComponent<StageLightingBootstrap>();
                }

                EditorSceneManager.MarkSceneDirty(stage1Scene);
                if (!EditorSceneManager.SaveScene(stage1Scene))
                {
                    throw new System.InvalidOperationException(
                        $"Failed to save installed scene {Stage1ScenePath}.");
                }

                Debug.Log(
                    $"Installed candle lighting on {Stage1ScenePath}. " +
                    "The original Stage.unity scene was not changed.");
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                if (openedByInstaller && stage1Scene.IsValid() && stage1Scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(stage1Scene, true);
                }
            }
        }

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root.name == objectName)
                {
                    return root;
                }
            }

            return null;
        }
    }
}
