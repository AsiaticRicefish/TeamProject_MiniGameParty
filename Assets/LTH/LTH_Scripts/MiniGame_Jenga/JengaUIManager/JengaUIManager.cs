using DesignPattern;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class JengaUIManager : CombinedSingleton<JengaUIManager>, IGameComponent
{
    [Header("UI 요소")]
    [SerializeField] private TMP_Text timerText; // 타이머 기능 UI

    [Header("카운트다운 UI")]
    [SerializeField] private GameObject countdownPanel;      // 카운트다운 패널
    [SerializeField] private TMP_Text countdownText;         // 카운트다운 텍스트 (3, 2, 1, START!)

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
            JengaGameManager.Instance.OnGameStateChanged += OnGameStateChanged; // 게임 상태 변경 이벤트 구독

            // 초기 시간 설정
            UpdateTimerUI(JengaGameManager.Instance.GetRemainingTime());
            Debug.Log("[JengaUIManager - Initialize] 이벤트 구독 완료");
        }
        else
        {
            Debug.LogWarning("[JengaUIManager - Initialize] JengaGameManager.Instance가 null입니다.");
        }

        // UI 요소들 초기 상태 설정
        InitializeUI();

        Debug.Log("[JengaUIManager - Initialize] UI 매니저 초기화 완료");
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
    }
    #endregion

    #region 카운트다운 UI
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
                break;

            case JengaGameState.Finished:
                // 게임 종료 시 처리
                break;
        }
    }

    /// <summary>
    /// 네트워크를 통해 동기화된 카운트다운 시작 (모든 클라이언트에서 동시 실행)
    /// </summary>
    public void StartCountdown(float duration)
    {
        if (countdownPanel != null && countdownText != null)
        {
            countdownPanel.SetActive(true);
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

    protected override void OnDestroy()
    {
        // 메모리 누수 방지를 위한 이벤트 구독 해제
        if (JengaGameManager.Instance != null)
        {
            JengaGameManager.Instance.OnTimeUpdated -= UpdateTimerUI;
            JengaGameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
        }
        base.OnDestroy();
    }

}