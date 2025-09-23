using System;
using System.Collections.Generic;
using System.Threading;
using Customization;
using Cysharp.Threading.Tasks;
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

            if (rewardText) rewardText.text = "+ 0";
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
                .Join(transform.DOScale(1f, appearDuration).SetEase(Ease.OutBack));

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
                .Join(rt.DOAnchorPos(targetPos, 0.5f)).SetEase(Ease.OutBack);
        }

        public async UniTask ShowReward(CancellationToken ct, float duration = 0.8f)
        {
            rewardCg.alpha = 0f;
            float fadeT = 0f;
            const float fadeDur = 0.2f;
            while (fadeT < fadeDur)
            {
                fadeT += Time.deltaTime;
                rewardCg.alpha = Mathf.Clamp01(fadeT / fadeDur);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            rewardCg.alpha = 1f;

            float t = 0f;
            int last = -1;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - p, 2f); // EaseOutQuad

                int value = Mathf.RoundToInt(Mathf.Lerp(0, _totalReward, eased));
                if (value != last)
                {
                    rewardText.text = $"+ {value}";
                    last = value;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            rewardText.text = $"+ {_totalReward}";
        }

        #endregion
    }
}