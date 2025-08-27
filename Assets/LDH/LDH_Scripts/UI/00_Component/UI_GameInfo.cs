using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_GameInfo : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI gameName;
        [SerializeField] private TextMeshProUGUI playerCount;

        public void SetGameName(string gameName)
        {
            this.gameName.text = gameName;
        }

        public void SetPlayerCount(int current)
        {
            playerCount.text = $"Current Players : {current}";
        }

    }
}