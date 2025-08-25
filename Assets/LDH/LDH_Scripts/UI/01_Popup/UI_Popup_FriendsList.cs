using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_FriendsList : UI_Popup
    {
        [SerializeField] private Button closeButton;
        
        protected override void Init()
        {
            base.Init();
            
            //외부는 UI_Base의 OnClosedRequested를 구독해서 처리
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(RequestClose);
        }

    }
}