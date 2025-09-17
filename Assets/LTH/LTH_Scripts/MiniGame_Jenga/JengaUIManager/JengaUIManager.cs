using System.Collections;
using System.Collections.Generic;
using DesignPattern;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InputBlocker;

public class JengaUIManager : CombinedSingleton<JengaUIManager>, IGameComponent
{
    [Header("UI 요소")]
    [SerializeField] private TMP_Text timerText; // 타이머 기능 UI

    [Header("카운트다운 UI")]
    [SerializeField] private GameObject countdownPanel;      // 카운트다운 패널
    [SerializeField] private TMP_Text countdownText;         // 카운트다운 텍스트 (3, 2, 1, START!)

    [Header("랭킹 UI")]
    [SerializeField] private JengaRankingUIAnimated rankingUI;

    [Header("회전 버튼")]
    [SerializeField] private Button rotateButton;

    [Header("대기 UI")]
    [SerializeField] private GameObject waitingPanel;
    
    private bool _iAmEliminated = false;
    private InputLockToken _eliminateLock;


    protected override void OnAwake()
    {
        base.isPersistent = false; // 젠가 씬에서만 사용
        base.OnAwake();
    }

    public void Initialize()
    {
        Debug.Log("[JengaUIManager - Initialize] UI 매니저 초기화 시작");

        // 게임 매니저의 시간 업데이트 이벤트 구독
        if (JengaGameManager.Instance != null)
        {
            JengaGameManager.Instance.OnTimeUpdated += UpdateTimerUI;
            JengaGameManager.Instance.OnGameStateChanged += OnGameStateChanged;         // 게임 상태 변경 이벤트 구독
            JengaGameManager.Instance.OnGameFinished += OnGameFinished_ShowRanking;
            JengaGameManager.Instance.OnRankingsUpdated += OnRankingsUpdated_Live;      // 실시간 랭킹 구독

            // 초기 시간 설정
            UpdateTimerUI(JengaGameManager.Instance.GetRemainingTime());
            Debug.Log("[JengaUIManager - Initialize] 이벤트 구독 완료");
        }

        // UI 요소들 초기 상태 설정
        InitializeUI();

        // 재입장/중도 합류: 이미 Playing 상태면 바로 켜두기
        if (JengaGameManager.Instance != null &&
            JengaGameManager.Instance.currentState == JengaGameState.Playing)
        {
            if (rankingUI != null) rankingUI.OpenForLive();
        }

        PrimeRankingUIIfPossible();

        Debug.Log("[JengaUIManager - Initialize] UI 매니저 초기화 완료");
    }

    /// <summary>
    /// 게임 상태 변경 시 호출되는 이벤트 핸들러
    /// </summary>
    private void OnGameStateChanged(JengaGameState newState)
    {
        switch (newState)
        {
            case JengaGameState.Playing:
                // 게임 시작 시 카운트다운 UI 숨김 (혹시 남아있을 경우를 대비)
                HideCountdown();
                if (rankingUI != null) rankingUI.OpenForLive();
                PrimeRankingUIIfPossible();

                _iAmEliminated = false;
                if (waitingPanel) waitingPanel.SetActive(false);

                _eliminateLock?.Dispose();
                _eliminateLock = null;
                break;

            case JengaGameState.Finished:
                // 게임 종료 시 00:00
                if (timerText != null) timerText.text = "0:00";

                if (JengaGameManager.Instance != null)
                    JengaGameManager.Instance.OnRankingsUpdated -= OnRankingsUpdated_Live;
                break;
        }
    }

    #region UI 초기 활성화 상태
    /// <summary>
    /// UI 요소들 초기 상태 설정
    /// </summary>
    private void InitializeUI()
    {
        // 타이머 텍스트 활성화 (게임 내내 보여야 함)
        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
        }

