using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;   // new namespace

namespace MORTIS.Players
{
    public class PlayerSetup : NetworkBehaviour
    {
        [SerializeField] Camera playerCamera;
        [SerializeField] AudioListener audioListener;

        [SerializeField] Transform cameraRoot;          // Camera Root
        [SerializeField] CinemachineCamera vcam;        // CM_vcam

        void Start()
        {
            bool isLocal = IsOwner;

            if (playerCamera)  playerCamera.enabled = isLocal;
            if (audioListener) audioListener.enabled = isLocal;
            if (vcam)          vcam.enabled = isLocal;

            // Tracking Target can be set in the prefab inspector,
            // but just in case you want to enforce it in code:
            if (isLocal && vcam != null && cameraRoot != null)
            {
                var t = vcam.Target;
                t.TrackingTarget = cameraRoot;
                vcam.Target = t;
            }
        }
    }
}
