using UnityEngine;

namespace Game.Lighting.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class IlluminationProbeVisual : MonoBehaviour
    {
        [SerializeField] private Color unlitColor = new Color(0.18f, 0.22f, 0.3f, 1f);
        [SerializeField] private Color litColor = new Color(1f, 0.38f, 0.08f, 1f);

        private Renderer targetRenderer;
        private MaterialPropertyBlock propertyBlock;

        public IlluminationSample CurrentSample { get; private set; }

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            Vector3 position = transform.position;
            CurrentSample = IlluminationSystem.Sample(new Vector2(position.x, position.y));
            float illumination = Mathf.Clamp01(CurrentSample.Intensity);
            Color displayColor = Color.Lerp(unlitColor, litColor, illumination);

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", displayColor);
            propertyBlock.SetColor("_BaseColor", displayColor);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
