using Unity.Netcode;
using UnityEngine;

namespace MORTIS.SceneFlow
{
    public enum RunPhase : byte { Boot, MainMenu, TicketBooth, Playing, SafeRoom, End }

    // Put this on GameSystems (which has a NetworkObject)
    public class RunStateService : NetworkBehaviour
    {
        public NetworkVariable<RunPhase> Phase =
            new NetworkVariable<RunPhase>(RunPhase.Boot, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                Phase.Value = RunPhase.Boot;
        }

        public void ServerSetPhase(RunPhase p)
        {
            if (!IsServer) return;
            Phase.Value = p;
            Debug.Log($"[RunStateService] Phase -> {p}");
        }
    }
}
