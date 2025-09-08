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

    [Header("Ending Count Ranges (by round)")]
    [SerializeField] private Vector2Int round1 = new Vector2Int(20, 30);
    [SerializeField] private Vector2Int round2 = new Vector2Int(15, 25);
    [SerializeField] private Vector2Int round3 = new Vector2Int(10, 20);

    private int currentEndingCount;   // 이번 라운드 엔딩 카운트(탭 수 한계)
    private int currentTap;           // 현재까지 탭 수
    private bool myTurn;              // 내 턴 여부
    private float turnTimeLeft;       // 턴 제한시간(선택)
    private float turnTimeMax = 10f;  // 기본 10s (필요 시 조정)

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

        // 라운드별 엔딩카운트 설정
        currentEndingCount = GetEndingCountForRound(roundIndex);
        // 인원 감소 시 더 줄이길 원한다면 가중치 적용
        currentEndingCount -= Mathf.Max(0, 4 - alivePlayerCount) * 2;
        currentEndingCount = Mathf.Max(3, currentEndingCount);

        // 위험도 초기화
        if (starRenderer) starRenderer.material = starNormalMat;
        if (turnBannerMine)  turnBannerMine.gameObject.SetActive(isMine);
        if (turnBannerOther) turnBannerOther.gameObject.SetActive(!isMine);

        // 표정/사운드 초기화
        if (unimoFace) unimoFace.Play("Idle", 0, 0);

        // 타이머 초기화(공통 UI)
        turnTimeMax = 10f;  // 필요시 라운드별 시간 조정
        turnTimeLeft = turnTimeMax;
        if (turnTimerUI) turnTimerUI.value = 1f;
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

        currentTap++;

        
        if (starRenderer && starNormalMat) starRenderer.material = starNormalMat;
        if (sfxTap) sfxTap.Play();
        if (vfxMeteor) vfxMeteor.Play();
        if (unimoFace) unimoFace.Play("Smile", 0, 0);

        // 위험도
        float dangerRatio = (float)currentTap / Mathf.Max(1, currentEndingCount);
        ApplyDanger(dangerRatio);

        // 엔딩 카운트 도달 → 탈락 연출 & 다음 턴
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
