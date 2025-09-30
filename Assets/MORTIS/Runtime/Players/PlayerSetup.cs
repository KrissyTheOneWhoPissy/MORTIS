using Unity.Netcode;
using UnityEngine;

namespace MORTIS.Players
{
    public class PlayerSetup : NetworkBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;

        public override void OnNetworkSpawn()
        {
            // Make sure only the local owner has active camera/audio.
            bool mine = IsOwner;
            if (playerCamera)   playerCamera.enabled   = mine;
            if (audioListener)  audioListener.enabled  = mine;
        }
    }
}
