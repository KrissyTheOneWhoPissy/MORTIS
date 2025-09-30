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
            var mine = IsOwner;
            if (playerCamera) playerCamera.enabled = mine;
            if (audioListener) audioListener.enabled = mine;

            if (mine)
            {
                var boot = GameObject.Find("BootstrapCamera");
                if (boot) boot.SetActive(false);
            }
        }
    }
}