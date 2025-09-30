using Unity.Netcode;
using UnityEngine;
using MORTIS.Data;

namespace MORTIS.SceneFlow
{
    // Put this on GameSystems (which has a NetworkObject)
    public class SceneTransitionService : NetworkBehaviour
    {
        [Header("Optional for later wiring")]
        [SerializeField] private SceneDirectory directory;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Debug.Log("[SceneTransitionService] Server up.");
                // Later: load Shared_UI + MainMenu additively
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void HostGameServerRpc()
        {
            if (!IsServer) return;
            Debug.Log("[SceneTransitionService] HostGame requested.");
            // Later: load TicketBooth
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartRunServerRpc()
        {
            if (!IsServer) return;
            Debug.Log("[SceneTransitionService] StartRun requested.");
            // Later: jump to first level
        }

        // Plain server-only method (no attribute; guard instead)
        public void ServerGoToNextNode()
        {
            if (!IsServer) return;
            Debug.Log("[SceneTransitionService] ServerGoToNextNode()");
            // Later: additive load next scene, unload current
        }

        // Example of a server-only helper you'll call after loading a Safe Room
        public void ServerRespawnAllAtSceneSpawns()
        {
            if (!IsServer) return;
            Debug.Log("[SceneTransitionService] Respawn (stub)");
            // Later: find Spawn_A..D and move/Revive players
        }
    }
}
