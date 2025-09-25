using System;
using System.Collections.Generic;
using System.Threading;
using Customization;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using Data;
using DG.Tweening;
using LDH_Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_ScoreEntry : MonoBehaviour
    {
        [Header("Canvas Group")] [SerializeField]
        private CanvasGroup cg;

        [SerializeField] private CanvasGroup rewardCg;

        [Header("UI Component")] [SerializeField]
        private Image profileImage;

        [SerializeField] private TMP_Text nickNameText;
        [SerializeField] private TMP_Text miniGameRankText;
        [SerializeField] private TMP_Text totalRankText;
        [SerializeField] private Transform scoreImageParent;
        [SerializeField] private GameObject scoreImagePrefab;
        [SerializeField] private Outline outline;

        // reward
        [SerializeField] private Image currencyImage;
        [SerializeField] private TMP_Text rewardText;

        [Header("Anim")] [SerializeField] private float appearDuration = 0.25f;
        [SerializeField] private float pulseDuration = 1f;


        [Header("Sound")] 
        [SerializeField] private Define_LDH.SfxKey coinSfx = Define_LDH.SfxKey.Main_Coin;
        private int _maxTicks = 20;

        private readonly List<GameObject> _scoreIcons = new();
        private int _totalReward;

        private void Awake()
        {
            if (totalRankText) totalRankText.enabled = false;
            if (outline) outline.enabled = false;

            // 초기 상태(투명/살짝 축소) — PlayAppearAsync에서 사용
            cg.alpha = 0f;
            transform.localScale = Vector3.one * 0.9f;
            rewardCg.alpha = 0f;
        }


        public async UniTask SetData(string nickName, int miniRank, int totalRank, string profileId, int totalScore,
            bool winner, Define_LDH.CurrencyType rewardCurrency = Define_LDH.DefaultData.DefaultRewardCurrency,
            int reward = 0)
        {
            // 프로필
            Sprite profile = await CustomizationManager.Instance.GetIconAsync(profileId);
            if (profileImage) profileImage.sprite = profile;

            // 이름
            if (nickNameText) nickNameText.text = nickName;

            // 미니게임 등수
            if (miniGameRankText) miniGameRankText.text = Util_LDH.GetRankFormat(miniRank);

            // 전체 등수
            if (totalRankText)
            {
                totalRankText.text = Util_LDH.GetRankFormat(totalRank);
                if (totalRank == 1)
                    totalRankText.color = new Color(240, 133, 43);
                else
                    totalRankText.color = Color.white;
            }

            // 전체 스코어
            if (scoreImageParent != null && scoreImagePrefab != null)
            {
                BuildScoreIcons(totalScore, winner);
            }

            // 보상
            if (currencyImage && CatalogProvider.TryGetCurrency(rewardCurrency, out var meta))
            {
                currencyImage.sprite = meta.icon;
            }

            if (rewardText) rewardText.text = $"+ {reward:N0}";

            _totalReward = reward;
        }


        private void BuildScoreIcons(int count, bool winner)
        {
            // 기존 비우고 다시
            foreach (var go in _scoreIcons) Destroy(go);
            _scoreIcons.Clear();

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(scoreImagePrefab, scoreImageParent);
                go.transform.SetAsLastSibling();
                _scoreIcons.Add(go);
            }

            if (winner)
                _scoreIcons[^1].gameObject.SetActive(false);
        }

        public void HideMiniGameRank() => miniGameRankText.enabled = false;


        #region Animation

        public UniTask PlayAppearAsync(float delay, CancellationToken ct)
        {
            cg.DOFade(1f, appearDuration).SetDelay(delay);
            var seq = DOTween.Sequence()
                .AppendInterval(delay)
                .Append(cg.DOFade(1f, appearDuration))
                .Join(transform.DOScale(1f, appearDuration).SetEase(Ease.OutBack))
                .SetUpdate(true);

            return seq.ToUniTask(cancellationToken: ct);
        }

        public UniTask PlayWinnerAndAddPointAsync(CancellationToken ct)
        {
            if (outline) outline.enabled = true;
            if (_scoreIcons.Count > 0 && !_scoreIcons[^1].activeSelf)
            {
                var scoreIcon = _scoreIcons[^1];
                scoreIcon.SetActive(true);

                var t = scoreIcon.transform;
                t.localScale = Vector3.one * 0.6f;

                SoundManager.Instance.PlaySFX(coinSfx.ToString());
                // 아이콘 펄스 애니메이션 완료까지 대기
                return t.DOScale(1f, pulseDuration)
                    .SetEase(Ease.InOutCubic)
                    .SetUpdate(true)
                    .ToUniTask(cancellationToken: ct)
                    .ContinueWith(
                        () =>
                        {
                            // if (outline)
                            //     outline.enabled = false;
                            t.localScale = Vector3.one; // 마무리 보정
                        });
            }

            // if (outline) outline.enabled = false;
            return UniTask.CompletedTask;
        }


        public void ShowTotalRank()
        {
            totalRankText.alpha = 0f;
            totalRankText.enabled = true;

            // 애니메이션 시작 전 셋팅
            var rt = totalRankText.rectTransform;
            Vector2 targetPos = totalRankText.rectTransform.anchoredPosition;
            Vector2 startPos = new Vector2(targetPos.x - 130, targetPos.y);
            rt.anchoredPosition = startPos;


            var seq = DOTween.Sequence()
                .Join(totalRankText.DOFade(1f, 0.5f))
                .Join(rt.DOAnchorPos(targetPos, 0.5f)).SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        public async UniTask ShowReward(CancellationToken ct, float duration = 0.8f, bool withSfx = false)
        {
            rewardCg.alpha = 0f;
            rewardText.text = "+ 0";


            var fadeTw = rewardCg.DOFade(1f, 0.2f).SetUpdate(true);
            await fadeTw.AsyncWaitForCompletion();

            int from = 0;
            int to = Mathf.Max(0, _totalReward);
            
            // tick 횟수 설정. 최대 tick을 제한하여 너무 소리가 많아지지 않도록 처리
            // step만큼 오를 때마다 효과음 재생
            int step = Mathf.Max(1, Mathf.CeilToInt((float)to / Mathf.Max(1, _maxTicks)));
            int lastTickPlayedValue = -step; // 첫 틱 보장

            var numTw = DOVirtual.Int(from, to, duration, v =>
                {
                    rewardText.text = $"+ {v}";
                    if (withSfx && v - lastTickPlayedValue >= step)
                    {
                        lastTickPlayedValue = v;
                        SoundManager.Instance?.PlaySFX(coinSfx.ToString());
                    }
                })
                .SetEase(Ease.OutQuad) // 같은 이징
                .SetUpdate(true); // 타임스케일 무시하고 진행(필요 없으면 제거)

            await numTw.AsyncWaitForCompletion();

            rewardText.text = $"+ {to}";
        }

        #endregion
    }
}