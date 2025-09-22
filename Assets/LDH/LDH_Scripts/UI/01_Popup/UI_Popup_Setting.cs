using System;
using Data;
using Managers;
using Photon.Pun;
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
        [SerializeField]
        private TextMeshProUGUI nickname;
        
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
          
            uid.text = Manager.Data.UID.Trim();
            nickname.text = PhotonNetwork.LocalPlayer.NickName.Trim();

            // todo: 저장된 볼륨 값 가져오기
            
        }


        private void Subscribe()
        {
            closeButton.onClick.AddListener(RequestClose);
            //todo: 볼륨 슬라이더
        }

        private void Unsubscribe()
        {
            closeButton.onClick.RemoveAllListeners();
            //todo: 볼륨 슬라이더
        }
    }
}