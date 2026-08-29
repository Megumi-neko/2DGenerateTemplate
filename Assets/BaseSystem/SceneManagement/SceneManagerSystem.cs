using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.BaseSystem
{
    /// <summary>
    /// Provides a single, persistent entry point for runtime scene transitions.
    /// Scenes must be included in Build Settings before they can be loaded in a player.
    /// </summary>
    [AddComponentMenu("Game/Base System/Scene Manager System")]
    [DisallowMultipleComponent]
    public sealed class SceneManagerSystem : MonoBehaviour
    {
        private static SceneManagerSystem instance;

        private AsyncOperation loadingOperation;
        private SceneLoadTarget loadingTarget;
        private bool isLoading;
        private string currentSceneName;
        private string currentScenePath;

        public static SceneManagerSystem Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    GameObject managerObject = new GameObject(nameof(SceneManagerSystem));
                    instance = managerObject.AddComponent<SceneManagerSystem>();
                }

                return instance;
            }
        }

        public static bool HasInstance => instance != null;
        public bool IsLoading => isLoading;
        public float LoadingProgress { get; private set; }
        public string CurrentSceneName => currentSceneName;
        public string CurrentScenePath => currentScenePath;
        public string LoadingSceneName => isLoading ? loadingTarget.Name : string.Empty;
        public LoadSceneMode LoadingMode => loadingTarget.Mode;

        public event Action<SceneLoadStarted> LoadStarted;
        public event Action<SceneLoadProgressChanged> LoadProgressChanged;
        public event Action<SceneLoadCompleted> LoadCompleted;
        public event Action<SceneLoadFailed> LoadFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            _ = Instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            UpdateCurrentScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }

        public SceneLoadRequestStatus LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return Reject(new SceneLoadTarget(sceneName, -1, mode),
                    SceneLoadFailureReason.EmptySceneName);
            }

            SceneLoadTarget target = new SceneLoadTarget(sceneName.Trim(), -1, mode);
            if (!Application.CanStreamedLevelBeLoaded(target.Name))
            {
                return Reject(target, SceneLoadFailureReason.SceneNotInBuildSettings);
            }

            return BeginLoad(target);
        }

        public SceneLoadRequestStatus LoadScene(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
        {
            SceneLoadTarget target = new SceneLoadTarget(string.Empty, buildIndex, mode);
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                return Reject(target, SceneLoadFailureReason.InvalidBuildIndex);
            }

            return BeginLoad(target);
        }

        public SceneLoadRequestStatus LoadScene(SceneLoadTarget target)
        {
            if (target.HasName)
            {
                return LoadScene(target.Name, target.Mode);
            }

            return LoadScene(target.BuildIndex, target.Mode);
        }



        private SceneLoadRequestStatus BeginLoad(SceneLoadTarget target)
        {
            if (isLoading)
            {
                if (loadingTarget.Equals(target))
                {
                    PublishFailed(target, SceneLoadFailureReason.AlreadyLoadingSameScene);
                    return SceneLoadRequestStatus.AlreadyLoadingSameScene;
                }

                PublishFailed(target, SceneLoadFailureReason.AnotherSceneIsLoading);
                return SceneLoadRequestStatus.AnotherSceneIsLoading;
            }

            try
            {
                loadingOperation = target.HasName
                    ? SceneManager.LoadSceneAsync(target.Name, target.Mode)
                    : SceneManager.LoadSceneAsync(target.BuildIndex, target.Mode);
            }
            catch (Exception exception)
            {
                loadingOperation = null;
                PublishFailed(target, SceneLoadFailureReason.LoadException, exception.Message);
                return SceneLoadRequestStatus.Failed;
            }

            if (loadingOperation == null)
            {
                PublishFailed(target, SceneLoadFailureReason.LoadOperationUnavailable);
                return SceneLoadRequestStatus.Failed;
            }

            isLoading = true;
            loadingTarget = target;
            LoadingProgress = 0f;
            PublishStarted(target);
            StartCoroutine(TrackLoad(target, loadingOperation));
            return SceneLoadRequestStatus.Accepted;
        }

