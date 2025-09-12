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
        [SerializeField] private TextMeshProUGUI elapsedText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Slider playerCntSlider;
        
        
        [Header("UI Setting")] 
        [SerializeField] private bool positionControlOnAwake;
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
            InitSlider();
            SetPlayerCount(0,0);
            SetElapsed();
            
            // 위치
            if(positionControlOnAwake)
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
            playerCntSlider.value = currentPlayerCount;
            playerCntSlider.maxValue = maxPlayerCount;
      
        }

        public void SetElapsed()
        {
            float value = Time.unscaledTime - _startTime;
            elapsedText.text = Util_LDH.FormatTimeMS(value);
        }

        public void SetCancelable(bool cancelable)
        {
            cancelButton.interactable = cancelable;
        }
        
        private void InitSlider()
        {
            playerCntSlider.minValue = 0;
        }

        #endregion
        
        
    }
}