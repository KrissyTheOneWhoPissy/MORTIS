using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using MORTIS.Players;
using MORTIS.SceneFlow;

[RequireComponent(typeof(Collider))]
public class ExitZone : NetworkBehaviour
{
    [SerializeField] bool majorityRequired = true;
    private readonly HashSet<ulong> inside = new();

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;

        // Only count player root objects that are ALIVE
        if (no.IsPlayerObject && PlayerLifeState.IsAlive(no.OwnerClientId))
        {
            inside.Add(no.OwnerClientId);
            TryAdvance();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null) return;

        if (no.IsPlayerObject)
            inside.Remove(no.OwnerClientId);
    }

    void TryAdvance()
    {
        int alive = 0;
        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
            if (PlayerLifeState.IsAlive(kv.Key)) alive++;

        if (alive == 0) return;

        bool ok = majorityRequired ? (inside.Count * 2 > alive) : (inside.Count >= 1);
        if (ok)
            FindFirstObjectByType<SceneTransitionService>()?.ServerGoToNextNode();
    }
}
