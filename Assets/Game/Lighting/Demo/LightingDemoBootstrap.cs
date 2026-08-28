using System.Collections.Generic;
using UnityEngine;

namespace Game.Lighting.Demo
{
    [DisallowMultipleComponent]
    public sealed class LightingDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector2Int gridCellCount = new Vector2Int(17, 11);
        [SerializeField, Min(0.25f)] private float cellSize = 1f;

        private readonly List<Material> runtimeMaterials = new List<Material>();
        private Transform generatedRoot;

        public LightEmitter2D ControlledLight { get; private set; }
        public LightEmitter2D SecondaryLight { get; private set; }

        private void Awake()
        {
            EnsureCamera();
            BuildDemoEnvironment();

            LightingDemoController controller = GetComponent<LightingDemoController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<LightingDemoController>();
            }

            controller.Initialize(targetCamera, ControlledLight);
        }

        private void OnDestroy()
        {
            foreach (Material material in runtimeMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            runtimeMaterials.Clear();
        }

        private void EnsureCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                GameObject cameraObject = new GameObject("Lighting Demo Camera");
                cameraObject.tag = "MainCamera";
                targetCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                targetCamera.transform.SetPositionAndRotation(
                    new Vector3(0f, -6f, -14f),
                    Quaternion.Euler(-20f, 0f, 0f));
                targetCamera.fieldOfView = 55f;
                targetCamera.nearClipPlane = 0.1f;
                targetCamera.farClipPlane = 100f;
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
            }

            DarknessOverlayEffect overlay = targetCamera.GetComponent<DarknessOverlayEffect>();
            if (overlay == null)
            {
                overlay = targetCamera.gameObject.AddComponent<DarknessOverlayEffect>();
            }

            overlay.GameplayPlaneZ = 0f;
            overlay.DarknessOpacity = 0.96f;
        }

        private void BuildDemoEnvironment()
        {
            GameObject rootObject = new GameObject("Generated Lighting Demo Content");
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(transform, false);

            Material groundMaterial = CreateMaterial(new Color(0.25f, 0.31f, 0.22f, 1f));
            Material gridMaterial = CreateMaterial(new Color(0.42f, 0.5f, 0.39f, 0.75f));
            Material probeMaterial = CreateMaterial(Color.white);
            Material primaryMarkerMaterial = CreateMaterial(new Color(1f, 0.68f, 0.12f, 1f));
            Material secondaryMarkerMaterial = CreateMaterial(new Color(0.2f, 0.78f, 1f, 1f));

            float width = gridCellCount.x * cellSize;
            float height = gridCellCount.y * cellSize;
            CreateQuad(
                "Ground",
                Vector3.zero + Vector3.forward * 0.2f,
                new Vector3(width, height, 1f),
                groundMaterial,
                generatedRoot);
            CreateGrid(width, height, gridMaterial);
            CreateProbes(probeMaterial);

            ControlledLight = CreateEmitter(
                "Primary Candle Light",
                new Vector2(-1.5f, 0f),
                3.25f,
                12f,
                primaryMarkerMaterial);
            ControlledLight.Shape = LightShape2D.Sector;
            ControlledLight.MinimumSectorAngle = 60f;
            ControlledLight.SectorAngle = 90f;
            ControlledLight.MaximumFocusMultiplier = 2.25f;
            ControlledLight.Direction = Vector2.right;

            SecondaryLight = CreateEmitter(
                "Secondary Candle Light",
                new Vector2(4f, 1.25f),
                2.2f,
                6f,
                secondaryMarkerMaterial);
            SecondaryLight.Shape = LightShape2D.Circle;
            SecondaryLight.BaseIntensity = 0.8f;
            SecondaryLight.SetEmitting(true);
        }

        private LightEmitter2D CreateEmitter(
            string objectName,
            Vector2 position,
            float radius,
            float damagePerSecond,
            Material markerMaterial)
        {
            GameObject emitterObject = new GameObject(objectName);
            emitterObject.transform.SetParent(generatedRoot, false);
            emitterObject.transform.position = new Vector3(position.x, position.y, -0.08f);

            LightEmitter2D emitter = emitterObject.AddComponent<LightEmitter2D>();
            emitter.BaseRadius = radius;
            emitter.BaseIntensity = 1f;
            emitter.BaseDamagePerSecond = damagePerSecond;
            emitter.EdgeSoftness = 0.35f;

            CreateQuad(
                "Candle Marker",
                emitterObject.transform.position + Vector3.back * 0.02f,
                new Vector3(0.55f, 0.55f, 1f),
                markerMaterial,
                emitterObject.transform);
            return emitter;
        }

        private void CreateGrid(float width, float height, Material gridMaterial)
        {
            float left = -width * 0.5f;
            float bottom = -height * 0.5f;

            for (int x = 0; x <= gridCellCount.x; x++)
            {
                float xPosition = left + x * cellSize;
                CreateLine(
                    $"Grid Vertical {x}",
                    new Vector3(xPosition, bottom, 0.1f),
                    new Vector3(xPosition, bottom + height, 0.1f),
                    gridMaterial);
            }

            for (int y = 0; y <= gridCellCount.y; y++)
            {
                float yPosition = bottom + y * cellSize;
                CreateLine(
                    $"Grid Horizontal {y}",
                    new Vector3(left, yPosition, 0.1f),
                    new Vector3(left + width, yPosition, 0.1f),
                    gridMaterial);
            }
        }

        private void CreateProbes(Material probeMaterial)
        {
            int halfWidth = gridCellCount.x / 2;
            int halfHeight = gridCellCount.y / 2;
            for (int y = -halfHeight + 1; y < halfHeight; y += 2)
            {
                for (int x = -halfWidth + 1; x < halfWidth; x += 2)
                {
                    Vector3 position = new Vector3(x * cellSize, y * cellSize, -0.04f);
                    GameObject probe = CreateQuad(
                        $"Illumination Probe {x} {y}",
                        position,
                        new Vector3(0.28f, 0.28f, 1f),
                        probeMaterial,
                        generatedRoot);
                    probe.AddComponent<IlluminationProbeVisual>();
                }
            }
        }

        private void CreateLine(string objectName, Vector3 start, Vector3 end, Material material)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(generatedRoot, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.numCapVertices = 0;
        }

        private GameObject CreateQuad(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = objectName;
            quad.transform.SetParent(parent, true);
            quad.transform.position = position;
            quad.transform.localScale = scale;
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;

            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            return quad;
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.DontSave
            };
            runtimeMaterials.Add(material);
            return material;
        }
    }
}
