using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class OfflineBootstrap : MonoBehaviour
{
    [SerializeField] string mainMenuSceneName = "MainMenu";

    void Start()
    {
        // If networking hasn't started yet, show the menu via regular SceneManager.
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            if (!SceneManager.GetSceneByName(mainMenuSceneName).isLoaded)
                SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Additive);
        }
    }
}
