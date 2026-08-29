using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Plays the MainMenu entrance animation and adds animated feedback to its buttons.
    /// </summary>
    [AddComponentMenu("Game/UI/Main Menu UI Controller")]
    [DisallowMultipleComponent]
    public sealed class MainMenuUIController : MonoBehaviour
    {
        private const float MinimumDuration = 0.05f;

        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform contributerButton;
        [SerializeField] private RectTransform exitButton;
        [SerializeField] private Graphic title;
        [SerializeField, Min(MinimumDuration)] private float entranceDuration = 1.8f;
        [SerializeField, Min(0f)] private float titleDelay = 0.35f;
        [SerializeField, Min(0f)] private float buttonDelay = 0.18f;
        [SerializeField, Min(0f)] private float startOffset = 450f;
        [SerializeField, Range(1f, 1.5f)] private float hoverScale = 1.08f;
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.92f;
        [SerializeField, Min(MinimumDuration)] private float feedbackDuration = 0.12f;

        private readonly List<MainMenuButtonAnimation> buttonAnimations =
            new List<MainMenuButtonAnimation>();
        private readonly List<RectTransform> entranceButtons = new List<RectTransform>();
        private readonly List<Vector2> entranceEndPositions = new List<Vector2>();
        private Tween entranceTween;
        private Tween titleTween;

        private void Awake()
        {
            ResolveReferences();
            ConfigureButtonAnimations();
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
            KillEntranceTweens();
        }

        private void OnValidate()
        {
            entranceDuration = Mathf.Max(MinimumDuration, entranceDuration);
            titleDelay = Mathf.Max(0f, titleDelay);
            buttonDelay = Mathf.Max(0f, buttonDelay);
            startOffset = Mathf.Max(0f, startOffset);
            hoverScale = Mathf.Max(1f, hoverScale);
            pressedScale = Mathf.Clamp(pressedScale, 0.5f, 1f);
            feedbackDuration = Mathf.Max(MinimumDuration, feedbackDuration);
        }

        private void ResolveReferences()
        {
            if (startButton == null)
            {
                startButton = FindButtonRectTransform("Start");
            }

            if (contributerButton == null)
            {
                contributerButton = FindButtonRectTransform("Contributer");
            }

            if (exitButton == null)
            {
                exitButton = FindButtonRectTransform("Exit");
            }

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

        private RectTransform FindButtonRectTransform(string objectName)
        {
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

            for (int i = 0; i < entranceButtons.Count; i++)
            {
                RectTransform button = entranceButtons[i];
                Vector2 endPosition = entranceEndPositions[i];
                button.anchoredPosition = endPosition + Vector2.left * startOffset;
                sequence.Insert(
                    i * buttonDelay,
                    button.DOAnchorPos(endPosition, entranceDuration).SetEase(Ease.OutCubic));
            }

            entranceTween = sequence;
            if (title != null)
            {
                titleTween = title
                    .DOFade(1f, entranceDuration)
                    .From(0f)
                    .SetDelay(titleDelay)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        private void CacheEntranceButtons()
        {
            if (entranceButtons.Count > 0)
            {
                return;
            }

            AddEntranceButton(startButton);
            AddEntranceButton(contributerButton);
            AddEntranceButton(exitButton);
        }

        private void AddEntranceButton(RectTransform button)
        {
            if (button == null)
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

            if (titleTween != null)
            {
                titleTween.Kill();
                titleTween = null;
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
