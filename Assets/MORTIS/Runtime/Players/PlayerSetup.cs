using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

namespace MORTIS.Players
{
    public class PlayerSetup : NetworkBehaviour
    {
        [Header("Local-only Components")]
        [SerializeField] Camera playerCamera;
        [SerializeField] AudioListener audioListener;
        [SerializeField] CinemachineCamera vcam; // CM_vcam (Cinemachine 3)

        [Header("Cinemachine Target")]
        [Tooltip("Assign CM_Target (or CM_Pitch if you later split). This is what the CinemachineCamera will track.")]
        [SerializeField] Transform cmTrackingTarget;

        public override void OnNetworkSpawn()
        {
            ApplyLocalSetup();
        }

        void ApplyLocalSetup()
        {
            bool isLocal = IsOwner;

            if (playerCamera)  playerCamera.enabled  = isLocal;
            if (audioListener) audioListener.enabled = isLocal;
            if (vcam)          vcam.enabled          = isLocal;

            // Only the local player's vcam should have a tracking target set.
            if (!isLocal || vcam == null) return;

            if (cmTrackingTarget != null)
            {
                var t = vcam.Target;
                t.TrackingTarget = cmTrackingTarget;
                vcam.Target = t;
            }
            else
            {
                Debug.LogWarning($"[{nameof(PlayerSetup)}] Missing cmTrackingTarget on {name}. " +
                                 $"Assign CM_Target (or CM_Pitch) to avoid Cinemachine following the wrong transform.");
            }
        }
    }
}
