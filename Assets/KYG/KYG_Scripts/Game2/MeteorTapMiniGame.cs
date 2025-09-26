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

    private bool myTurn => currentActor == PhotonNetwork.LocalPlayer.ActorNumber;

    // ---------- lifecycle ----------
    public void SafeInitialize()
    {
        TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
        EnsureAnimatorBaseState();

        // 씬 합류 직후에도 현재 턴 상태를 즉시 반영(배너/버튼 상태 동기화)
        HandleTurnChanged(-2, TurnManager.Instance.CurrentActor);
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

        // 턴 전환 배너(짧게) + 조작 가능 여부
        if (!disableUI && ui)
        {
            ui.ShowTurnTransition(prev, curr); // “마이 턴!”(당사자만) + “NEXT >>”(모두) 짧게 표시
            ui.SetTapInteractable(myTurn);
        }

        // 게임 종료(최후 1인 남아 TurnManager가 curr=-1 브로드캐스트)
        if (curr == -1)
        {
            if (!disableUI && ui)
            {
                ui.SetTapInteractable(false);
                // 최종 게임오버는 팀 정책에 맞춰 공용 UI에서 처리하세요
                // (여기서는 라운드 엔딩 폭발자가 아닌 "최종 우승 확정" 상황)
            }
            return;
        }

        // 새 턴 진입
        if (myTurn)
        {
            localTurnTap = 0;

            // “무탭이면 자동 1회 적용” 타이머 시작
            if (turnWindowCo != null) StopCoroutine(turnWindowCo);
            turnWindowCo = StartCoroutine(Co_NoTapAutoCommit(noTapAutoTime));
        }
        else
        {
            // 내 턴 아니면 보호적으로 인터랙션 Off
            if (!disableUI) ui?.SetTapInteractable(false);

            // 내 이전 코루틴 정리
            if (turnWindowCo != null) { StopCoroutine(turnWindowCo); turnWindowCo = null; }
        }
    }

    // “아무 것도 안 누르면” 자동으로 최소 1회 탭 반영 후 턴 종료
    private IEnumerator Co_NoTapAutoCommit(float sec)
    {
        float t = Mathf.Max(0.01f, sec);
        while (t > 0f && localTurnTap == 0)
        {
            yield return null;
            t -= Time.deltaTime;
        }

        // 여전히 0번이면 자동 1회 반영
        if (localTurnTap == 0 && myTurn)
        {
            RequestAddTap(1);   // 네트워크 증가 요청(마스터 경유)
            localTurnTap = 1;
            TryEndTurn();       // 최소 조건 채웠으니 턴 종료 시도
        }
        turnWindowCo = null;
    }

    // ---------- input ----------
    public void OnTap()
    {
        if (!myTurn) return;

        // 최대 3회 제한
        if (localTurnTap >= maxTapsPerTurn) return;

        // 간단한 연출(옵셔널)
        if (!disableAnimator) SafeTrigger(trgStarPulse);

        // 네트워크에 “+1” 요청 (마스터에서 누적 관리)
        RequestAddTap(1);
        localTurnTap++;

        // 이번 턴의 최대치에 도달하면 자동으로 턴 종료
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
        Debug.Log($"[Tap] TryEndTurn by {PhotonNetwork.LocalPlayer.ActorNumber} (master:{PhotonNetwork.IsMasterClient}) localTurnTap={localTurnTap}");
        if (!myTurn) return;

        int need = Mathf.Max(0, minTapsPerTurn - localTurnTap);
        if (need > 0)
        {
            RequestAddTap(need);
            localTurnTap += need;
        }

        // 내 턴 버튼 막기
        if (!disableUI) ui?.SetTapInteractable(false);

        // ✅ 누구 턴이든 "마스터"가 NextTurn을 호출하도록 보장
        if (PhotonNetwork.IsMasterClient)
        {
            TurnManager.Instance.NextTurn();
        }
        else
        {
            // 비마스터일 땐 마스터에게 다음 턴 요청
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
