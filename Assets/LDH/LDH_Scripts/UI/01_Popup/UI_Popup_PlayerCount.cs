using LDH_Util;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_PlayerCount : UI_Popup
    {
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private TMP_Text currentCountText;
        
        private int _minPlayCount = 2;
        private int _maxPlayerCount = 2;

        protected override void Init()
        {
            base.Init();

            currentCountText.text = Define_LDH.MaxPlayers.ToString();
            
            
            increaseButton.onClick.AddListener(()=> AdjustCount(true));
            decreaseButton.onClick.AddListener(() => AdjustCount(false));
        }


        private void AdjustCount(bool up)
        {
            int value = Mathf.Clamp(Define_LDH.MaxPlayers + (up ? 1 : -1), _minPlayCount, _maxPlayerCount + 1);
            Define_LDH.MaxPlayers = value;
            currentCountText.text = value.ToString();
        }
    }
}