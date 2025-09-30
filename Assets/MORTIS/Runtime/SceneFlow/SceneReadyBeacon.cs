using Unity.Netcode;
using UnityEngine;
using System.Collections;

namespace MORTIS.SceneFlow
{
    public class SceneReadyBeacon : NetworkBehaviour
    {
        [SerializeField] float extraDelay = 0.25f; // small buffer; tune as needed

        public override void OnNetworkSpawn()
        {
            if (IsClient) StartCoroutine(NotifyWhenReady());
        }

        IEnumerator NotifyWhenReady()
        {
            // Wait a frame so all Awake/Start have run; buffer for slower loads
            yield return null;
            if (extraDelay > 0f) yield return new WaitForSeconds(extraDelay);
            if (IsClient) ClientSceneReadyServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        void ClientSceneReadyServerRpc(ServerRpcParams p = default)
        {
            var sts = FindFirstObjectByType<SceneTransitionService>();
            if (sts) sts.ServerMarkClientReady(p.Receive.SenderClientId);
        }
    }
}
