using UnityEngine;

namespace MORTIS.Players
{
    public class PlayerControlGate : MonoBehaviour
    {
        [SerializeField] Behaviour[] toToggle; // e.g., PlayerMover, PlayerLook

        public void SetFrozen(bool freeze)
        {
            if (toToggle == null) return;
            foreach (var b in toToggle) if (b) b.enabled = !freeze;
        }
    }
}
