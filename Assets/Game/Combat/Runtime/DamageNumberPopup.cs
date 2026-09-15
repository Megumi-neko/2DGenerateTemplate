using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat
{
    [DisallowMultipleComponent]
    public sealed class DamageNumberPopup : MonoBehaviour
    {
        [SerializeField] private Text text;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0.01f)] private float visibleDuration = 1.5f;
        [SerializeField, Min(0f)] private float riseDistance = 0.6f;
        [SerializeField, Min(0.01f)] private float startScale = 1.2f;
        [SerializeField] private Color startDamageColor = new Color(1f, 0.65f, 0.65f, 1f);
        [SerializeField, Min(0.01f)] private float redApproachDamage = 100f;

        private Transform owner;
        private Vector3 baseWorldPosition;
        private float displayedDamage;
        private float animationTime;

        public bool IsPlaying => owner != null && animationTime < visibleDuration;
        public Transform Owner => owner;
        public float DisplayedDamage => displayedDamage;

        private void Awake()
        {
            ResolveReferences();
            Hide();
        }

        private void Update()
        {
            if (!IsPlaying || !owner.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            animationTime += Time.deltaTime;
            if (animationTime >= visibleDuration)
            {
                Hide();
                return;
            }

            float normalizedTime = Mathf.Clamp01(animationTime / visibleDuration);
            transform.position = baseWorldPosition + Vector3.up *
                (riseDistance * EaseOutCubic(normalizedTime));
            transform.localScale = Vector3.one *
                Mathf.Lerp(startScale, 1f, EaseOutBack(normalizedTime));
            if (canvasGroup != null)
            {
                canvasGroup.alpha = normalizedTime < 0.65f
                    ? 1f
                    : 1f - Mathf.InverseLerp(0.65f, 1f, normalizedTime);
            }

            Camera targetCamera = Camera.main;
            if (targetCamera != null)
            {
                transform.rotation = targetCamera.transform.rotation;
            }
        }

        public void Show(Transform newOwner, float damage, Vector3 worldOffset)
        {
            if (newOwner == null || damage <= 0f || !IsFinite(damage))
            {
                return;
            }

            owner = newOwner;
            displayedDamage = damage;
            animationTime = 0f;
            baseWorldPosition = owner.position + worldOffset;
            transform.position = baseWorldPosition;
            transform.localScale = Vector3.one * startScale;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            RenderDamage();
        }

        public void Hide()
        {
            owner = null;
            displayedDamage = 0f;
            animationTime = visibleDuration;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            transform.localScale = Vector3.one;
        }

        private void RenderDamage()
        {
            if (text == null)
            {
                return;
            }

            text.text = FormatDamage(displayedDamage);
            float redFactor = 1f - Mathf.Exp(-displayedDamage / redApproachDamage);
            text.color = Color.Lerp(startDamageColor, Color.red, redFactor);
        }

        private void ResolveReferences()
        {
            if (text == null)
            {
                text = GetComponentInChildren<Text>(true);
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        public static string FormatDamage(float damage)
        {
            return Mathf.RoundToInt(Mathf.Max(0f, damage)).ToString();
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                overshoot * shifted * shifted;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void OnValidate()
        {
            visibleDuration = Mathf.Max(0.01f, visibleDuration);
            riseDistance = Mathf.Max(0f, riseDistance);
            startScale = Mathf.Max(0.01f, startScale);
            redApproachDamage = Mathf.Max(0.01f, redApproachDamage);
        }
    }
}
