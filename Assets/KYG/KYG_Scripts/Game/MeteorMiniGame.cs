using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 별똥별(탭) 미니게임 컨트롤러
/// - 라운드/인원에 따라 엔딩 카운트(최대 탭 횟수) 설정
/// - 내 턴일 때만 탭 가능
/// - 탭 피드백(머테리얼/사운드/낙하FX/표정)
/// - 위험도 점증(색상, 경고음, 표정)
/// - 엔딩 카운트 도달 시 탈락 처리 후 다음 턴
/// </summary>

namespace KYG
{
    
public class MeteorTapMiniGame : MonoBehaviourPun
{
    [Header("Refs")]
    [SerializeField] private Renderer starRenderer;   // 중앙 별 머테리얼 변경 대상
    [SerializeField] private Material starNormalMat;
    [SerializeField] private Material starWarningMat;
    [SerializeField] private AudioSource sfxTap;
    [SerializeField] private AudioSource sfxMeteor;
    [SerializeField] private AudioSource sfxWarning;
    [SerializeField] private ParticleSystem vfxMeteor; // 하늘 별똥별 낙하
    [SerializeField] private Animator unimoFace;       // 유니모 표정(Idle/Smile/Worried/Anxious 등)
    [SerializeField] private TMP_Text  turnBannerMine; // "마이 턴!" (나만 보임)
    [SerializeField] private TMP_Text  turnBannerOther;// "상대 턴" (모두 보임)
    [SerializeField] private Slider    turnTimerUI;    // 공통 UI(점차 줄어드는 숫자/슬라이더)
    
    [Header("Count UI")] // 카운트 숫자 UI
    [SerializeField] private TMP_Text countNumberText;   // 씬의 숫자 텍스트에 바인딩 (3,2,1...)
    [SerializeField] private float    countDuration = 3f; // 카운트 총 시간(초) 예: 3초
    [SerializeField] private float    afterDelay    = 1.0f; // 카운트 종료 후 다음 턴으로 넘기기 전 딜레이
    
    [Header("Tap Limits per Turn")] // 탭 제한
    [SerializeField] private int minTapPerTurn = 1;
    [SerializeField] private int maxTapPerTurn = 3;

    [Header("Ending Count Ranges (by round)")]
    [SerializeField] private Vector2Int round1 = new Vector2Int(20, 30);
    [SerializeField] private Vector2Int round2 = new Vector2Int(15, 25);
    [SerializeField] private Vector2Int round3 = new Vector2Int(10, 20);

    private int currentEndingCount;   // 이번 라운드 엔딩 카운트(탭 수 한계)
    private int currentTap;           // 현재까지 탭 수
    private bool myTurn;              // 내 턴 여부
    private float turnTimeLeft;       // 턴 제한시간(선택)
    private float turnTimeMax = 10f;  // 기본 10s (필요 시 조정)
    
    
    // 카운트/탭 상태
    private bool countActive = false;
    private int  tapsThisTurn = 0;

    void OnEnable()
    {
        // TurnManager의 브로드캐스트 이후 RPC_SetCurrentTurn에서 내턴 여부가 판별됩니다. (로그 기준 매 프레임 판단 불필요)
        // 여기서는 매 턴 시작시 호출될 InitTurn()을 TurnManager 쪽에서 호출해주면 깔끔합니다.
    }

    /// <summary>TurnManager.RPC_SetCurrentTurn 끝에서, 내/상대 턴 UI와 함께 호출</summary>
    public void InitTurn(bool isMine, int roundIndex, int alivePlayerCount)
    {
        myTurn = isMine;
        currentTap = 0;

        currentEndingCount = GetEndingCountForRound(roundIndex);
        currentEndingCount -= Mathf.Max(0, 4 - alivePlayerCount) * 2;
        currentEndingCount = Mathf.Max(3, currentEndingCount);

        if (starRenderer) starRenderer.material = starNormalMat;
        if (turnBannerMine)  turnBannerMine.gameObject.SetActive(isMine);
        if (turnBannerOther) turnBannerOther.gameObject.SetActive(!isMine);

        if (unimoFace) unimoFace.Play("Idle", 0, 0);

        // (기존 공용 슬라이더는 그대로 유지 – 필요시 숨겨도 됩니다)
        turnTimeMax = 10f;
        turnTimeLeft = turnTimeMax;
        if (turnTimerUI) turnTimerUI.value = 1f;

        // NEW: 내 턴이면 카운트 숫자 UI 시작, 상대 턴이면 숫자 숨김
        if (countNumberText) countNumberText.gameObject.SetActive(isMine);
        tapsThisTurn = 0;
        if (isMine)
            StartCoroutine(CoCountWindow());
        else
            countActive = false;
    }

