using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_Util;
using LDH_Utils;
using TMPro;
using Unity.VisualScripting;
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
        
        [Header("Transform Setting")] [SerializeField]
        private Vector2 targetRectOffset;
        
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
            Util_LDH.SetCenterTop(targetRect, targetRect.sizeDelta, targetRectOffset);
        }
        

        public void SetStatus(bool isComplete)
        {
            statusText.text = isComplete ? "매칭 완료!" : "매칭 중...";
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



        #region Animation

        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            if (!cg) return;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
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
                t += Time.deltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / fadeTime);
                await UniTask.Yield(ct);
            }
        }

        #endregion
    }
}