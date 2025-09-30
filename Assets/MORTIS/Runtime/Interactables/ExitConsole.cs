using Unity.Netcode;
using UnityEngine;
using MORTIS.SceneFlow;
using MORTIS.Players;

[RequireComponent(typeof(Collider))]
public class ExitConsole : NetworkBehaviour
{
    [SerializeField] float useDistance = 2.0f;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        var no = other.GetComponentInParent<NetworkObject>();
        if (no && no.IsPlayerObject && PlayerLifeState.IsAlive(no.OwnerClientId)
            && Vector3.Distance(no.transform.position, transform.position) <= useDistance)
        {
            // simple “press E” check (works with both input systems)
            bool pressed = Input.GetKeyDown(KeyCode.E);
            #if ENABLE_INPUT_SYSTEM
            pressed |= UnityEngine.InputSystem.Keyboard.current?.eKey.wasPressedThisFrame == true;
            #endif
            if (pressed)
                FindFirstObjectByType<SceneTransitionService>()?.ServerGoToNextNode();
        }
    }
}
