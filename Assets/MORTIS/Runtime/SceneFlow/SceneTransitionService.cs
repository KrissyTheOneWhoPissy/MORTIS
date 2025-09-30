using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using MORTIS.Data;

namespace MORTIS.SceneFlow
{
    public class SceneTransitionService : NetworkBehaviour
    {
        [SerializeField] private SceneDirectory directory;
        [SerializeField] private string currentScene;   // name of the active content scene
        int cursor = -1;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            StartCoroutine(Boot());
        }

        IEnumerator Boot()
        {
            // Optional shared UI scene
            if (!string.IsNullOrEmpty(directory.sharedUI))
            {
                NetworkManager.SceneManager.LoadScene(directory.sharedUI, LoadSceneMode.Additive);
                yield return WaitUntilLoaded(directory.sharedUI);
            }

            // Start in Main Menu
            yield return ServerLoadContent(directory.mainMenu);
            var rs = FindFirstObjectByType<RunStateService>();
            rs?.ServerSetPhase(RunPhase.MainMenu);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HostGameServerRpc()
        {
            if (!IsServer) return;

            // If an offline MainMenu was loaded by OfflineBootstrap, unload it now.
            var mm = UnityEngine.SceneManagement.SceneManager.GetSceneByName(directory.mainMenu);
            if (mm.IsValid() && mm.isLoaded)
                UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(mm);

            StartCoroutine(ServerLoadContent(directory.ticketBooth));
            var rs = FindFirstObjectByType<RunStateService>();
            rs?.ServerSetPhase(RunPhase.TicketBooth);
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartRunServerRpc()
        {
            if (!IsServer) return;
            cursor = -1;
            ServerGoToNextNode();
        }

        public void ServerGoToNextNode()
        {
            if (!IsServer) return;

            cursor++;
            if (cursor >= directory.runOrder.Count)
            {
                StartCoroutine(ServerLoadContent(directory.ticketBooth));
                var rs = FindFirstObjectByType<RunStateService>();
                rs?.ServerSetPhase(RunPhase.TicketBooth);
                return;
            }

            var node = directory.runOrder[cursor];
            StartCoroutine(ServerLoadContent(node.sceneName, node.isSafeRoom));

            var rs2 = FindFirstObjectByType<RunStateService>();
            rs2?.ServerSetPhase(node.isSafeRoom ? RunPhase.SafeRoom : RunPhase.Playing);
        }

        IEnumerator ServerLoadContent(string sceneName, bool reviveOnSafeRoom = false)
        {
            ShowLoadingClientRpc(true);

            NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            yield return WaitUntilLoaded(sceneName);

            if (!string.IsNullOrEmpty(currentScene))
            {
                var prev = SceneManager.GetSceneByName(currentScene);
                if (prev.IsValid() && prev.isLoaded)
                    NetworkManager.SceneManager.UnloadScene(prev); // pass Scene, not string
            }

            currentScene = sceneName;

            if (reviveOnSafeRoom)
            {
                // placeholder for respawn — you’ll wire this when SafeRoom spawns exist
                // ServerRespawnAllAtSceneSpawns();
            }

            ShowLoadingClientRpc(false);
        }

        IEnumerator WaitUntilLoaded(string sceneName)
            => new WaitUntil(() => SceneManager.GetSceneByName(sceneName).isLoaded);

        [ClientRpc] void ShowLoadingClientRpc(bool show)
        {
            var panel = GameObject.Find("LoadingPanel");
            if (panel) panel.SetActive(show);
        }
    }
}
