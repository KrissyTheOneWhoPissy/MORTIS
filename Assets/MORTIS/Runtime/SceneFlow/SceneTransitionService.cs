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

            // Collect SpawnPoints in the current content scene
            var points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            var byId = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var p in points)
                if (!string.IsNullOrEmpty(p.spawnId) && !byId.ContainsKey(p.spawnId))
                    byId[p.spawnId] = p.transform;

            // Deterministic seating order: host->A, then B, C, D
            string[] seatOrder = { "A", "B", "C", "D" };
            var clientIds = new System.Collections.Generic.List<ulong>(NetworkManager.ConnectedClients.Keys);
            clientIds.Sort();

            for (int i = 0; i < clientIds.Count; i++)
            {
                var clientId = clientIds[i];
                if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var ci) || !ci.PlayerObject)
                    continue;

                var po = ci.PlayerObject;

                // optional revive
                var life = po.GetComponent<MORTIS.Players.PlayerLifeState>();
                if (reviveAll && life) life.State.Value = MORTIS.Players.LifeState.Alive;

                string desired = seatOrder[i % seatOrder.Length];

                if (!byId.TryGetValue(desired, out var t) || !t)
                {
                    Debug.LogError($"[Spawn] Missing SpawnPoint '{desired}' in scene '{currentScene}'. " +
                               "Place a SpawnPoint with spawnId A/B/C/D.");
                    continue; // do not move this player
                }

                // Move EXACTLY to the transform (no raycast / no fallback)
                Vector3 targetPos = t.position;
                Quaternion targetRot = t.rotation;

                // Ask the owner to move themselves (works with owner-auth transforms)
                var mover = po.GetComponent<MORTIS.Players.PlayerSpawnMover>();
                if (mover)
                {
                    var rpcParams = new ClientRpcParams {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                    };
                    mover.TeleportOwnerClientRpc(targetPos, targetRot, rpcParams);
                }
                else
                {
                    var cc = po.GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    po.transform.SetPositionAndRotation(targetPos, targetRot);
                    if (cc) cc.enabled = true;
                }
            }

            // For visibility in Console
            var found = string.Join(", ", System.Array.ConvertAll(points, p => $"{p.spawnId}:{p.name}"));
            Debug.Log($"[Spawn] Used explicit spawns → {found}");
        }
    }   
}