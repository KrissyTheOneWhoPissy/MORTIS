// Temp - simple main menu UI for testing purposes only for now.

using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using MORTIS.SceneFlow;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button testHostBtn;   // <- NEW

    void Awake()
    {
        hostBtn.onClick.AddListener(OnHostClicked);
        testHostBtn.onClick.AddListener(OnTestHostClicked);   // <- NEW
    }

    private void OnHostClicked()
    {
        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartHost();

        FindFirstObjectByType<SceneTransitionService>()?.HostGameServerRpc();
    }

    private void OnTestHostClicked()
    {
        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartHost();

        // NEW: tell the SceneTransitionService to go to the testing scene
        FindFirstObjectByType<SceneTransitionService>()?.HostTestSceneServerRpc();
    }
}