private System.Collections.IEnumerator TrackLoad(SceneLoadTarget target, AsyncOperation operation)
        {
            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                if (!Mathf.Approximately(progress, LoadingProgress))
                {
                    LoadingProgress = progress;
                    PublishProgress(target, progress);
                }

                yield return null;
            }

            if (!isLoading || loadingOperation != operation)
            {
                yield break;
            }

            isLoading = false;
            loadingOperation = null;
            LoadingProgress = 1f;
            PublishProgress(target, 1f);
            Scene loadedScene = target.HasName
                ? SceneManager.GetSceneByName(target.Name)
                : SceneManager.GetSceneByBuildIndex(target.BuildIndex);
            UpdateCurrentScene(loadedScene.IsValid() ? loadedScene : SceneManager.GetActiveScene());
            SceneLoadCompleted evt = new SceneLoadCompleted(target, currentSceneName, currentScenePath);
            EventBus.Instance.Publish(evt);
            LoadCompleted?.Invoke(evt);
        }

        private SceneLoadRequestStatus Reject(SceneLoadTarget target, SceneLoadFailureReason reason, string details = "")
        {
            PublishFailed(target, reason, details);
            return reason == SceneLoadFailureReason.AlreadyLoadingSameScene
                ? SceneLoadRequestStatus.AlreadyLoadingSameScene
                : reason == SceneLoadFailureReason.AnotherSceneIsLoading
                    ? SceneLoadRequestStatus.AnotherSceneIsLoading
                    : SceneLoadRequestStatus.Rejected;
        }

        private void PublishStarted(SceneLoadTarget target)
        {
            SceneLoadStarted evt = new SceneLoadStarted(target, currentSceneName, currentScenePath);
            EventBus.Instance.Publish(evt);
            LoadStarted?.Invoke(evt);
        }

        private void PublishProgress(SceneLoadTarget target, float progress)
        {
            SceneLoadProgressChanged evt = new SceneLoadProgressChanged(target, progress);
            EventBus.Instance.Publish(evt);
            LoadProgressChanged?.Invoke(evt);
        }

        private void PublishFailed(SceneLoadTarget target, SceneLoadFailureReason reason, string details = "")
        {
            SceneLoadFailed evt = new SceneLoadFailed(target, reason, details);
            EventBus.Instance.Publish(evt);
            LoadFailed?.Invoke(evt);
        }

private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateCurrentScene(scene);
        }

        private void UpdateCurrentScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                currentSceneName = string.Empty;
                currentScenePath = string.Empty;
                return;
            }

            currentSceneName = scene.name;
            currentScenePath = scene.path;
        }
    }

    public enum SceneLoadRequestStatus
    {
        Accepted,
        AlreadyLoadingSameScene,
        AnotherSceneIsLoading,
        Rejected,
        Failed
    }

    public enum SceneLoadFailureReason
    {
        EmptySceneName,
        InvalidBuildIndex,
        SceneNotInBuildSettings,
        AnotherSceneIsLoading,
        AlreadyLoadingSameScene,
        LoadOperationUnavailable,
        LoadException,
        CancelNotSupported
    }

    [Serializable]
    public readonly struct SceneLoadTarget : IEquatable<SceneLoadTarget>
    {
        public readonly string Name;
        public readonly int BuildIndex;
        public readonly LoadSceneMode Mode;

        public bool HasName => !string.IsNullOrEmpty(Name);

        public SceneLoadTarget(string name, int buildIndex, LoadSceneMode mode)
        {
            Name = name ?? string.Empty;
            BuildIndex = buildIndex;
            Mode = mode;
        }

        public bool Equals(SceneLoadTarget other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                   BuildIndex == other.BuildIndex && Mode == other.Mode;
        }

        public override bool Equals(object obj)
        {
            return obj is SceneLoadTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397 + BuildIndex) * 397 + (int)Mode;
            }
        }
    }

    public readonly struct SceneLoadStarted
    {
        public readonly SceneLoadTarget Target;
        public readonly string PreviousSceneName;
        public readonly string PreviousScenePath;

        public SceneLoadStarted(SceneLoadTarget target, string previousSceneName, string previousScenePath)
        {
            Target = target;
            PreviousSceneName = previousSceneName;
            PreviousScenePath = previousScenePath;
        }
    }

    public readonly struct SceneLoadProgressChanged
    {
        public readonly SceneLoadTarget Target;
        public readonly float Progress;

        public SceneLoadProgressChanged(SceneLoadTarget target, float progress)
        {
            Target = target;
            Progress = progress;
        }
    }

    public readonly struct SceneLoadCompleted
    {
        public readonly SceneLoadTarget Target;
        public readonly string SceneName;
        public readonly string ScenePath;

        public SceneLoadCompleted(SceneLoadTarget target, string sceneName, string scenePath)
        {
            Target = target;
            SceneName = sceneName;
            ScenePath = scenePath;
        }
    }

    public readonly struct SceneLoadFailed
    {
        public readonly SceneLoadTarget Target;
        public readonly SceneLoadFailureReason Reason;
        public readonly string Details;

        public SceneLoadFailed(SceneLoadTarget target, SceneLoadFailureReason reason, string details = "")
        {
            Target = target;
            Reason = reason;
            Details = details ?? string.Empty;
        }
    }
}
