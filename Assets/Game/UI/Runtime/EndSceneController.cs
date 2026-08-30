using System.Collections;
using Game.BaseSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class EndSceneController : MonoBehaviour
    {
        [SerializeField] private EndOutcome defaultOutcome = EndOutcome.GameOver;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField, Min(0.01f)] private float fadeDuration = 6f;
        [SerializeField, Min(0.01f)] private float fallDuration = 2.2f;
        [SerializeField, Min(0f)] private float startHeight = 180f;
        [SerializeField, Min(0f)] private float bounceHeight = 15f;
        [SerializeField, Min(1)] private int bounceCount = 3;
        [SerializeField, Min(0.01f)] private float bounceDuration = 1.1f;
        [SerializeField, Min(0.01f)] private float promptPulseDuration = 1.2f;
        [SerializeField] private string promptText = "点击任意位置回到主界面";
        [SerializeField] private Vector2 promptPosition = new Vector2(0f, -260f);

        [SerializeField] private Canvas endCanvas;
        [SerializeField] private GameObject gameOverObject;
        [SerializeField] private GameObject victoryObject;
        [SerializeField] private SpriteRenderer gameOverRenderer;
        [SerializeField] private SpriteRenderer victoryRenderer;
        [SerializeField] private Text prompt;
        private CanvasGroup promptGroup;
        private bool ready;
        private bool loading;

        private void Awake()
        {
            if (endCanvas == null) endCanvas = FindObjectOfType<Canvas>();
            if (endCanvas != null) endCanvas.transform.localScale = Vector3.one;
            if (gameOverObject == null) gameOverObject = FindTitlePanel("GameOver");
            if (victoryObject == null) victoryObject = FindTitlePanel("YouWin");
            if (gameOverRenderer == null && gameOverObject != null)
                gameOverRenderer = gameOverObject.GetComponentInChildren<SpriteRenderer>(true);
            if (victoryRenderer == null && victoryObject != null)
                victoryRenderer = victoryObject.GetComponentInChildren<SpriteRenderer>(true);
            if (prompt == null) prompt = FindTextByName("Text (Legacy) (1)");
            if (prompt != null)
            {
                Canvas promptCanvas = endCanvas;
                RectTransform promptRect = prompt.rectTransform;
                Vector2 savedPosition = promptRect.anchoredPosition;
                Vector3 savedScale = promptRect.localScale;
                Vector2 savedSize = promptRect.sizeDelta;
                if (promptCanvas != null)
                {
                    prompt.transform.SetParent(promptCanvas.transform, false);
                    prompt.transform.SetAsLastSibling();
                    promptRect.anchoredPosition = savedPosition;
                    promptRect.localScale = savedScale;
                    promptRect.sizeDelta = savedSize;
                }
                prompt.text = promptText;
                promptGroup = prompt.GetComponent<CanvasGroup>();
                if (promptGroup == null)
                {
                    promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
                }
                if (promptGroup != null)
                {
                    promptGroup.alpha = 0f;
                }
            }
            EndOutcome outcome = EndSceneFlow.ConsumeOutcome(defaultOutcome);
            SetVisible(gameOverObject, outcome == EndOutcome.GameOver);
            SetVisible(victoryObject, outcome == EndOutcome.Victory);
            StartCoroutine(PlayOutcome(outcome));
        }

        private void Update()
        {
            if (ready && !loading && (Input.GetMouseButtonDown(0) || Input.touchCount > 0 || Input.anyKeyDown))
                ReturnToMainMenu();
        }

        private IEnumerator PlayOutcome(EndOutcome outcome)
        {
            if (outcome == EndOutcome.GameOver)
            {
                SetAlpha(gameOverRenderer, 0f);
                yield return FadeSprite(gameOverRenderer, 1f, fadeDuration);
            }
            else
            {
                SetAlpha(victoryRenderer, 1f);
                if (victoryObject == null)
                {
                    ready = true;
                    StartPromptPulse();
                    yield break;
                }
                Transform t = victoryRenderer == null ? victoryObject.transform : victoryRenderer.transform;
                Vector3 target = t.localPosition;
                t.localPosition = target + Vector3.up * startHeight;
                yield return Move(t, target + Vector3.up * startHeight, target, fallDuration);
                for (int i = 0; i < bounceCount; i++)
                    yield return Bounce(t, target, bounceHeight / (i + 1f), bounceDuration);
                t.localPosition = target;
            }
            ready = true;
            StartPromptPulse();
        }

        private void StartPromptPulse()
        {
            if (prompt != null && promptGroup != null)
            {
                prompt.enabled = true;
                promptGroup.alpha = 0f;
                StartCoroutine(PulsePrompt());
            }
        }

        private IEnumerator FadeSprite(SpriteRenderer renderer, float target, float duration)
        {
            if (renderer == null) yield break;
            Color color = renderer.color;
            float start = color.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                renderer.color = color;
                yield return null;
            }
            color.a = target;
            renderer.color = color;
        }

        private IEnumerator Move(Transform target, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                p = p * p * (3f - 2f * p);
                target.localPosition = Vector3.Lerp(from, to, p);
                yield return null;
            }
        }

        private IEnumerator Bounce(Transform target, Vector3 position, float height, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                target.localPosition = position + Vector3.up * (Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) * height);
                yield return null;
            }
        }

        private IEnumerator PulsePrompt()
        {
            while (!loading)
            {
                yield return FadeCanvas(1f);
                yield return FadeCanvas(0f);
            }
        }

        private IEnumerator FadeCanvas(float target)
        {
            float start = promptGroup.alpha;
            float elapsed = 0f;
            while (elapsed < promptPulseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                promptGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / promptPulseDuration));
                yield return null;
            }
            promptGroup.alpha = target;
        }

        private void ReturnToMainMenu()
        {
            loading = true;
            ready = false;
            StartCoroutine(LoadMainMenuNextFrame());
        }

        private IEnumerator LoadMainMenuNextFrame()
        {
            yield return null;
#if UNITY_EDITOR
            UnityEditor.Selection.activeObject = null;
#endif
            if (SceneManagerSystem.HasInstance &&
                SceneManagerSystem.Instance.LoadScene(mainMenuSceneName) == SceneLoadRequestStatus.Accepted)
            {
                yield break;
            }
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private static void SetVisible(GameObject obj, bool visible) { if (obj != null) obj.SetActive(visible); }
        private static void SetAlpha(SpriteRenderer renderer, float alpha) { if (renderer != null) { Color c = renderer.color; c.a = alpha; renderer.color = c; } }
        private static GameObject FindTitlePanel(string name)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null || candidate.name != name || !candidate.gameObject.scene.IsValid()) continue;
                if (candidate.GetComponent<RectTransform>() != null &&
                    candidate.parent != null && candidate.parent.GetComponent<Canvas>() != null)
                {
                    return candidate.gameObject;
                }
            }
            return null;
        }

        private static Text FindTextByName(string name) { Text[] all = Resources.FindObjectsOfTypeAll<Text>(); foreach (Text text in all) if (text != null && text.gameObject.scene.IsValid() && text.name == name) return text; return null; }
    }
}
