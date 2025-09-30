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

        // Track which clients have reported ready for the just-loaded scene
        System.Collections.Generic.HashSet<ulong> _ready = new();
        string _awaitingScene = null;
        bool _revivePending = false;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            StartCoroutine(Boot());
        }

        [ClientRpc]
        void FreezePlayersClientRpc(bool freeze)
        {
            // Local-only: toggle this client's control components
            var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (local)
            local.GetComponent<MORTIS.Players.PlayerControlGate>()?.SetFrozen(freeze);
        }

        // Called by SceneReadyBeacon via ServerRpc
        public void ServerMarkClientReady(ulong clientId)
        {
            if (!IsServer || string.IsNullOrEmpty(_awaitingScene)) return;
            _ready.Add(clientId);
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
            FreezePlayersClientRpc(true);                  // 1) freeze local controls on everyone

            _ready.Clear();
            _awaitingScene = sceneName;                    // 2) start barrier
            _revivePending = reviveOnSafeRoom;

            // 3) load scene additively on all clients
            NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            yield return WaitUntilLoaded(sceneName);
            yield return null;                             // let scene Awake/Start (and beacons) run

            // 4) wait until every connected client has reported ready
            int expected = NetworkManager.ConnectedClients.Count;
            float timeout = 30f;
            float t = 0f;
            while (_ready.Count < expected && t < timeout)
                {
                yield return null;
                t += Time.deltaTime;
                }

            // 5) unload previous content scene
            if (!string.IsNullOrEmpty(currentScene))
                {
                var prev = SceneManager.GetSceneByName(currentScene);
                if (prev.IsValid() && prev.isLoaded)
                    NetworkManager.SceneManager.UnloadScene(prev);
                }

            currentScene = sceneName;

            // 6) place players on spawns (with your ground snap) + optional revive
            ServerMovePlayersToSpawnsIfPresent(_revivePending);

            // 7) unfreeze controls + hide loading
            FreezePlayersClientRpc(false);
            ShowLoadingClientRpc(false);

            // 8) clear barrier state
            _awaitingScene = null;
            _revivePending = false;
        }


        IEnumerator WaitUntilLoaded(string sceneName)
            => new WaitUntil(() => SceneManager.GetSceneByName(sceneName).isLoaded);

        [ClientRpc]
        void ShowLoadingClientRpc(bool show)
        {
            var panel = GameObject.Find("LoadingPanel");
            if (panel) panel.SetActive(show);
        }
        
        void ServerMovePlayersToSpawnsIfPresent(bool reviveAll)
    {
        if (!IsServer) return;

        var a = GameObject.FindWithTag("Spawn_A")?.transform;
        var b = GameObject.FindWithTag("Spawn_B")?.transform;
        var c = GameObject.FindWithTag("Spawn_C")?.transform;
        var d = GameObject.FindWithTag("Spawn_D")?.transform;
        var table = new Transform[] { a, b, c, d };
        bool anySpawns = (a || b || c || d);

        int i = 0;
        foreach (var kv in NetworkManager.ConnectedClients)
            {
        var po = kv.Value.PlayerObject;
        if (!po) continue;

        // revive in safe rooms
        var life = po.GetComponent<MORTIS.Players.PlayerLifeState>();
        if (reviveAll && life) life.State.Value = MORTIS.Players.LifeState.Alive;

        var cc = po.GetComponent<CharacterController>();
        float half = cc ? cc.height * 0.5f : 0.9f;
        if (cc) cc.enabled = false;

        // choose spawn or fallback
        Transform t = anySpawns ? table[i % table.Length] : null;
        Vector3 pos = t ? t.position : new Vector3(0f, 1f, 0f);
        Quaternion rot = t ? t.rotation : Quaternion.identity;

        // snap to ground with a raycast from above
        Vector3 origin = pos + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point + Vector3.up * (half + 0.02f);
        else
            pos += Vector3.up * (half + 0.02f);

        po.transform.SetPositionAndRotation(pos, rot);

        if (cc) cc.enabled = true;
        i++;
            }
        }
    }
}
