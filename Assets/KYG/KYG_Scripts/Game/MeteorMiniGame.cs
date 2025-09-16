using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using DesignPattern;

namespace KYG
{
    /// <summary>
    /// 별똥별(탭) 미니게임.
    /// - TurnManager에서 확정한 엔딩카운트를 직접 전달받아(InitTurnWithEnding) 모든 클라 동일하게 사용
    /// - 내 턴에서만 탭 가능(1턴 최대 3회), 노탭 시 최소 1회 보장
    /// - 위험도/사운드/연출, 엔딩 도달 시 전 클라 동기화
    /// - 중복 종료/중복 NextTurn 방지 가드
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class MeteorTapMiniGame : MonoBehaviourPun
    {
        [Header("Refs")]
        [SerializeField] private Renderer starRenderer;
        [SerializeField] private Material starNormalMat;
        [SerializeField] private Material starWarningMat;
        [SerializeField] private AudioSource sfxTap;
        [SerializeField] private AudioSource sfxMeteor;
        [SerializeField] private AudioSource sfxWarning;
        [SerializeField] private ParticleSystem vfxMeteor;
        [SerializeField] private Animator unimoFace;
        [SerializeField] private TMP_Text  turnBannerMine;
        [SerializeField] private TMP_Text  turnBannerOther;
        [SerializeField] private Slider    turnTimerUI;
        
        [SerializeField] private Transform starRoot;          // 별 오브젝트 루트(상승용)
        [SerializeField] private float     starRiseHeight = 2f;
        [SerializeField] private float     starRiseDuration = 1.2f;
        
        [SerializeField] private TMP_Text  exclamationText;  // 느낌표 텍스트
        [SerializeField] private AudioSource sfxExclamation; // 느낌표 사운드
        [SerializeField] private ParticleSystem vfxExplosion;// 폭발 이펙트
        [SerializeField] private string animFaceAnxious = "Anxious"; // 애니메이션 클립명 통일
        [SerializeField] private string animFaceWorried = "Worried";
        [SerializeField] private string animFacePanic   = "Fear";    // "불안" 계열 최종 표정

        [Header("Count UI")]
        [SerializeField] private TMP_Text countNumberText;
        [SerializeField] private float    countDuration = 3f;
        [SerializeField] private float    afterDelay    = 1f;

        [Header("Tap Limits per Turn")]
        [SerializeField] private int minTapPerTurn = 1;
        [SerializeField] private int maxTapPerTurn = 3;

        // 내부 상태
        private int  currentEndingCount;
        private int  currentTap;
        private bool myTurn;
        private float turnTimeLeft;
        private float turnTimeMax = 10f;

        private bool countActive  = false;
        private int  tapsThisTurn = 0;
        private bool endedThisTurn = false;

        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// TurnManager.RPC_SetCurrentTurn → EnsureMiniAndInit 에서 호출.
        /// 모든 클라 동일 엔딩카운트(sharedEndingCount)를 사용.
        /// </summary>
        public void InitTurnWithEnding(bool isMine, int roundIndex, int alivePlayerCount, int sharedEndingCount)
        {
            myTurn = isMine;
            currentTap = 0;
            tapsThisTurn = 0;
            endedThisTurn = false;

            currentEndingCount = Mathf.Max(3, sharedEndingCount);

            // UI/비주얼 초기화
            if (starRenderer && starNormalMat) starRenderer.material = starNormalMat;
            SafeSetActive(turnBannerMine,  isMine);
            SafeSetActive(turnBannerOther, !isMine);
            if (unimoFace) unimoFace.Play("Idle", 0, 0);

            turnTimeMax = 10f;
            turnTimeLeft = turnTimeMax;
            if (turnTimerUI) turnTimerUI.value = 1f;

            SafeSetActive(countNumberText, isMine); // 숫자 카운트는 내 턴만 표시
            EnsureUIVisible();                      // 렌더/알파 잠김 대비

            if (isMine) StartCoroutine(CoCountWindow());
            else        countActive = false;

            Debug.Log($"[MeteorTapMiniGame] InitTurn → mine={isMine}, round={roundIndex}, alive={alivePlayerCount}, ending={currentEndingCount}");
        }

        private void Update()
        {
            if (turnTimeLeft > 0f)
            {
                turnTimeLeft -= Time.deltaTime;
                if (turnTimerUI) turnTimerUI.value = Mathf.Clamp01(turnTimeLeft / turnTimeMax);
            }
        }

        // UI 버튼(탭)
        public void OnTap()
        {
            if (!myTurn) return;
            if (!countActive) return;
            if (endedThisTurn) return;
            if (tapsThisTurn >= maxTapPerTurn) return;

            DoOneTapFXAndLogic();
            tapsThisTurn++;

            Debug.Log($"[MeteorTapMiniGame] 내 턴 탭 횟수: {tapsThisTurn}/{maxTapPerTurn}, 총 누적 탭: {currentTap}/{currentEndingCount}");
        }

        // 1회 탭 처리
        private void DoOneTapFXAndLogic()
        {
            currentTap++;

            if (starRenderer && starNormalMat) starRenderer.material = starNormalMat;
            if (sfxTap)     sfxTap.Play();
            if (vfxMeteor)  vfxMeteor.Play();
            if (unimoFace)  unimoFace.Play("Smile", 0, 0);

            float r = (float)currentTap / Mathf.Max(1, currentEndingCount);
            ApplyDanger(r);

            if (currentTap >= currentEndingCount)
                photonView.RPC(nameof(RPC_OnEndingReached), RpcTarget.All);
        }

        private void ApplyDanger(float r)
        {
            if (r > 0.66f)
            {
                if (starRenderer && starWarningMat) starRenderer.material = starWarningMat;
                if (sfxWarning && !sfxWarning.isPlaying) sfxWarning.Play();
                if (unimoFace) unimoFace.Play("Anxious", 0, 0);
            }
            else if (r > 0.33f)
            {
                if (unimoFace) unimoFace.Play("Worried", 0, 0);
            }
        }
        
        public void InitTurn(bool isMine, int roundIndex, int alivePlayerCount)
        {
            // ✅ 구버전 호출 호환용: 방/라운드/인원 기반 결정적 엔딩수 산출
            int sharedEndingCount = ComputeCompatEnding(roundIndex, alivePlayerCount);
            InitTurnWithEnding(isMine, roundIndex, alivePlayerCount, sharedEndingCount);
        }

        private int ComputeCompatEnding(int roundIndex, int alivePlayers)
        {
            // 라운드별 범위
            Vector2Int range = new Vector2Int(20, 30);
            if (roundIndex == 2) range = new Vector2Int(15, 25);
            else if (roundIndex >= 3) range = new Vector2Int(10, 20);

            // 🔒 결정적 시드(모든 클라 동일): 방 이름/플레이어 수/라운드
            int roomSeed = (PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name.GetHashCode() : 1234567);
            int seed = roomSeed ^ roundIndex ^ (PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0);
            var rng = new System.Random(seed);

            int ending = rng.Next(range.x, range.y + 1);
            ending -= Mathf.Max(0, 4 - alivePlayers) * 2; // 인원 보정(기획 반영)
            return Mathf.Max(3, ending);
        }

        // 내 턴 카운트(3→2→1→0) → 최소1회 보장 → (마스터) 다음 턴
        private System.Collections.IEnumerator CoCountWindow()
        {
            countActive = true;

            float t = countDuration;
            while (t > 0f)
            {
                if (countNumberText)
                {
                    int display = Mathf.CeilToInt(t);
                    countNumberText.text = display.ToString();
                }
                t -= Time.deltaTime;
                yield return null;
            }

            if (countNumberText)
            {
                countNumberText.text = "0";
                yield return null; // 0을 한 프레임 노출
                countNumberText.gameObject.SetActive(false);
            }

            countActive = false;

            // 최소 1회 보장
            if (!endedThisTurn && tapsThisTurn < minTapPerTurn)
            {
                DoOneTapFXAndLogic();
                tapsThisTurn = Mathf.Max(tapsThisTurn, 1);
            }

            // 마스터만 다음 턴 스케줄 (엔딩 미도달 시)
            if (PhotonNetwork.IsMasterClient)
            {
                yield return new WaitForSeconds(afterDelay);
                if (!endedThisTurn && currentTap < currentEndingCount && IsAuthoritative())
                {
                    TryNextTurnOrLocalFallback();
                }
            }
        }
        
        private bool IsAuthoritative()
        {
            // 온라인이면 마스터만, 오프라인/미연결이면 로컬 테스트 편의를 위해 권위자로 간주
            return (Photon.Pun.PhotonNetwork.IsConnectedAndReady)
                ? Photon.Pun.PhotonNetwork.IsMasterClient
                : true;
        }

// TurnManager가 없거나 비온라인이면 로컬로 다음 턴을 강제 진행
        private void TryNextTurnOrLocalFallback()
        {
            // 1) 온라인 & TurnManager 존재 → 정식 진행
            if (KYG.TurnManager.Instance != null && Photon.Pun.PhotonNetwork.IsConnectedAndReady)
            {
                KYG.TurnManager.Instance.NextTurn();
                return;
            }

            // 2) 로컬 폴백: LocalMiniGameBoot가 장착되어 있으면 그쪽으로 다음 턴을 회전
            var boot = UnityEngine.Object.FindObjectOfType<KYG.LocalMiniGameBoot>();
            if (boot != null)
            {
                boot.NextLocalTurn();
                return;
            }

            // 3) 폴백도 없으면 최소한 내/남 턴을 토글해서 재시작(2인 가정)
            //    필요 시 alivePlayers를 직렬화 필드로 받아 확장 가능
            bool nextIsMine = !myTurn;
            InitTurn(nextIsMine, 1, 2);
            UnityEngine.Debug.LogWarning("[MeteorTapMiniGame] No TurnManager/Boot found. Fallback toggled turn locally.");
        }
        
        public bool IsMyTurn => myTurn;

        [PunRPC]
        private void RPC_OnEndingReached()
        {
            if (endedThisTurn) return; // 중복 방지
            endedThisTurn = true;

            // 0) 별 색상 붉게 (경고 머티리얼)
            if (starRenderer && starWarningMat) starRenderer.material = starWarningMat;

            // 1) 유니모 표정: 초조 → 2) 느낌표 텍스트/사운드
            if (unimoFace) unimoFace.Play(animFaceAnxious, 0, 0);
            if (exclamationText) exclamationText.gameObject.SetActive(true);
            if (sfxExclamation)  sfxExclamation.Play();

            // 3) 별 오브젝트 위로 상승
            StartCoroutine(CoEndingSequence());

            if (IsAuthoritative())
            {
                TryNextTurnOrLocalFallback();
            }
        }
        
        private IEnumerator CoEndingSequence()
        {
            // 살짝 지연을 줘서 느낌표 노출
            yield return new WaitForSeconds(0.25f);

            // 별 상승
            if (starRoot != null)
            {
                Vector3 start = starRoot.position;
                Vector3 end   = start + Vector3.up * starRiseHeight;
                float t = 0f;
                while (t < starRiseDuration)
                {
                    t += Time.deltaTime;
                    starRoot.position = Vector3.Lerp(start, end, t / starRiseDuration);
                    yield return null;
                }
            }

            // 4) 폭발 이펙트 + 표정 "불안"
            if (unimoFace) unimoFace.Play(animFacePanic, 0, 0);
            if (vfxExplosion) vfxExplosion.Play();
            if (sfxMeteor) sfxMeteor.Play(); // 메테오/충돌 계열 사운드

            // 5) 탈락자 처리(마스터 권한)
            if (IsAuthoritative())
            {
                int actor = KYG.TurnManager.Instance != null
                    ? KYG.TurnManager.Instance.GetCurrentTurnActor()
                    : -1;

                // 승패/탈락 로직은 온라인 기준 설계이므로, 오프라인 테스트에선 단순 턴 회전만.
                if (KYG.ShootingGameManager.Instance != null && Photon.Pun.PhotonNetwork.IsConnectedAndReady)
                {
                    KYG.ShootingGameManager.Instance.Eliminate(actor);
                    if (!KYG.ShootingGameManager.Instance.IsGameOver())
                        TryNextTurnOrLocalFallback();
                    // 게임오버면 ShootingGameManager가 처리
                }
                else
                {
                    // 로컬이면 걍 다음 턴
                    TryNextTurnOrLocalFallback();
                }
            }
        }

        // ────────────────────── UI 보강 유틸 ──────────────────────
        private void EnsureUIVisible()
        {
            // CanvasGroup/알파로 잠긴 케이스 복구
            var cg = GetComponentInParent<CanvasGroup>();
            if (cg)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            if (turnBannerMine)  turnBannerMine.canvasRenderer.SetAlpha(1f);
            if (turnBannerOther) turnBannerOther.canvasRenderer.SetAlpha(1f);
            if (countNumberText) countNumberText.canvasRenderer.SetAlpha(1f);
            if (turnTimerUI)     turnTimerUI.gameObject.SetActive(true);
        }

        private void SafeSetActive(TMP_Text txt, bool on)
        {
            if (!txt) return;
            var go = txt.gameObject;
            if (!go.activeSelf && on)  go.SetActive(true);
            if (go.activeSelf && !on)  go.SetActive(false);
        }
    }
}
