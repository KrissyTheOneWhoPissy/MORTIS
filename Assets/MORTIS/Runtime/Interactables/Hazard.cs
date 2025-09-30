// Temp - simple hazard script for testing purposes only for now.
using Unity.Netcode;
using UnityEngine;
using MORTIS.Players;

[RequireComponent(typeof(Collider))]
public class Hazard : NetworkBehaviour
{
    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var no = other.GetComponentInParent<NetworkObject>();
        if (no == null || !no.IsPlayerObject) return;

        var life = no.GetComponent<PlayerLifeState>();
        if (life) life.State.Value = LifeState.Dead;
    }
}