        // 카운트다운 패널 비활성화 (필요할 때만 활성화)
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        // 카운트다운 텍스트도 미리 설정
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }

        // 랭킹 패널은 기본 비활성
        if (rankingUI != null)
        {
            rankingUI.Hide();
        }

        if (rotateButton)
        {
            rotateButton.gameObject.SetActive(false);
        }

        if (waitingPanel)
        {
            waitingPanel.SetActive(false);
        }
        _iAmEliminated = false;
    }
    #endregion

    #region 카운트다운 UI

    /// <summary>
    /// 네트워크를 통해 동기화된 카운트다운 시작 (모든 클라이언트에서 동시 실행)
    /// </summary>
    public void StartCountdown(float duration)
    {
        if (countdownPanel != null && countdownText != null)
        {
            countdownPanel.SetActive(true);

            if (rotateButton) 
            { 
                rotateButton.gameObject.SetActive(false); 
            }

            StartCoroutine(CountdownCoroutine(duration));
        }
    }

    /// <summary>
    /// 카운트다운 UI 숨기기
    /// </summary>
    public void HideCountdown()
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 동기화된 카운트다운 코루틴
    /// </summary>
    private IEnumerator CountdownCoroutine(float duration)
    {
        int countdown = Mathf.RoundToInt(duration);

        // 숫자 카운트다운 (3, 2, 1)
        while (countdown > 0)
        {
            countdownText.text = countdown.ToString();
            StartCoroutine(ScaleAnimation(countdownText.transform));

            yield return new WaitForSeconds(1f);
            countdown--;
        }

        // "START!" 표시
        countdownText.text = "START!";
        StartCoroutine(ScaleAnimation(countdownText.transform));

        yield return new WaitForSeconds(1f);

        if (rotateButton)
        {
            rotateButton.gameObject.SetActive(true);
            rotateButton.interactable = true;
        }
    }

    /// <summary>
    /// 간단한 스케일 애니메이션 (임시로 만든 코드로 제거하거나 대폭 수정 예정)
    /// </summary>
    private IEnumerator ScaleAnimation(Transform target)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.localScale;
        Vector3 largeScale = originalScale * 1.2f;

        // 커지기
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            target.localScale = Vector3.Lerp(originalScale, largeScale, elapsed / 0.2f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 원래 크기로
        elapsed = 0f;
        while (elapsed < 0.3f)
        {
            target.localScale = Vector3.Lerp(largeScale, originalScale, elapsed / 0.3f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        target.localScale = originalScale;
    }

    #endregion

    #region 타이머 UI
    private void UpdateTimerUI(float remainingTime)
    {
        if (timerText != null && JengaGameManager.Instance != null)
        {
            timerText.text = JengaGameManager.Instance.GetFormattedTime();
        }
    }

    #endregion

    #region 회전 UI

    public void OnClick_RotateTower()
    {
        var mgr = JengaTowerManager.Instance;
        if (mgr == null)  return;

        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        var tower = mgr.GetPlayerTower(myActor);
        if (tower == null) return;

        var rot = tower.GetComponentInParent<JengaRotateController>()
              ?? tower.GetComponent<JengaRotateController>()
              ?? (tower.transform.parent ? tower.transform.parent.GetComponent<JengaRotateController>() : null);

        if (rot == null) return; 

        rot.Toggle();
    }

    public void SetRotateButtonInteractable(bool interactable)
    {
        if (rotateButton) rotateButton.interactable = interactable;
    }

    public void HideRotateButton() { if (rotateButton) rotateButton.gameObject.SetActive(false); }
    public void ShowRotateButton() { if (rotateButton) rotateButton.gameObject.SetActive(true); }

    #endregion

    #region 랭킹 UI

    /// <summary>
    /// 게임 종료 시 랭킹 표시
    /// </summary>
    /// <param name="rankings"></param>
    private void OnGameFinished_ShowRanking(Dictionary<string, int> rankings)
    {
        if (rankingUI == null)
        {
            Debug.LogWarning("[JengaUIManager] rankingUI not set.");
            return;
        }
        rankingUI.Show(rankings);   // 정렬/애니메이션/1등 강조까지 내부에서 처리
    }

    private void OnRankingsUpdated_Live(Dictionary<string, int> ranks)
    {
        if (JengaGameManager.Instance.currentState == JengaGameState.Finished) return;
        rankingUI?.UpdateLiveRanks(ranks);
    }

    #endregion

    #region 대기 UI
    public void ShowWaiting()
    {
        _iAmEliminated = true;

        if (waitingPanel) waitingPanel.SetActive(true);

        if (_eliminateLock == null)
            _eliminateLock = InputManager.Instance?.Acquire(InputType.Interaction, "Jenga eliminated");

        HideRotateButton(); // 조작 불가
    }

    private void PrimeRankingUIIfPossible()
    {
        if (rankingUI == null || JengaGameManager.Instance == null) return;

        // 룸 프로퍼티에서 읽기
        Dictionary<string, int> ranks = null;
        var room = PhotonNetwork.CurrentRoom;
        if (room != null &&
            room.CustomProperties.TryGetValue(JengaRoomProps.KEY_RANK_UIDS, out var uObj) &&
            room.CustomProperties.TryGetValue(JengaRoomProps.KEY_RANK_VALS, out var vObj) &&
            uObj is string[] uids && vObj is int[] vals && uids.Length > 0)
        {
            ranks = new Dictionary<string, int>();
            for (int i = 0; i < uids.Length && i < vals.Length; i++)
                ranks[uids[i]] = vals[i];
        }

        // 없으면 GameManager 캐시 사용
        if (ranks == null && JengaGameManager.Instance.TryGetLastRankSnapshot(out var snap))
            ranks = snap;

        if (ranks != null && ranks.Count > 0)
        {
            rankingUI.OpenForLive();
            rankingUI.UpdateLiveRanks(ranks);
        }
    }

    #endregion


    #region 강제 정리 (플레이어 1명이라도 이탈 시 호출)
    protected override void OnDestroy()
    {
        Debug.Log("[JengaUIManager] OnDestroy - cleaning up resources");

        // 메모리 누수 방지를 위한 이벤트 구독 해제
        if (JengaGameManager.Instance != null)
        {
            JengaGameManager.Instance.OnTimeUpdated -= UpdateTimerUI;
            JengaGameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            JengaGameManager.Instance.OnGameFinished -= OnGameFinished_ShowRanking;
            JengaGameManager.Instance.OnRankingsUpdated -= OnRankingsUpdated_Live;
        }
        _eliminateLock?.Dispose();
        _eliminateLock = null;
        base.OnDestroy();
    }
    #endregion

}