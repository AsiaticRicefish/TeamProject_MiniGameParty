// MeteorTapMiniGame.cs
using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace YG
{
public class MeteorTapMiniGame : MonoBehaviourPun
{
    [Header("Round End Count (Inclusive)")]
    [SerializeField] private Vector2Int round1 = new(20, 30);
    [SerializeField] private Vector2Int round2 = new(15, 25);
    [SerializeField] private Vector2Int round3 = new(10, 20);

    [Header("UI / Visual Refs (Optional)")]
    [SerializeField] private Image starImage;           
    [SerializeField] private Animator starAnimator;     
    [SerializeField] private MeteorTapUI ui;            

    [Header("Tap Rules (Per Turn)")]
    [Tooltip("한 플레이어의 1턴 동안 최소 보장 탭 수")]
    [SerializeField] private int minTapsPerTurn = 1;   // 요구사항: 최소 1
    [Tooltip("한 플레이어의 1턴 동안 최대 허용 탭 수")]
    [SerializeField] private int maxTapsPerTurn = 3;   // 요구사항: 최대 3
    [Tooltip("아무것도 누르지 않으면 이 시간(초) 뒤 자동으로 최소 탭(=1)을 적용하고 턴 종료")]
    [SerializeField] private float noTapAutoTime = 3.0f;

    [Header("Testing Toggles")]
    public bool disableAnimator = true;
    public bool disableVFX = true;
    public bool disableSFX = true;
    public bool disableUI = false;
    public bool verboseLogs = true;

    [Header("Animation/VFX keys (Optional)")]
    [SerializeField] private string trgStarShake   = "Star_Shake";
    [SerializeField] private string trgStarPulse   = "Star_Pulse";
    [SerializeField] private string trgMeteorDrop  = "Meteor_Drop";
    [SerializeField] private string trgStarExplode = "Star_Explode";
    [SerializeField] private string stIdle        = "Idle";

    // --- runtime ---
    private int roundIndex;
    private int sharedEndCount;     // 라운드 종료 카운트(모든 플레이어 공유)
    private int tapCount;           // 현재까지 누적 탭 수(공유 수치)
    private int currentActor = -1;  // 현재 턴 주인의 ActorNumber

    private int localTurnTap;       // "이번 내 턴" 동안 내가 누른 횟수
    private Coroutine turnWindowCo; // 무탭 자동 처리 코루틴
    
    private const int MaxTapPerTurn = 3;     // 턴당 최대 탭 수(기획 기준)
   
    private bool turnResolved = false;       // ★ 이미 커밋(해소)했는지 여부 가드

    private bool myTurn => currentActor == PhotonNetwork.LocalPlayer.ActorNumber;

    // ---------- lifecycle ----------
    // 초기화 직후에도 현재/다음 배너가 올바르게 보이도록 보정
    public void SafeInitialize()
    {
        TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
        EnsureAnimatorBaseState();

        int curr = TurnManager.Instance.CurrentActor;    // 마스터/클라 모두 최신 브로드캐스트 반영
        int next = TurnManager.Instance.GetNextActor();
        if (!disableUI && ui)
        {
            ui.UpdateTurnBanners(curr, next);
            ui.SetTapInteractable(curr == PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }

    public void OnGameStart()
    {
        if (verboseLogs) Debug.Log("[MeteorTap] OnGameStart (Turn order 이후 RPC_StartRound를 통해 라운드 시작)");
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;
    }

    private void EnsureAnimatorBaseState()
    {
        if (!disableAnimator && starAnimator)
            SafePlay(stIdle);
    }

    // ---------- networking: 라운드 시작 ----------
    [PunRPC]
    public void RPC_StartRound(int round, int endCount)
    {
        StartRound(round, endCount);
    }

    public void StartRound(int round, int endCount)
    {
        roundIndex = round;
        sharedEndCount = Mathf.Max(1, endCount);
        tapCount = 0;                 // 라운드 시작이므로 누적 초기화
        localTurnTap = 0;

        // 인게임 HUD 다시 보이기 (카드 선택 UI 이후)
        InGameUIManager.Instance?.Show(true);

        if (!disableUI && ui)
        {
            ui.SetRound(roundIndex + 1, sharedEndCount);
            ui.SetSharedCount(0, sharedEndCount);
            ui.SetProgress01(0f);
            ui.SetTapInteractable(false); // 턴 들어오면 켬
        }

        if (verboseLogs) Debug.Log($"[MeteorTap] StartRound r={roundIndex}, end={sharedEndCount}");
    }

    private Vector2Int GetRoundRange(int r)
    {
        if (r == 0) return round1; if (r == 1) return round2; return round3;
    }

    // ---------- turn ----------
    private void HandleTurnChanged(int prev, int curr)
    {
        currentActor = curr;

        if (!disableUI && ui)
        {
            int next = TurnManager.Instance.GetNextActor();
            ui.UpdateTurnBanners(curr, next);
            ui.SetTapInteractable(myTurn);
        }

        if (curr == -1)
        {
            if (!disableUI && ui) ui.SetTapInteractable(false);
            return;
        }

        if (myTurn)
        {
            localTurnTap = 0;
            turnResolved = false; // ✅ 이번 턴 시작이므로 커밋/해소 플래그 초기화

            if (turnWindowCo != null) StopCoroutine(turnWindowCo);
            turnWindowCo = StartCoroutine(Co_NoTapAutoCommit(noTapAutoTime));
        }
        else
        {
            if (!disableUI) ui?.SetTapInteractable(false);
            if (turnWindowCo != null) { StopCoroutine(turnWindowCo); turnWindowCo = null; }
        }
    }

    // “아무 것도 안 누르면” 자동으로 최소 1회 탭 반영 후 턴 종료
    private IEnumerator Co_NoTapAutoCommit(float sec)
    {
        turnResolved = false; // 안전: 턴 시작 시 항상 false

        float elapsed = 0f;
        float limit = Mathf.Max(0.01f, sec);

        // ⬇️ 더 이상 localTurnTap 값(0/1/2/3)을 기다리지 않습니다.
        while (elapsed < limit && !turnResolved && myTurn)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        turnWindowCo = null;

        // 이미 커밋됐거나 내 턴이 아니면 종료
        if (turnResolved || !myTurn) yield break;

        // 시간 종료 → 현재까지의 탭 수(0/1/2/3)로 턴 종료
        // 0회면 TryEndTurn()에서 1회로 자동 보정
        TryEndTurn();
    }

    // ---------- input ----------
    public void OnTap()
    {
        if (!myTurn) return;        // 내 턴이 아니면 무시
        if (turnResolved) return;   // ✅ 이미 커밋(턴 종료)된 상태면 무시
        if (localTurnTap >= maxTapsPerTurn) return; // 최대 3회 제한

        if (!disableAnimator) SafeTrigger(trgStarPulse);

        // 네트워크에 “+1” 요청 (마스터가 진짜 누적/브로드캐스트)
        RequestAddTap(1);
        localTurnTap++;

        // 3회 도달 시 즉시 턴 종료
        if (localTurnTap >= maxTapsPerTurn)
            TryEndTurn();
    }

    /// <summary>
    /// 내 턴 종료 시도:
    /// - 최소치 미만이면 부족분을 자동으로 채워서 반영
    /// - 마스터가 NextTurn() 호출
    /// </summary>
    private void TryEndTurn()
    {
        if (!myTurn) return;
        if (turnResolved) return;   // ✅ 이중 호출 방지
        turnResolved = true;        // ✅ 이제부터는 더 이상 커밋 로직 진입 금지

        // 타이머 코루틴 중지
        if (turnWindowCo != null) { StopCoroutine(turnWindowCo); turnWindowCo = null; }

        // 최소 보장치(예: 0회면 1회로) 자동 보정
        int need = Mathf.Max(0, minTapsPerTurn - localTurnTap);
        if (need > 0)
        {
            RequestAddTap(need);   // 네트워크에 누적 반영
            localTurnTap += need;
        }

        // 내 턴 입력 차단
        if (!disableUI) ui?.SetTapInteractable(false);

        // 다음 턴 진행(마스터만 직접 호출, 클라는 요청)
        if (PhotonNetwork.IsMasterClient)
        {
            TurnManager.Instance.NextTurn();
        }
        else
        {
            photonView.RPC(nameof(RPC_RequestNextTurn), RpcTarget.MasterClient);
        }
    }
    
    [PunRPC]
    private void RPC_RequestNextTurn(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 보안: 정말 그 턴의 주인이 보낸 건지 확인
        if (TurnManager.Instance.CurrentActor != info.Sender.ActorNumber) return;

        TurnManager.Instance.NextTurn();
        Debug.Log($"[Turn] RPC_RequestNextTurn from {info.Sender.ActorNumber}, current={TurnManager.Instance.CurrentActor}");
    }

    // ---------- network tap add: 마스터 경유 ----------
    private void RequestAddTap(int delta)
    {
        if (delta <= 0) return;

        if (PhotonNetwork.IsMasterClient)
        {
            // 마스터면 즉시 집계 + 브로드캐스트
            photonView.RPC(nameof(RPC_AddTap), RpcTarget.AllBuffered, delta, currentActor);
        }
        else
        {
            // 클라면 마스터에게 요청
            photonView.RPC(nameof(RPC_RequestAddTap), RpcTarget.MasterClient, delta, PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }

    [PunRPC]
    private void RPC_RequestAddTap(int delta, int requesterActor)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 1) 이 미니게임이 “마스터가 알고 있는 현재 턴 주인”과 일치하는가?
        if (requesterActor != currentActor) return;

        // 2) TurnManager의 현재 턴과도 일치하는가? (이중 검증)
        if (TurnManager.Instance.CurrentActor != requesterActor) return;

        photonView.RPC(nameof(RPC_AddTap), RpcTarget.AllBuffered, delta, currentActor);
    }

    [PunRPC]
    private void RPC_AddTap(int delta, int turnOwnerActor)
    {
        // 공유 카운트 증가
        tapCount += delta;
        float p = sharedEndCount > 0 ? Mathf.Clamp01((float)tapCount / sharedEndCount) : 0f;

        if (!disableUI && ui)
        {
            ui.SetSharedCount(tapCount, sharedEndCount);
            ui.SetProgress01(p);
        }

        // 경고 구간 진입 연출(옵션)
        if (p >= 0.66f && !disableAnimator) SafeTrigger(trgStarShake);

        // 라운드 엔딩카운트 도달 → “이번 턴 주인”이 폭발/탈락
        if (tapCount >= sharedEndCount)
        {
            // ★ 게임오버 UI는 “해당 턴 주인”에게만 띄우기 위해 타겟 정보 전파
            photonView.RPC(nameof(RPC_ShowGameOverFor), RpcTarget.All, turnOwnerActor);

            // 연출 + 탈락 + 다음 라운드 시작은 코루틴으로 처리
            StartCoroutine(Co_EliminateCurrent(turnOwnerActor));
        }
    }

    [PunRPC]
    private void RPC_ShowGameOverFor(int actorNumber)
    {
        // 내 Actor와 일치하는 사람에게만 게임오버 패널 표시
        if (!disableUI && ui && PhotonNetwork.LocalPlayer.ActorNumber == actorNumber)
            ui.ShowGameOverLocalOnly();
    }

    private IEnumerator Co_EliminateCurrent(int eliminatedActor)
    {
        // 간단한 연출(옵션)
        if (!disableAnimator) SafeTrigger(trgMeteorDrop);
        yield return new WaitForSeconds(0.65f);
        if (!disableAnimator) SafeTrigger(trgStarExplode);
        yield return new WaitForSeconds(0.55f);

        // 마스터가 탈락/다음 라운드 세팅
        if (PhotonNetwork.IsMasterClient)
        {
            // 현재 턴 주인 탈락
            TurnManager.Instance.Eliminate(eliminatedActor);

            // 다음 라운드 준비(남은 인원 > 1일 때)
            // 라운드 인덱스는 0..2까지만 증가
            int nextRound = Mathf.Min(roundIndex + 1, 2);
            var range = GetRoundRange(nextRound);
            int nextEnd = Random.Range(range.x, range.y + 1);

            // 전원에게 같은 라운드/엔딩카운트 브로드캐스트
            photonView.RPC(nameof(RPC_StartRound), RpcTarget.AllBuffered, nextRound, nextEnd);
        }
    }

    // ---------- safe helpers ----------
    private void SafePlay(string state)
    {
        if (disableAnimator || !starAnimator) return;
        int hash = Animator.StringToHash(state);
        if (starAnimator.HasState(0, hash)) starAnimator.Play(hash, 0, 0f);
    }
    private void SafeTrigger(string trig)
    {
        if (disableAnimator || !starAnimator) return;
        foreach (var p in starAnimator.parameters)
            if (p.name == trig) { starAnimator.SetTrigger(trig); return; }
    }
}
}
