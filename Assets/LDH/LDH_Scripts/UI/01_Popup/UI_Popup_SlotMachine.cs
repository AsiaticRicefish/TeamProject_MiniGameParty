using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LDH_MainGame;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_SlotMachine : UI_Popup
    {
        [SerializeField] private RectTransform title;
        [SerializeField] private RectTransform subTitle;

        [SerializeField] private RectTransform slotMachine;
        [SerializeField] private Transform handle;
        [SerializeField] private UI_SlotMachine_Row slotRow;

        [Header("CanvasGroup")] [SerializeField]
        private CanvasGroup tCg;

        [SerializeField] private CanvasGroup sCg;
        [SerializeField] private CanvasGroup smCg;

        [Header("Timings")] [SerializeField] private float titleInTime = 0.35f;
        [SerializeField] private float subTitleDelay = 0.3f;
        [SerializeField] private float subTitleInTime = 0.35f;
        [SerializeField] private float delayBeforeDrop = 0.8f;
        [SerializeField] private float dropTimeDown = 0.28f; // 쿵 떨어지는 구간
        [SerializeField] private float dropTimeUp = 0.18f; // 살짝 되튀기
        [SerializeField] private float dropOvershoot = 28f; // 되튀기 픽셀
        [SerializeField] private float handleDownDeg = 25f; // 레버 회전 각도

        private int _targetIndex;

        public async UniTask SetData(List<MiniGameInfo> candidates, int targetIndex)
        {
            _targetIndex = targetIndex;
            await slotRow.SetCandidates(candidates);
        }

        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            // 타이틀/서브타이틀/슬롯 머신 초기 상태 세팅
            tCg.alpha = 0f;
            sCg.alpha = 0f;
            smCg.alpha = 0f;

            // 초기 상태
            var t0 = title.anchoredPosition;
            var s0 = subTitle.anchoredPosition;
            var sm0 = slotMachine.anchoredPosition;

            title.anchoredPosition = t0 + new Vector2(0, -30f);
            subTitle.anchoredPosition = s0 + new Vector2(0, -30f);
            slotMachine.anchoredPosition = sm0 + new Vector2(0, 180f);

            // 전체 캔버스 alpha 활성화
            cg.alpha = 1f;

            // 1) 타이틀 인
            var twTitle = DOTween.Sequence()
                .Join(title.DOAnchorPosY(t0.y, titleInTime)).SetEase(Ease.OutCubic)
                .Join(tCg.DOFade(1f, titleInTime))
                .SetLink(gameObject);

            await twTitle.AsyncWaitForCompletion();

            // 2) 서브타이틀 인
            var twSubTitle = DOTween.Sequence()
                .AppendInterval(subTitleDelay)
                .Append(
                    DOTween.Sequence().Join(subTitle.DOAnchorPosY(s0.y, subTitleInTime))
                        .Join(sCg.DOFade(1f, subTitleInTime))
                ).SetLink(gameObject);

            await twSubTitle.AsyncWaitForCompletion();


            await UniTask.Delay(TimeSpan.FromSeconds(delayBeforeDrop));

            // 2) 슬롯 머신 등장 연출 : 위에서 쿵 떨어지는 효과
            var tw3 = DOTween.Sequence()
                .Append(slotMachine.DOAnchorPosY(sm0.y + dropOvershoot, dropTimeDown).SetEase(Ease.OutCubic))
                .Append(slotMachine.DOAnchorPosY(sm0.y, dropTimeUp).SetEase(Ease.InCubic)).SetLink(gameObject);
            
            await tw3.AsyncWaitForCompletion();

        }


        // 슬롯 작동 
        public async UniTask PullHandle(CancellationToken ct = default)
        {
            var down = handle
                .DOLocalRotate(new Vector3(0, 0, -handleDownDeg), 0.08f)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);
            await down.AsyncWaitForCompletion();

            slotRow.StartRotating(_targetIndex);

            var up = handle
                .DOLocalRotate(Vector3.zero, 0.12f)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);
            await up.AsyncWaitForCompletion();
            
            await UniTask.WaitUntil(() => slotRow.rowStopped, cancellationToken: ct);
        }
    }
}