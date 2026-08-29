using UnityEngine;

namespace Game.Lighting
{
    [AddComponentMenu("Game/Lighting/Camera Facing Sprite")]
    [DisallowMultipleComponent]
    public sealed class CameraFacingSprite : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

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

            transform.rotation = Quaternion.LookRotation(
                -targetCamera.transform.forward,
                targetCamera.transform.up);
        }
    }
}
