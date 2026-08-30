using System.Collections;
using Game.Lighting;
using UnityEngine;

namespace Game.Combat
{
    [AddComponentMenu("Game/Combat/Candle Hit Flash")]
    [DisallowMultipleComponent]
    public sealed class CandleHitFlash : MonoBehaviour
    {
        [SerializeField] private Color hitColor = new Color(1f, 0.08f, 0.08f, 1f);
        [SerializeField, Min(0.01f)] private float flashDuration = 0.16f;

        private SpriteRenderer[] renderers;
        private Color[] baseColors;
        private Coroutine flashRoutine;

        public static void FlashMainCandle()
        {
            StageLightingBootstrap bootstrap = FindObjectOfType<StageLightingBootstrap>();
            GameObject candle = bootstrap == null ? null : bootstrap.CentralCandle;
            if (candle == null) return;
            CandleHitFlash flash = candle.GetComponent<CandleHitFlash>();
            if (flash == null) flash = candle.AddComponent<CandleHitFlash>();
            flash.Flash();
        }

        public void Flash()
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private void Awake()
        {
            CacheRenderers();
        }

        private IEnumerator FlashRoutine()
        {
            if (renderers == null) CacheRenderers();
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = hitColor;

            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, flashDuration));
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = baseColors[i];
            flashRoutine = null;
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i] == null ? Color.white : renderers[i].color;
        }

        private void OnValidate()
        {
            flashDuration = Mathf.Max(0.01f, flashDuration);
        }
    }
}
