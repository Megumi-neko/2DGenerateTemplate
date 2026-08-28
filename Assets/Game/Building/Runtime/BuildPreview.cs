using UnityEngine;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Build Preview")]
    [DisallowMultipleComponent]
    public sealed class BuildPreview : MonoBehaviour
    {
        private static readonly Color ValidColor = new Color(0.25f, 1f, 0.35f, 0.65f);
        private static readonly Color InvalidColor = new Color(1f, 0.2f, 0.2f, 0.65f);

        private GameObject previewObject;
        private BuildDefinition currentDefinition;
        private SpriteRenderer[] spriteRenderers;
        private Collider2D[] colliders;

        public bool IsVisible => previewObject != null && previewObject.activeSelf;

        public void Show(
            BuildDefinition definition,
            Vector3 worldPosition,
            bool isValid)
        {
            if (definition == null || definition.Prefab == null)
            {
                Hide();
                return;
            }

            if (previewObject == null || currentDefinition != definition)
            {
                DestroyPreviewObject();
                previewObject = Instantiate(definition.Prefab, transform);
                previewObject.name = $"{definition.DisplayName} Preview";
                currentDefinition = definition;
                spriteRenderers = previewObject.GetComponentsInChildren<SpriteRenderer>(true);
                colliders = previewObject.GetComponentsInChildren<Collider2D>(true);
                foreach (Collider2D collider in colliders)
                {
                    collider.enabled = false;
                }
            }

            previewObject.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            previewObject.SetActive(true);
            Color color = isValid ? ValidColor : InvalidColor;
            foreach (SpriteRenderer renderer in spriteRenderers)
            {
                renderer.color = color;
            }
        }

        public void Hide()
        {
            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            DestroyPreviewObject();
        }

        private void DestroyPreviewObject()
        {
            if (previewObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(previewObject);
            }
            else
            {
                DestroyImmediate(previewObject);
            }

            previewObject = null;
            currentDefinition = null;
            spriteRenderers = null;
            colliders = null;
        }
    }
}
