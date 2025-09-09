using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_Setting : UI_Popup
    {
        [SerializeField] private Button closeButton;

        [Header("User Info")] 
        [SerializeField]
        private TextMeshProUGUI linkAccount;
        [SerializeField]
        private TextMeshProUGUI uid;
        
        
        protected override void Init()
        {
            base.Init();
            Subscribe();
        }

        protected override void Clear()
        {
            base.Clear();
            Unsubscribe();
        }

        private void OnEnable()
        {
            //계정 정보 반영하기
            // 저장된 볼륨 값 가져오기
        }


        private void Subscribe()
        {
            closeButton.onClick.AddListener(RequestClose);
        }

        private void Unsubscribe()
        {
            closeButton.onClick.RemoveAllListeners();
        }
    }
}