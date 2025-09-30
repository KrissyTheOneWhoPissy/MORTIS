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
            public string id;                 // e.g., "Ring 01"
            public string addressableKey;     // e.g., "scenes/level_ring_01"
            public bool isSafeRoom;
        }

        public string mainMenuKey = "scenes/main_menu";
        public string ticketBoothKey = "scenes/ticket_booth_lobby";
        public string sharedUiKey = "scenes/shared_ui";

        public List<Node> runOrder = new();  // interleave levels & saferooms
    }
}
