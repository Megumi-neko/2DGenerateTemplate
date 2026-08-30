using UnityEngine;
using UnityEngine.UI;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Building Health Bar")]
    [DisallowMultipleComponent]
    public sealed class BuildingHealthBar : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0.04f, 1.1f, 0f);
        [SerializeField] private Vector2 size = new Vector2(1.25f, 0.12f);
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        [SerializeField] private Color fillColor = new Color(0.95f, 0.05f, 0.05f, 0.98f);

        private BuildingHealth health;
        private Slider slider;

        public void SetOffset(Vector3 value)
        {
            offset = value;
            Transform bar = transform.Find("Building Health Bar");
            if (bar != null)
            {
                bar.localPosition = offset;
            }
        }

        public void Initialize(BuildingHealth target)
        {
            health = target;
            if (slider == null) CreateBar();
            Refresh();
        }

        private void Awake()
        {
            if (health == null) health = GetComponent<BuildingHealth>();
            if (health != null) Initialize(health);
        }

        private void Update()
        {
            if (health == null || slider == null) return;
            slider.value = health.NormalizedHealth;
        }

        private void CreateBar()
        {
            GameObject canvasObject = new GameObject("Building Health Bar", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = offset;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 25;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = size;

            GameObject sliderObject = new GameObject("Health", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
            slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;

            Image background = sliderObject.AddComponent<Image>();
            background.color = backgroundColor;
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(sliderObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillObject.GetComponent<Image>().color = fillColor;
            slider.fillRect = fillRect;
            slider.transition = Selectable.Transition.None;
        }

        private void Refresh()
        {
            if (slider != null && health != null) slider.value = health.NormalizedHealth;
        }
    }
}
