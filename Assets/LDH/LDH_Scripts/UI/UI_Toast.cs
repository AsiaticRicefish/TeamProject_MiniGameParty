using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Toast : UI_Base
    {

        [SerializeField] protected float fadeTime = 0.5f;
        [SerializeField] private RectTransform targetRect;
        [SerializeField] TextMeshProUGUI label;
        
        public RectTransform TargetRect => targetRect;
        
        protected override void Init()
        {
            interactable = false;
            blocksRaycasts = false;
            
            base.Init();
        }
        
        public void SetMessage(string msg) { if (label) label.text = msg; }

        
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
    }
}