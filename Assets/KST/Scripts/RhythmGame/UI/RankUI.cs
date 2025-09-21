using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// RhythmGame ↔ (기존) JengaRankingUIAnimated 연동 브릿지
    /// - 교체 없이 기존 연출/애니메이션 그대로 사용
    /// </summary>
    public class RankUI : MonoBehaviour
    {
        [Header("Existing Animated UI")]
        [SerializeField] private JengaRankingUIAnimated rankingUI;

        private void Awake()
        {
            if (!rankingUI)
            {
                Debug.LogWarning("[RhythmRankingBridge] rankingUI (JengaRankingUIAnimated) 가 비어있습니다.");
            }
        }

        private void OnEnable()
        {
            StartCoroutine(IE_DelaySubscribe());
        }

        IEnumerator IE_DelaySubscribe()
        {
            yield return new WaitUntil(() => GameManager.Instance != null);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnRankingsUpdated += HandleRanksUpdated;
                GameManager.Instance.OnGameStart += HandleGameStart;
                GameManager.Instance.OnGameOver += HandleGameOver;
            }
            rankingUI.OpenForLive();
            // 레이트 조인/씬 재진입 대비: 이미 스냅샷이 있으면 바로 반영
            PrimeFromSnapshotIfAny();
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnRankingsUpdated -= HandleRanksUpdated;
                GameManager.Instance.OnGameStart -= HandleGameStart;
                GameManager.Instance.OnGameOver -= HandleGameOver;
            }
        }

        private void HandleGameStart()
        {
            rankingUI?.OpenForLive();
            PrimeFromSnapshotIfAny();
        }

        private void HandleRanksUpdated(Dictionary<string, int> ranks)
        {
            rankingUI.OpenForLive();
            // 실시간 반영
            rankingUI?.UpdateLiveRanks(ranks);
        }

        private void HandleGameOver()
        {
            // 종료 시 최종 스냅샷이 있으면 연출 포함 Show
            if (GameManager.Instance != null &&
                GameManager.Instance.TryGetLastRankSnapshot(out var finalRanks) &&
                finalRanks != null && finalRanks.Count > 0)
            {
                rankingUI?.Show(finalRanks);
            }
        }

        /// <summary>
        /// 방에 늦게 들어온 경우 등: 스냅샷이 있으면 패널 열고 즉시 반영
        /// </summary>
        private void PrimeFromSnapshotIfAny()
        {
            if (GameManager.Instance == null || rankingUI == null) return;

            if (GameManager.Instance.TryGetLastRankSnapshot(out var snap) &&
                snap != null && snap.Count > 0)
            {
                rankingUI.OpenForLive();
                rankingUI.UpdateLiveRanks(snap);
            }
        }
    }
}
