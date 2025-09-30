using Unity.Netcode;
using UnityEngine;

namespace MORTIS.Players
{
    public enum LifeState : byte { Alive, Downed, Dead }

    public class PlayerLifeState : NetworkBehaviour
    {
        public NetworkVariable<LifeState> State =
            new NetworkVariable<LifeState>(LifeState.Alive,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public static bool IsAlive(ulong clientId)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var ci))
                return false;
            var po = ci.PlayerObject;
            return po && po.GetComponent<PlayerLifeState>().State.Value == LifeState.Alive;
        }

        [ContextMenu("Kill (Server)")]   public void DebugKill()   { if (IsServer) State.Value = LifeState.Dead; }
        [ContextMenu("Revive (Server)")] public void DebugRevive() { if (IsServer) State.Value = LifeState.Alive; }
    }
}
