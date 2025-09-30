using UnityEngine;

namespace MORTIS.Runtime
{
    // MonoBehaviour (no networking needed for now).
    public class AchievementService : MonoBehaviour
    {
        public void Unlock(string id)
        {
            Debug.Log($"[AchievementService] Unlock '{id}' (stub)");
            // Later: call Steam/Platform APIs here
        }
    }
}
