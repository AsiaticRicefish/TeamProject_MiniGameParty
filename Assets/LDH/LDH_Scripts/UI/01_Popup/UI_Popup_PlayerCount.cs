using Cysharp.Threading.Tasks;
using LDH_Util;
using Managers;
using Network;
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
        [SerializeField] private Button okButton;
        [SerializeField] private Button closeButton;
        
        
        private const int MinPlayCount = 2;
        private const int MaxPlayerCount = 4;
        private int _value = 0;
        protected override void Init()
        {
            base.Init();

            _value = Define_LDH.MaxPlayers;
            currentCountText.text = Define_LDH.MaxPlayers.ToString();
            
            increaseButton.onClick.AddListener(()=> AdjustCount(true));
            decreaseButton.onClick.AddListener(() => AdjustCount(false));
            okButton.onClick.AddListener(SetCountAndStartMatch);
            closeButton.onClick.AddListener(OnCancelMatch);
        }
        


        private void AdjustCount(bool up)
        {
            _value = Mathf.Clamp(_value + (up ? 1 : -1), MinPlayCount, MaxPlayerCount);
            currentCountText.text = _value.ToString();
        }

        private void AdjustCount(int count)
        {
            _value = Mathf.Clamp(count, MinPlayCount, MaxPlayerCount);
            currentCountText.text = _value.ToString();
        }

        private void SetCountAndStartMatch()
        {
            Define_LDH.MaxPlayers = _value;
            Manager.UI.ClosePopupUI(this).Forget();
#if TEST_PLAYER_COUNT
            MatchController.Instance.StartMatching();
#endif
        }

        private void OnCancelMatch()
        {
#if TEST_PLAYER_COUNT
            MatchController.Instance.CancelMatching();
#endif
            Manager.UI.ClosePopupUI(this).Forget();
        }
    }
}