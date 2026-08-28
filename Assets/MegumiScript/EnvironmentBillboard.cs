using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps environment renderers facing the active camera for a 2.5D look.
/// Attach this component to an environment root instead of rotating the root itself.
/// </summary>
[DisallowMultipleComponent]
public class EnvironmentBillboard : MonoBehaviour
{
    public enum BillboardMode
    {
        Full,
        YAxisOnly
    }

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Targets")]
    [SerializeField] private bool autoCollectSpriteRenderers = true;
    [SerializeField] private bool includeInactive = false;
    [SerializeField] private bool includeMeshRenderers = false;
    [SerializeField] private Renderer[] additionalTargets;

    [Header("Billboard")]
    [SerializeField] private BillboardMode mode = BillboardMode.Full;
    [SerializeField] private bool reverseFacing = false;

    private Transform[] targetTransforms = System.Array.Empty<Transform>();

    private void Awake()
    {
        RebuildTargets();
    }

    private void OnEnable()
    {
        if (targetTransforms == null || targetTransforms.Length == 0)
        {
            RebuildTargets();
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        foreach (Transform target in targetTransforms)
        {
            if (target == null || target == transform)
            {
                continue;
            }

            ApplyBillboard(target);
        }
    }

    /// <summary>
    /// Rebuilds the renderer list. Call this after adding environment objects at runtime.
    /// </summary>
    public void RebuildTargets()
    {
        var renderers = new List<Renderer>();

        if (autoCollectSpriteRenderers)
        {
            renderers.AddRange(GetComponentsInChildren<SpriteRenderer>(includeInactive));
        }

        if (includeMeshRenderers)
        {
            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(includeInactive));
        }

        if (additionalTargets != null)
        {
            foreach (Renderer renderer in additionalTargets)
            {
                if (renderer != null)
                {
                    renderers.Add(renderer);
                }
            }
        }

        var uniqueTransforms = new List<Transform>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.transform == transform || uniqueTransforms.Contains(renderer.transform))
            {
                continue;
            }

            uniqueTransforms.Add(renderer.transform);
        }

        targetTransforms = uniqueTransforms.ToArray();
    }

    private void ApplyBillboard(Transform target)
    {
        if (mode == BillboardMode.YAxisOnly)
        {
            Vector3 toCamera = targetCamera.transform.position - target.position;
            toCamera.y = 0f;

            if (toCamera.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (reverseFacing)
            {
                toCamera = -toCamera;
            }

            target.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            return;
        }

        Vector3 facing = reverseFacing
            ? targetCamera.transform.forward
            : -targetCamera.transform.forward;

        target.rotation = Quaternion.LookRotation(facing, targetCamera.transform.up);
    }
}
