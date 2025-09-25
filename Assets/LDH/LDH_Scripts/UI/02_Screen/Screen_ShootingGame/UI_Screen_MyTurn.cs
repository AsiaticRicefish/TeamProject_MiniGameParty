using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LDH_Util;
using Managers;
using PMS_Util;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Screen_MyTurn : UI_Screen
    {
        [Header("Targets")]
        [SerializeField] RectTransform myTurnBgRect; // 배경
        [SerializeField] RectTransform myTurnTextRect; // text
        [SerializeField] private CanvasGroup myTurnTextCg;

        [SerializeField] private int xOffset;

        [Header("Timing")] 
        [SerializeField] float bgDuration = 0.3f; // 페이드 인
        [SerializeField] private float intervalDelay = 0.2f;
        [SerializeField] float moveTime = 0.5f; // 들어오는 시간
        [SerializeField] private float fadeTime = 0.25f;
        [SerializeField] float hold = 0.6f; // 중앙에서 머무는 시간
        public float HoldTime => hold;

        
        private Sequence seq;
        private float targetWidth;
        private Vector2 bgTargetOffsetMin;
        private Vector2 bgTargetOffsetMax;
        private Vector2 bgStartOffsetMin;
        private Vector2 bgStartOffsetMax;
        private Vector2 textTargetAnchorPos;
        private Vector2 textStartAnchorPos;
        private Vector2 textEndAnchorPos;

        private void Start()
        {
            targetWidth = myTurnBgRect.rect.size.x;
            textTargetAnchorPos = myTurnTextRect.anchoredPosition;
            textStartAnchorPos = textTargetAnchorPos - new Vector2(xOffset, 0);
            textEndAnchorPos = textTargetAnchorPos + new Vector2(xOffset, 0);


            bgTargetOffsetMin = myTurnBgRect.offsetMin;
            bgTargetOffsetMax = myTurnBgRect.offsetMax;

            bgStartOffsetMin = new Vector2(targetWidth * 0.5f, myTurnBgRect.offsetMin.y);
            bgStartOffsetMax = new Vector2(-targetWidth * 0.5f, myTurnBgRect.offsetMax.y);
        }

        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            // 배경 초반 상태
            myTurnBgRect.offsetMin = bgStartOffsetMin;
            myTurnBgRect.offsetMax = bgStartOffsetMax;

            // 텍스트 초반 위치
            myTurnTextRect.anchoredPosition = textStartAnchorPos;
            myTurnTextCg.alpha = 0f;

            cg.alpha = 1f;

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            // 1) 배경 펼치기
            seq.Join(DOTween.To(
                getter: () => myTurnBgRect.offsetMin,
                setter: v => myTurnBgRect.offsetMin = v,
                endValue: bgTargetOffsetMin,
                duration: bgDuration
            )).SetEase(Ease.OutCubic);
            seq.Join(DOTween.To(
                getter: () => myTurnBgRect.offsetMax,
                setter: v => myTurnBgRect.offsetMax = v,
                endValue: bgTargetOffsetMax,
                duration: bgDuration
            )).SetEase(Ease.OutCubic);

            // 2) 대기
            seq.AppendInterval(intervalDelay);

            // 3) 텍스트 이동 및 페이드
            seq.Join(myTurnTextRect.DOAnchorPos(textTargetAnchorPos, moveTime)).SetEase(Ease.OutQuad)
                .Join(myTurnTextCg.DOFade(1f, fadeTime));

            await seq.AsyncWaitForCompletion();
        }

        protected override async UniTask OnCloseAsync(CancellationToken ct)
        {
            seq?.Kill();
            seq = DOTween.Sequence();
            seq.SetUpdate(true);

            //거꾸로
            // 3) 텍스트 이동 및 페이드
            seq.Join(myTurnTextRect.DOAnchorPos(textEndAnchorPos, moveTime)).SetEase(Ease.InCubic)
                .Join(myTurnTextCg.DOFade(0f, fadeTime));

            // 2) 대기
            seq.AppendInterval(intervalDelay);

            
            // 1) 배경 줄이기
            seq.Join(DOTween.To(
                getter: () => myTurnBgRect.offsetMin,
                setter: v => myTurnBgRect.offsetMin = v,
                endValue: bgStartOffsetMin,
                duration: bgDuration
            )).SetEase(Ease.InCubic);
            seq.Join(DOTween.To(
                getter: () => myTurnBgRect.offsetMax,
                setter: v => myTurnBgRect.offsetMax = v,
                endValue: bgStartOffsetMax,
                duration: bgDuration
            )).SetEase(Ease.InCubic);
            
            
            await seq.AsyncWaitForCompletion();
            cg.alpha = 0f;
        }
    }
}