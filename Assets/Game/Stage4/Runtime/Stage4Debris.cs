using System;
using Game.Building;
using Game.Lighting;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Stage4
{
    [DisallowMultipleComponent]
    public sealed class Stage4Debris : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Slider progress;
        private Image progressFill;
        private GameObject progressRoot;
        private LightEmitter2D revealEmitter;
        private CoinInventory coins;
        private float repairRate;
        private int reward;
        private bool revealed;
        private bool rewarded;
        private float repaired;
        private float required;
        private Action<Stage4Debris> completed;

        public bool IsRevealed => revealed;
        public float Repair01 => required <= 0f ? 1f : Mathf.Clamp01(repaired / required);

        public void Initialize(Sprite sprite, CoinInventory inventory, float repairRequired,
            float repairRatePerIntensity, int coinReward, Vector3 barOffset, Vector2 barSize,
            Action<Stage4Debris> onCompleted = null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            spriteRenderer.sortingOrder = 4;
            coins = inventory;
            required = Mathf.Max(0.01f, repairRequired);
            repairRate = Mathf.Max(0f, repairRatePerIntensity);
            reward = Mathf.Max(0, coinReward);
            completed = onCompleted;
            CreateProgressBar(barOffset, barSize);
        }

        private void Update()
        {
            if (!revealed && IlluminationSystem.IsLit(transform.position))
            {
                revealed = true;
                spriteRenderer.color = Color.white;
                if (progressRoot != null) progressRoot.SetActive(true);
                revealEmitter = gameObject.AddComponent<LightEmitter2D>();
                revealEmitter.Shape = LightShape2D.Circle;
                revealEmitter.BaseRadius = 0.55f;
                revealEmitter.BaseIntensity = 0.01f;
                revealEmitter.BaseDamagePerSecond = 0f;
            }
            if (!revealed) return;

            IlluminationSample sample = IlluminationSystem.Sample(transform.position);
            float persistentLightIntensity = revealEmitter == null ? 0f : revealEmitter.CurrentIntensity;
            float repairIntensity = Mathf.Max(0f, sample.Intensity - persistentLightIntensity);
            UpdateProgressColor(repairIntensity, sample.StrongestSource, revealEmitter);
            if (sample.IsLit && repairIntensity > 0.0001f && repairRate > 0f)
            {
                repaired += repairIntensity * repairRate * Time.deltaTime;
                if (progress != null) progress.value = Repair01;
                if (repaired >= required) Complete();
            }
        }

        private void UpdateProgressColor(
            float intensity,
            LightEmitter2D strongestSource,
            LightEmitter2D persistentEmitter)
        {
            if (progressFill == null) return;
            Color green = new Color(0.2f, 1f, 0.35f, 0.95f);
            Color blue = new Color(0.1f, 0.4f, 1f, 0.98f);
            if (strongestSource == null || strongestSource == persistentEmitter || intensity <= 0.0001f)
            {
                progressFill.color = green;
                return;
            }

            float maximumIntensity = strongestSource.BaseIntensity * strongestSource.MaximumFocusMultiplier;
            float normalizedIntensity = intensity / Mathf.Max(0.01f, maximumIntensity);
            progressFill.color = Color.Lerp(green, blue, Mathf.Clamp01(normalizedIntensity));
        }

        private void Complete()
        {
            if (rewarded) return;
            rewarded = true;
            coins?.Add(reward);
            completed?.Invoke(this);
            Destroy(gameObject);
        }

        private void CreateProgressBar(Vector3 offset, Vector2 size)
        {
            GameObject canvasObject = new GameObject("Repair Progress", typeof(Canvas), typeof(CanvasScaler));
            progressRoot = canvasObject;
            progressRoot.SetActive(false);
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = offset;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            canvasObject.GetComponent<RectTransform>().sizeDelta = size;
            GameObject sliderObject = new GameObject("Progress", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            progress = sliderObject.GetComponent<Slider>();
            progress.minValue = 0f;
            progress.maxValue = 1f;
            progress.value = 0f;
            progress.interactable = false;
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(sliderObject.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            progressFill = fillImage;
            fillImage.color = new Color(0.2f, 1f, 0.35f, 0.95f);
            progress.fillRect = fillRect;
        }
    }
}
