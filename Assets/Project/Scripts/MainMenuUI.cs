using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using MORTIS.SceneFlow;

public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button testHostBtn;
    [SerializeField] private Button joinBtn;

    [Header("Quick connect (local)")]
    [SerializeField] private string hostAddress = "127.0.0.1";
    [SerializeField] private ushort hostPort = 7777;

    void Awake()
    {
        hostBtn.onClick.AddListener(() => RequestTransition(TransitionTarget.Game));
        testHostBtn.onClick.AddListener(() => RequestTransition(TransitionTarget.Test));
        joinBtn.onClick.AddListener(JoinAsClient);
    }

    enum TransitionTarget { Game, Test }

    void RequestTransition(TransitionTarget target)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // If we are offline, start host (quick test convenience)
        if (!nm.IsListening)
            nm.StartHost();

        // If we are a client, DO NOT try to host. Just send the RPC request to the server.
        if (SceneTransitionService.Instance == null)
        {
            Debug.LogWarning("[MainMenuUI] SceneTransitionService.Instance not ready yet.");
            return;
        }

        switch (target)
        {
            case TransitionTarget.Game:
                SceneTransitionService.Instance.HostGameServerRpc();
                break;
            case TransitionTarget.Test:
                SceneTransitionService.Instance.HostTestSceneServerRpc();
                break;
        }
    }

    void JoinAsClient()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (nm.IsListening)
        {
            Debug.Log("[MainMenuUI] Already connected/listening.");
            return;
        }

        // Force UnityTransport to connect to localhost for quick testing
        var utp = nm.GetComponent<UnityTransport>();
        if (utp == null) utp = FindFirstObjectByType<UnityTransport>();
        if (utp == null)
        {
            Debug.LogError("[MainMenuUI] UnityTransport not found. Make sure it’s on the NetworkManager.");
            return;
        }

        utp.ConnectionData.Address = hostAddress;
        utp.ConnectionData.Port = hostPort;

        nm.StartClient();
        Debug.Log($"[MainMenuUI] Starting client → {hostAddress}:{hostPort}");
    }
}
