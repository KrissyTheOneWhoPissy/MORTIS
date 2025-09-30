using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Use A / B / C / D")]
    public string spawnId = "A";

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.35f, $"Spawn_{spawnId}");
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.7f);
    }
#endif
}
