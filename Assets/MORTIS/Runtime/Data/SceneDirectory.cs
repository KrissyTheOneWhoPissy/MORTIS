using UnityEngine;
using System.Collections.Generic;

namespace MORTIS.Data
{
    [CreateAssetMenu(menuName = "MORTIS/Scene Directory")]
    public class SceneDirectory : ScriptableObject
    {
        [System.Serializable]
        public class Node
        {
            public string id;        // e.g., "Ring 01"
            public string sceneName; // e.g., "Level_Ring_01"
            public bool isSafeRoom;
        }

        [Header("Scene names (exact in Build Settings)")]
        public string mainMenu = "MainMenu";
        public string ticketBooth = "TicketBooth_Lobby";
        public string sharedUI = "Shared_UI"; // optional; leave empty if unused

        [Header("Run order")]
        public List<Node> runOrder = new();
    }
}
