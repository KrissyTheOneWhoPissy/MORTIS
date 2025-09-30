//Temp - simple main menu UI for testing purposes only for now.

using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using MORTIS.SceneFlow;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button hostBtn;

    void Awake()
    {
        hostBtn.onClick.AddListener(() =>
        {
            if (!NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.StartHost();
            FindFirstObjectByType<SceneTransitionService>()?.HostGameServerRpc();
        });
    }
}
