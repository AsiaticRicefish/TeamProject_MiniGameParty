using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_Util;
using LDH_Utils;
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
        
        [Header("Animation Setting")]
        [SerializeField] private float fadeTime = 0.3f;
        
        
        private float startTime;
        
        protected override void Init()
        {
            base.Init();
            
            //버튼 구독 처리
            cancelButton.onClick.AddListener(()=> RequestClose());

            startTime = Time.unscaledTime;
            
            SetStatus(false);
            SetPlayerCount(0,0);
            SetElapsed();
            
            // 위치
            transform.SetParent(Manager.UI.UIRoot.CenterArea.transform);
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
            float value = Time.unscaledTime - startTime;
            elpasedText.text = Util_LDH.FormatTimeMS(value);
        }

        public void SetCancelable(bool cancelable)
        {
            cancelButton.interactable = cancelable;
        }
        

        #endregion

     
        
        /// <summary>
        /// Self Auto Close
        /// </summary>
        /// <param name="seconds"></param>
        /// <param name="ct"></param>
        public async UniTask AutoCloseAfter(float seconds, CancellationToken ct)
        {
            // 매칭 완료를 잠깐 보여주기 위한 지연
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                return; // 팝업이 파괴되면 자연스레 취소됨
            }

            // UI 매니저의 표준 닫기 흐름을 타고 싶으면 RequestClose()가 안전
            Manager.UI.ClosePopupUI(this).Forget();
        }

        
        #region Animation

        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            if (!cg) return;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(t / fadeTime);
                await UniTask.Yield(ct);
            }
        }


        protected override async UniTask OnCloseAsync(CancellationToken ct)
        {
            if (!cg) return;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / fadeTime);
                await UniTask.Yield(ct);
            }
        }

        #endregion
    }
}