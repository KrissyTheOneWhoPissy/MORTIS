using Unity.Netcode;
using UnityEngine;

namespace MORTIS.Players
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerSpawnMover : NetworkBehaviour
    {
        [ClientRpc]
        public void TeleportOwnerClientRpc(Vector3 pos, Quaternion rot, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return; // only the owner moves themselves

            var cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);

            // Optional: if you have a NetworkTransform, nudges it to the new pose immediately
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt) nt.Teleport(pos, rot, transform.localScale);

            if (cc) cc.enabled = true;
        }
    }
}
