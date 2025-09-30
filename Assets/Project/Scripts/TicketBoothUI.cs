// Temp - simple ticket booth UI for testing purposes only for now.

using UnityEngine;
using UnityEngine.UI;
using MORTIS.SceneFlow;

public class TicketBoothUI : MonoBehaviour
{
    [SerializeField] private Button startRunBtn;

    void Awake()
    {
        startRunBtn.onClick.AddListener(() =>
        {
            FindFirstObjectByType<SceneTransitionService>()?.StartRunServerRpc();
        });
    }
}
