using System;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_PrivateMatchOptions : MonoBehaviour
    {
        [Header("Component UI")] 
        
        
        [SerializeField] private Toggle privateMatchToggle;
        [SerializeField] private GameObject optionObj;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private TMP_InputField roomCodeInputField;


        public Toggle PrivateMatchToggle =>  privateMatchToggle;
        public Button CreateRoomButton => createRoomButton;
        public TMP_InputField RoomCodeInputField => roomCodeInputField;

        private void Awake() => Init();
        private void OnDestroy() => Unsubscribe();

        private void Init()
        {
            //이벤트 구독 처리
            Subscribe();

            //초기 설정
            privateMatchToggle.isOn = false;
            optionObj?.SetActive(false);
        }


        #region Event Subscribe / Unsubscribe

        private void Subscribe()
        {
            privateMatchToggle?.onValueChanged.AddListener(ActivePrivateMatchOption);
        }

        private void Unsubscribe()
        {
            privateMatchToggle?.onValueChanged.RemoveListener(ActivePrivateMatchOption);
        }

        #endregion


        private void ActivePrivateMatchOption(bool isOn)
        {
            if (!isOn) ClearUI();
            optionObj.SetActive(isOn);
        }

        private void ClearUI()
        {
            roomCodeInputField.text = "";
        }
    }
}