    void Update()
    {
        // 타이머 진행(마스터 기준으로만 다음 턴 넘기지만, 로컬 UI는 동기 표현)
        if (turnTimeLeft > 0f)
        {
            turnTimeLeft -= Time.deltaTime;
            if (turnTimerUI) turnTimerUI.value = Mathf.Clamp01(turnTimeLeft / turnTimeMax);
        }
    }

    // UI 버튼(탭) 연결: 내 턴일 때만 반응
    public void OnTap()
    {
        if (!myTurn) return;
        if (!countActive) return;          // NEW: 카운트 중에만 허용
        if (tapsThisTurn >= maxTapPerTurn) return; // NEW: 최대 3회 제한

        DoOneTapFXAndLogic();
        tapsThisTurn++;
    }

    private void DoOneTapFXAndLogic()
    {
        currentTap++;

        if (starRenderer && starNormalMat) starRenderer.material = starNormalMat;
        if (sfxTap) sfxTap.Play();
        if (vfxMeteor) vfxMeteor.Play();
        if (unimoFace) unimoFace.Play("Smile", 0, 0);

        // 위험도
        float dangerRatio = (float)currentTap / Mathf.Max(1, currentEndingCount);
        ApplyDanger(dangerRatio);

        // 엔딩 카운트
        if (currentTap >= currentEndingCount)
        {
            photonView.RPC(nameof(RPC_OnEndingReached), RpcTarget.All);
        }
    }

    private void ApplyDanger(float r)
    {
        // r: 0~1. 기획서 예시처럼 1/3, 2/3 구간에서 강도 업
        if (r > 0.66f)
        {
            if (starRenderer && starWarningMat) starRenderer.material = starWarningMat;
            if (sfxWarning && !sfxWarning.isPlaying) sfxWarning.Play();
            if (unimoFace) unimoFace.Play("Anxious", 0, 0); // 초조/불안 표정
        }
        else if (r > 0.33f)
        {
            if (unimoFace) unimoFace.Play("Worried", 0, 0);
        }
    }
    
    private System.Collections.IEnumerator CoCountWindow()
    {
        countActive = true;

        float t = countDuration;
        while (t > 0f)
        {
            // 소숫점 반올림해 “3,2,1” 형태로 보여주기
            if (countNumberText)
            {
                int display = Mathf.CeilToInt(t);
                countNumberText.text = display.ToString();
            }
            t -= Time.deltaTime;
            yield return null;
        }

        // 마지막 “0” 표기 후 숨김
        if (countNumberText)
        {
            countNumberText.text = "0";
            // 0을 아주 잠깐 보여주고 꺼도 좋음
            yield return null;
            countNumberText.gameObject.SetActive(false);
        }

        countActive = false;

        // NEW: 최소 탭 1회 보장 – 안 눌렀으면 1회 처리
        if (tapsThisTurn < minTapPerTurn)
        {
            DoOneTapFXAndLogic();
            tapsThisTurn = Mathf.Max(tapsThisTurn, 1);
        }

        // NEW: 마스터만 일정 시간 후 다음 턴
        if (PhotonNetwork.IsMasterClient)
        {
            yield return new WaitForSeconds(afterDelay);
            // 엔딩에 걸려 RPC_OnEndingReached가 이미 NextTurn을 호출했다면
            // 중복 호출을 피하고 싶다면 여기서 간단한 가드(예: currentTap < currentEndingCount)로 체크
            if (currentTap < currentEndingCount)
            {
                KYG.TurnManager.Instance.NextTurn();
            }
        }
    }

    [PunRPC]
    private void RPC_OnEndingReached()
    {
        // 엔딩 연출(폭발/느낌표/별 상승 등) → 탈락자 처리
        if (sfxMeteor) sfxMeteor.Play();
        if (unimoFace) unimoFace.Play("Anxious", 0, 0);

        // TODO: 폭발 이펙트, 별 상승 연출 트리거 등 기획서 연출 추가

        // 마스터만 게임 상태 갱신 & 다음 턴 호출
        if (PhotonNetwork.IsMasterClient)
        {
            // TODO: 현재 턴의 플레이어를 탈락 처리(게임 매니저 players에서 제거/Flag)
            // ex) ShootingGameManager.Instance.Eliminate(CurrentTurnUid);

            KYG.TurnManager.Instance.NextTurn(); // 다음 턴(인원 줄어들었으면 라운드/카운트 자동 단축 효과)
        }
    }

    private int GetEndingCountForRound(int roundIndex)
    {
        Vector2Int range = round1;
        if (roundIndex == 2) range = round2;
        else if (roundIndex >= 3) range = round3;
        return Random.Range(range.x, range.y + 1);
    }
}
}
