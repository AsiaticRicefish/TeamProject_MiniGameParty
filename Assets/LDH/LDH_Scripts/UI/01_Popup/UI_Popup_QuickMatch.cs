using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_Util;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_QuickMatch : UI_Popup
    {
        
        [Header("UI Component")] 
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI playerCntText;
        [SerializeField] private TextMeshProUGUI elpasedText;
        [SerializeField] private Button cancelButton;
        
        [Header("UI Setting")] 
        [SerializeField] private Vector2 targetRectOffset;
        [SerializeField] private string matchingInProgressMessage = "Finding...";
        [SerializeField] private string matchingCompleteMessage = "Complete!";
        
        
        private float _startTime;
        
        protected override void Init()
        {
            base.Init();
            
            //버튼 구독 처리
            cancelButton.onClick.AddListener(()=> RequestClose());

            _startTime = Time.unscaledTime;
            
            SetStatus(false);
            SetPlayerCount(0,0);
            SetElapsed();
            
            // 위치
            Util_LDH.SetCenterTop(targetRect, targetRect.sizeDelta, targetRectOffset);
            
        }

        #region UI Data Update
        public void SetStatus(bool isComplete)
        {
            statusText.text = isComplete ? matchingCompleteMessage : matchingInProgressMessage;
        }

        public void SetPlayerCount(int currentPlayerCount, int maxPlayerCount)
        {
            playerCntText.text = $"{currentPlayerCount}/{maxPlayerCount}";
        }

        public void SetElapsed()
        {
            float value = Time.unscaledTime - _startTime;
            elpasedText.text = Util_LDH.FormatTimeMS(value);
        }

        public void SetCancelable(bool cancelable)
        {
            cancelButton.interactable = cancelable;
        }
        

        #endregion
        
        
    }
}