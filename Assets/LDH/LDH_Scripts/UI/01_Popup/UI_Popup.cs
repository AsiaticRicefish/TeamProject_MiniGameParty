using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Managers;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Popup : UI_Base
    {
        [Header("Animation Setting")]
        [SerializeField] protected float fadeTime = 0.1f;
        
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
            
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
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
            
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
            gameObject.SetActive(false);
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

            await Manager.UI.ClosePopupUI(this);
        }
    }
}