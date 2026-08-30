using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Controls the MainMenu entrance animation, button feedback, and menu interactions.
    /// </summary>
    [AddComponentMenu("Game/UI/Main Menu UI Controller")]
    [DisallowMultipleComponent]
    public sealed class MainMenuUIController : MonoBehaviour
    {
        private const float MinimumDuration = 0.05f;

        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform contributerButton;
        [SerializeField] private RectTransform exitButton;

        [Header("Menu Panels")]
        [SerializeField] private GameObject bookPanel;
        [SerializeField] private GameObject contributersPanel;
        [SerializeField] private Button bookButton;
        [SerializeField] private Button contributersMenuButton;
        [SerializeField] private Button bookCloseButton;
        [SerializeField] private Button contributersCloseButton;
        [SerializeField] private Button nextPageButton;

        [SerializeField] private Graphic title;
        [SerializeField, Min(MinimumDuration)] private float buttonEntranceDuration = 0.45f;
        [SerializeField, Min(MinimumDuration)] private float entranceDuration = 1.8f;
        [SerializeField, Min(0f)] private float titleDelay = 0.35f;
        [SerializeField, Min(0f)] private float buttonDelay = 0.18f;
        [SerializeField, Min(0f)] private float startOffset = 450f;
        [SerializeField, Range(1f, 1.5f)] private float hoverScale = 1.08f;
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.92f;
        [SerializeField, Min(MinimumDuration)] private float feedbackDuration = 0.12f;

        private readonly List<GameObject> bookPages = new List<GameObject>();
        private readonly List<MainMenuButtonAnimation> buttonAnimations =
            new List<MainMenuButtonAnimation>();
        private readonly List<RectTransform> entranceButtons = new List<RectTransform>();
        private readonly List<Vector2> entranceEndPositions = new List<Vector2>();
        private Tween entranceTween;
        private int currentBookPage;

        private void Awake()
        {
            ResolveReferences();
            ConfigureButtonAnimations();
            ConfigureMenuInteractions();
            CollectBookPages();
            ResetBookPage();
        }

        private void Start()
        {
            PlayEntranceAnimation();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            KillEntranceTweens();
            for (int i = 0; i < buttonAnimations.Count; i++)
            {
                if (buttonAnimations[i] != null)
                {
                    buttonAnimations[i].ResetAnimation();
                }
            }
        }

        private void OnDestroy()
        {
            RemoveMenuInteractions();
            KillEntranceTweens();
        }

        private void OnValidate()
        {
            entranceDuration = Mathf.Max(MinimumDuration, entranceDuration);
            buttonEntranceDuration = Mathf.Max(MinimumDuration, buttonEntranceDuration);
            titleDelay = Mathf.Max(0f, titleDelay);
            buttonDelay = Mathf.Max(0f, buttonDelay);
            startOffset = Mathf.Max(0f, startOffset);
            hoverScale = Mathf.Max(1f, hoverScale);
            pressedScale = Mathf.Clamp(pressedScale, 0.5f, 1f);
            feedbackDuration = Mathf.Max(MinimumDuration, feedbackDuration);
        }

        private void ConfigureMenuInteractions()
        {
            AddListener(GetButton(startButton), StartGame);
            AddListener(bookButton, OpenBookPanel);
            AddListener(contributersMenuButton, OpenContributersPanel);
            AddListener(GetButton(exitButton), ExitGame);
            AddListener(bookCloseButton, CloseBookPanel);
            AddListener(contributersCloseButton, CloseContributersPanel);
            AddListener(nextPageButton, ShowNextBookPage);
        }

        private void RemoveMenuInteractions()
        {
            RemoveListener(GetButton(startButton), StartGame);
            RemoveListener(bookButton, OpenBookPanel);
            RemoveListener(contributersMenuButton, OpenContributersPanel);
            RemoveListener(GetButton(exitButton), ExitGame);
            RemoveListener(bookCloseButton, CloseBookPanel);
            RemoveListener(contributersCloseButton, CloseContributersPanel);
            RemoveListener(nextPageButton, ShowNextBookPage);
        }

        private static Button GetButton(RectTransform buttonTransform)
        {
            return buttonTransform == null ? null : buttonTransform.GetComponent<Button>();
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private void OpenBookPanel()
        {
            if (contributersPanel != null)
            {
                contributersPanel.SetActive(false);
            }

            if (bookPanel != null)
            {
                bookPanel.SetActive(true);
            }

            ResetBookPage();
        }

        private void CloseBookPanel()
        {
            if (bookPanel != null)
            {
                bookPanel.SetActive(false);
            }
        }

        private void OpenContributersPanel()
        {
            if (bookPanel != null)
            {
                bookPanel.SetActive(false);
            }

            if (contributersPanel != null)
            {
                contributersPanel.SetActive(true);
            }
        }

        private void CloseContributersPanel()
        {
            if (contributersPanel != null)
            {
                contributersPanel.SetActive(false);
            }
        }

        private void StartGame()
        {
            Game.BaseSystem.SceneManagerSystem.Instance.LoadScene("Stage 4");
        }

        private void ExitGame()
        {
            Application.Quit();
        }

        private void CollectBookPages()
        {
            bookPages.Clear();
            if (bookPanel == null)
            {
                return;
            }

            for (int i = 0; i < bookPanel.transform.childCount; i++)
            {
                Transform child = bookPanel.transform.GetChild(i);
                int pageNumber;
                if (child.GetComponent<Text>() != null &&
                    TryGetLeadingNumber(child.name, out pageNumber))
                {
                    bookPages.Add(child.gameObject);
                }
            }

            bookPages.Sort((left, right) =>
                GetPageNumber(left.name).CompareTo(GetPageNumber(right.name)));
        }

        private static bool TryGetLeadingNumber(string objectName, out int pageNumber)
        {
            pageNumber = 0;
            int digitCount = 0;
            while (digitCount < objectName.Length && char.IsDigit(objectName[digitCount]))
            {
                digitCount++;
            }

            return digitCount > 0 &&
                   int.TryParse(objectName.Substring(0, digitCount), out pageNumber);
        }

        private static int GetPageNumber(string objectName)
        {
            int pageNumber;
            return TryGetLeadingNumber(objectName, out pageNumber)
                ? pageNumber
                : int.MaxValue;
        }

        private void ResetBookPage()
        {
            currentBookPage = 0;
            RefreshBookPage();
        }

        private void ShowNextBookPage()
        {
            if (currentBookPage < bookPages.Count - 1)
            {
                currentBookPage++;
            }

            RefreshBookPage();
        }

        private void RefreshBookPage()
        {
            for (int i = 0; i < bookPages.Count; i++)
            {
                if (bookPages[i] != null)
                {
                    bookPages[i].SetActive(i == currentBookPage);
                }
            }

            if (nextPageButton != null)
            {
                nextPageButton.interactable = bookPages.Count > 0 &&
                                               currentBookPage < bookPages.Count - 1;
            }
        }

        private void ResolvePanelReferences()
        {
            if (bookPanel == null)
            {
                Transform panel = transform.Find("BookPanel");
                bookPanel = panel == null ? null : panel.gameObject;
            }

            if (contributersPanel == null)
            {
                Transform panel = transform.Find("ContributersPanel");
                contributersPanel = panel == null ? null : panel.gameObject;
            }

            if (bookButton == null)
            {
                bookButton = FindButton("Book");
            }

            if (contributersMenuButton == null)
            {
                contributersMenuButton = FindButton("Contribute");
            }

            if (bookCloseButton == null)
            {
                bookCloseButton = FindChildButton(bookPanel, "Close");
            }

            if (contributersCloseButton == null)
            {
                contributersCloseButton = FindChildButton(contributersPanel, "Close");
            }

            if (nextPageButton == null)
            {
                nextPageButton = FindChildButton(bookPanel, "NextPage");
            }
        }

        private Button FindButton(string objectName)
        {
            Transform button = transform.Find(objectName);
            return button == null ? null : button.GetComponent<Button>();
        }

        private static Button FindChildButton(GameObject parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform button = parent.transform.Find(objectName);
            return button == null ? null : button.GetComponent<Button>();
        }

        private void ResolveReferences()
        {
            ResolvePanelReferences();
            startButton = ResolveButtonRectTransform(startButton, "Start");
            contributerButton = ResolveButtonRectTransform(contributerButton, "Contribute");
            exitButton = ResolveButtonRectTransform(exitButton, "Exit");

            if (title == null)
            {
                Text[] texts = GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].text == "标题" || texts[i].name == "标题")
                    {
                        title = texts[i];
                        break;
                    }
                }
            }
        }

        private RectTransform ResolveButtonRectTransform(
            RectTransform currentReference,
            string objectName)
        {
            if (currentReference != null && currentReference.name == objectName)
            {
                return currentReference;
            }

            Transform button = transform.Find(objectName);
            return button as RectTransform;
        }

        private void ConfigureButtonAnimations()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                MainMenuButtonAnimation animation =
                    buttons[i].GetComponent<MainMenuButtonAnimation>();
                if (animation == null)
                {
                    animation = buttons[i].gameObject.AddComponent<MainMenuButtonAnimation>();
                }

                animation.Configure(hoverScale, pressedScale, feedbackDuration);
                if (!buttonAnimations.Contains(animation))
                {
                    buttonAnimations.Add(animation);
                }
            }
        }

        private void PlayEntranceAnimation()
        {
            KillEntranceTweens();
            CacheEntranceButtons();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            if (title != null)
            {
                Color titleColor = title.color;
                titleColor.a = 0f;
                title.color = titleColor;
                sequence.AppendInterval(titleDelay);
                sequence.Append(
                    title.DOFade(1f, entranceDuration)
                        .SetEase(Ease.OutQuad));
            }

            float buttonStartTime = sequence.Duration();
            for (int i = 0; i < entranceButtons.Count; i++)
            {
                RectTransform button = entranceButtons[i];
                Vector2 endPosition = entranceEndPositions[i];
                button.anchoredPosition = endPosition + Vector2.left * startOffset;
                sequence.Insert(
                    buttonStartTime + i * buttonDelay,
                    button.DOAnchorPos(endPosition, buttonEntranceDuration)
                        .SetEase(Ease.OutQuart));
            }

            entranceTween = sequence;
        }

        private void CacheEntranceButtons()
        {
            if (entranceButtons.Count > 0)
            {
                return;
            }

            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                AddEntranceButton(buttons[i].transform as RectTransform);
            }
        }

        private void AddEntranceButton(RectTransform button)
        {
            if (button == null || entranceButtons.Contains(button))
            {
                return;
            }

            entranceButtons.Add(button);
            entranceEndPositions.Add(button.anchoredPosition);
        }

        private void KillEntranceTweens()
        {
            if (entranceTween != null)
            {
                entranceTween.Kill();
                entranceTween = null;
            }

            for (int i = 0; i < entranceButtons.Count; i++)
            {
                if (entranceButtons[i] != null)
                {
                    entranceButtons[i].DOKill();
                }
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class MainMenuButtonAnimation : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        private const float MinimumDuration = 0.05f;

        private float hoverScale = 1.08f;
        private float pressedScale = 0.92f;
        private float duration = 0.12f;
        private Vector3 normalScale;
        private Tween scaleTween;
        private bool isPointerOver;
        private bool isPointerDown;

        private void Awake()
        {
            normalScale = transform.localScale;
        }

        public void Configure(float configuredHoverScale, float configuredPressedScale, float configuredDuration)
        {
            hoverScale = configuredHoverScale;
            pressedScale = configuredPressedScale;
            duration = Mathf.Max(MinimumDuration, configuredDuration);
            if (normalScale == Vector3.zero)
            {
                normalScale = transform.localScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOver = true;
            if (!isPointerDown)
            {
                TweenTo(normalScale * hoverScale);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerOver = false;
            if (!isPointerDown)
            {
                TweenTo(normalScale);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPointerDown = true;
            TweenTo(normalScale * pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
            TweenTo(isPointerOver ? normalScale * hoverScale : normalScale);
        }

        public void ResetAnimation()
        {
            isPointerOver = false;
            isPointerDown = false;
            KillScaleTween();
            transform.localScale = normalScale;
        }

        private void OnDestroy()
        {
            KillScaleTween();
        }

        private void TweenTo(Vector3 targetScale)
        {
            KillScaleTween();
            scaleTween = transform
                .DOScale(targetScale, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void KillScaleTween()
        {
            if (scaleTween != null)
            {
                scaleTween.Kill();
                scaleTween = null;
            }
        }
    }
}
