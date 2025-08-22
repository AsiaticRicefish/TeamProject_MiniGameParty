using System.Collections.Generic;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_PrivateRoom : UI_Popup
    {
        
        [SerializeField] private GameObject playerPanel;
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private Button exitButton;

        private string _roomCode;
        
        public Dictionary<Player, UI_PlayerPanel> PlayerPanels;
        public Button ExitButton => exitButton;
        
        protected override void Init()
        {
            base.Init();
            PlayerPanels = new();
            exitButton.onClick.AddListener(()=> RequestClose());
        }
        
        public void SetRoomCode(string roomCode)
        {
            roomCodeText.text = roomCode;
            _roomCode = roomCode;
        }
    }
}