// MeteorTapMiniGame.cs (핵심만 발췌/교체)

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
    [SerializeField] private Image starImage;           // optional
    [SerializeField] private Animator starAnimator;     // optional
    [SerializeField] private MeteorTapUI ui;            // optional

    [Header("Testing Toggles")]
    [Tooltip("애니메이터 호출 무시")] public bool disableAnimator = true;
    [Tooltip("VFX 스폰 무시")] public bool disableVFX = true;
    [Tooltip("SFX 호출 무시")] public bool disableSFX = true;
    [Tooltip("UI 갱신도 생략(턴만 확인)")] public bool disableUI = false;
    [Tooltip("로그 자세히 출력")] public bool verboseLogs = true;

    [Header("VFX (Optional)")]
    [SerializeField] private VFXSpawner vfx;                  // optional
    [SerializeField] private string vfxMeteorTrail = "VFX_MeteorTrail";
    [SerializeField] private string vfxExplosion   = "VFX_Explosion_Round";

    [Header("Animation Triggers / States")]
    [SerializeField] private string trgStarShake   = "Star_Shake";
    [SerializeField] private string trgStarPulse   = "Star_Pulse";
    [SerializeField] private string trgMeteorDrop  = "Meteor_Drop";
    [SerializeField] private string trgStarExplode = "Star_Explode";
    [SerializeField] private string stIdle        = "Idle";

    // --- runtime ---
    private int roundIndex;
    private int sharedEndCount;
    private int tapCount;
    private int currentActor = -1;

    private bool myTurn => currentActor == PhotonNetwork.LocalPlayer.ActorNumber;

    // ---------- lifecycle ----------
    public void SafeInitialize()
    {
        if (verboseLogs) Debug.Log("[MeteorTap] SafeInitialize");
        TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
        EnsureAnimatorBaseState();
    }

    public void OnGameStart()
    {
        if (verboseLogs) Debug.Log("[MeteorTap] OnGameStart (wait for RPC_StartRound)");
        // 이제는 카드 선택 → TurnManager.InitTurnOrder → SceneController가 RPC_StartRound를 호출
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

    // ---------- networking ----------
    // SceneController에서 마스터가 호출하는 진입점
    [PunRPC]
    public void RPC_StartRound(int round, int endCount)
    {
        try
        {
            StartRound(round, endCount);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MeteorTap] StartRound EX: {e.Message}\n{e.StackTrace}");
        }
    }

    public void StartRound(int round, int endCount)
    {
        // 필수 컴포넌트 가드
        if (!ui) Debug.LogWarning("[MeteorTap] ui is NULL");
        //if (!sound) Debug.LogWarning("[MeteorTap] sound is NULL");
        //if (!turnBannerMine || !turnBannerOther) Debug.LogWarning("[MeteorTap] turn banners NULL");
        
        roundIndex = round;
        sharedEndCount = Mathf.Max(1, endCount);
        tapCount = 0;
        
        // 라운드 시작 = 본게임 시작 → 인게임 HUD 다시 보이기
        InGameUIManager.Instance?.Show(true);

        if (!disableUI && ui)
        {
            ui.SetRound(roundIndex + 1, sharedEndCount);
            ui.SetSharedCount(0, sharedEndCount);
            ui.SetProgress01(0f);
        }

        if (verboseLogs)
            Debug.Log($"[MeteorTap] StartRound r={roundIndex}, end={sharedEndCount}");
    }

    private Vector2Int GetRoundRange(int r)
    {
        if (r == 0) return round1; if (r == 1) return round2; return round3;
    }

    // ---------- turn ----------
    private void HandleTurnChanged(int prev, int curr)
    {
        currentActor = curr;

        // 배너/버튼 상태 갱신
        if (!disableUI && ui)
        {
            ui.ShowTurnBanners(curr);
            ui.SetTapInteractable(myTurn);
        }

        // 게임 오버 처리
        if (curr == -1)
        {
            if (!disableUI && ui)
            {
                ui.SetTapInteractable(false);
                ui.ShowGameOver(); // 게임 오버 패널만 On
            }
            return;
        }
    }

    // ---------- input ----------
    public void OnTap()
    {
        if (!myTurn) return;

        // FX 무시 모드
        if (!disableAnimator) SafeTrigger(trgStarPulse);
        if (!disableSFX) PlaySFXRandom("tap_shine1", "tap_shine2");

        if (PhotonNetwork.IsMasterClient)
            photonView.RPC(nameof(RPC_AddTap), RpcTarget.AllBuffered, 1);
    }

    [PunRPC]
    private void RPC_AddTap(int delta)
    {
        tapCount += delta;
        float p = sharedEndCount > 0 ? Mathf.Clamp01((float)tapCount / sharedEndCount) : 0f;

        if (!disableUI && ui)
        {
            ui.SetSharedCount(tapCount, sharedEndCount);
            ui.SetProgress01(p);
        }

        if (p >= 0.66f)
        {
            if (!disableAnimator) SafeTrigger(trgStarShake);
            if (!disableSFX) PlaySFX("warning_change");
        }

        if (tapCount >= sharedEndCount)
            StartCoroutine(Co_EliminateCurrent());
    }

    private IEnumerator Co_EliminateCurrent()
    {
        if (!disableAnimator) SafeTrigger(trgMeteorDrop);
        if (!disableSFX) PlaySFX("meteor_drop");
        if (!disableVFX && vfx) vfx.Spawn(vfxMeteorTrail, transform.position, Quaternion.identity, 1.2f);

        yield return new WaitForSeconds(0.65f);

        if (!disableAnimator) SafeTrigger(trgStarExplode);
        if (!disableSFX) PlaySFX("explosion_ding");
        if (!disableVFX && vfx) vfx.Spawn(vfxExplosion, transform.position, Quaternion.identity, 1.0f);

        yield return new WaitForSeconds(0.55f);

        if (PhotonNetwork.IsMasterClient)
        {
            TurnManager.Instance.Eliminate(currentActor);

            int nextRound = Mathf.Min(roundIndex + 1, 2);
            var range = GetRoundRange(nextRound);
            int nextEnd = Random.Range(range.x, range.y + 1);
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

    // SFX: 완전 무시 가능
    private ISoundFacade _sound;
    private ISoundFacade Sound => _sound ??= SoundFacade.TryFindInScene();
    private void PlaySFX(string key)  { if (!disableSFX) Sound?.PlaySFX(key); }
    private void PlaySFXRandom(string a, string b)
    {
        if (disableSFX) return;
        PlaySFX(Random.value < 0.5f ? a : b);
    }
}
}
