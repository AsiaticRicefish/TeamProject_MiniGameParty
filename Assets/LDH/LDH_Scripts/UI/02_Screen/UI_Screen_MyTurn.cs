using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Managers;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Screen_MyTurn : UI_Screen
    {
        [Header("Targets")]
        [SerializeField] RectTransform target;     // 애니메이션할 UI
        [SerializeField] RectTransform leftRef;    // 좌 위치 기준
        [SerializeField] RectTransform centerRef;  // 중앙 위치 기준
        [SerializeField] RectTransform rightRef;   // 우 위치 기준
        
        [Header("Timing")]
        [SerializeField] float moveTime = 0.5f;      // 들어오는 시간
        [SerializeField] float hold = 0.6f;        // 중앙에서 머무는 시간
        [SerializeField] float fadeTime = 0.25f;     // 페이드 인
        public float HoldTime => hold;
        
        
        [Header("Ease")]
        [SerializeField] Ease easeType = Ease.OutCubic;

        private Sequence seq;
        

        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            seq?.Kill();
            seq = DOTween.Sequence();
            seq.SetUpdate(true);
            
            seq.Append(target.DOAnchorPos(centerRef.anchoredPosition, moveTime).SetEase(easeType));
            seq.Join(cg.DOFade(1f, fadeTime));
            
            await seq.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(ct);
        }

        protected override async UniTask OnCloseAsync(CancellationToken ct)
        {
            seq?.Kill();
            seq = DOTween.Sequence();
            seq.SetUpdate(true);
            
            seq.Append(target.DOAnchorPos(rightRef.anchoredPosition, moveTime).SetEase(easeType));
            seq.Join(cg.DOFade(0f, fadeTime));
            
            await seq.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(ct);

        }
    }
}