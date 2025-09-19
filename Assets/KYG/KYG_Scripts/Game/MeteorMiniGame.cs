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
        [SerializeField] private AudioSource sfxTap;
        [SerializeField] private AudioSource sfxMeteor;
        [SerializeField] private AudioSource sfxWarning;
        [SerializeField] private ParticleSystem vfxMeteor;
        [SerializeField] private Animator unimoFace;
        [SerializeField] private TMP_Text  turnBannerMine;
        [SerializeField] private TMP_Text  turnBannerOther;
        [SerializeField] private Slider    turnTimerUI;
        
        [Header("Ending – Hidden BG Star")]
        [SerializeField] private GameObject bgHiddenStar; // 뒷배경에 숨겨둔 별(처음엔 비활성/알파0)
        
        [Header("Ending – Sky Meteor")]
        [SerializeField] private GameObject meteorPrefab;   // 운석(별똥별) 프리팹(트레일/파티클)
        [SerializeField] private Transform  meteorSpawnTop; // 화면 상단 스폰 기준
        [SerializeField] private Vector2    meteorSpawnX = new(-3.5f, 3.5f);
        [SerializeField] private Vector2    meteorEndXOffset = new(-1.0f, 1.0f);
        [SerializeField] private float      meteorFallDist = 6.5f;
        [SerializeField] private float      meteorFallTime = 0.6f; // 사선 낙하 시간
        
        [Header("Tap Flash")]
        [SerializeField] private Material starTapFlashMat;   // yellow1 (짧은 번쩍)
        [SerializeField] private float    starFlashSec = 0.07f;
        
        [Header("Tap FX")]
        [SerializeField] private AudioClip[] tapClips;      // shine1, shine2 등록
        [SerializeField] private float       tapPitchMin = 0.98f;
        [SerializeField] private float       tapPitchMax = 1.02f;

        [SerializeField] private float    shakeDuration = 0.12f; // 아주 짧게
        [SerializeField] private float    shakeAmplitude = 0.08f;// 좌우 진폭
        [SerializeField] private AnimationCurve shakeEase = AnimationCurve.EaseInOut(0,0, 1,1);

        [Header("Danger Gradation")]
        [SerializeField] private Renderer starRenderer;     // MainStar MeshRenderer
        [SerializeField] private Material starNormalMat;    // Yellow
        [SerializeField] private Material starOrangeMat;    // Orange
        [SerializeField] private Material starWarningMat;   // Red
        
        [SerializeField] private AudioClip changeClip;
        
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
        
        [SerializeField] private float bannerShowTime = 0.9f;   // 배너 노출 시간(짧게)
        [SerializeField] private float bannerFadeOut  = 0.2f;   // 페이드아웃 시간

        // 내부 상태
        private int  currentEndingCount;
        private int  currentTap;
        private bool myTurn;
        private float turnTimeLeft;
        private float turnTimeMax = 10f;

        private bool countActive  = false;
        private int  tapsThisTurn = 0;
        private bool endedThisTurn = false;
        
        private AudioSource _changeSrc;
        private int _lastDangerTier = -1; // 0=yellow, 1=orange, 2=red (변경 시에만 사운드)
        
        private bool _isFlashing; // 플래시 중인지 가드
        
        

        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// TurnManager.RPC_SetCurrentTurn → EnsureMiniAndInit 에서 호출.
        /// 모든 클라 동일 엔딩카운트(sharedEndingCount)를 사용.
        /// </summary>
        public void InitTurnWithEnding(bool isMine, int roundIndex, int alivePlayerCount, int sharedEndingCount)
        {
            Debug.Log($"[MTM] InitTurnWithEnding start mine={isMine}  " +
                      $"mineTxt={(turnBannerMine? turnBannerMine.name : "null")}#{(turnBannerMine? turnBannerMine.GetInstanceID():0)} " +
                      $"otherTxt={(turnBannerOther? turnBannerOther.name : "null")}#{(turnBannerOther? turnBannerOther.GetInstanceID():0)} " +
                      $"instances={FindObjectsOfType<MeteorTapMiniGame>(true).Length}");
            
            // 로컬 오프라인 테스트면 항상 내 턴으로 강제
            if (!Photon.Pun.PhotonNetwork.IsConnected)
                isMine = true;
            
            myTurn = isMine;
            //currentTap = 0;
            tapsThisTurn = 0;
            endedThisTurn = false;

            currentEndingCount = Mathf.Max(3, sharedEndingCount);

            // UI/비주얼 초기화
            if (starRenderer && starNormalMat) starRenderer.material = starNormalMat;
            //SafeSetActive(turnBannerMine,  isMine);
            //SafeSetActive(turnBannerOther, !isMine);
            if (unimoFace) unimoFace.Play("Idle", 0, 0);

            turnTimeMax = 10f;
            turnTimeLeft = turnTimeMax;
            if (turnTimerUI) turnTimerUI.value = 1f;

            SafeSetActive(countNumberText, isMine); // 숫자 카운트는 내 턴만 표시
            EnsureUIVisible();                      // 렌더/알파 잠김 대비
            
            // InitTurnWithEnding 내부 UI/비주얼 초기화 부분 교체
            // (둘 다 끄고 → 하나만 켜서 '동시 ON'을 원천 차단)
            if (turnBannerMine)  turnBannerMine.gameObject.SetActive(false);
            if (turnBannerOther) turnBannerOther.gameObject.SetActive(false);

            if (isMine) {
                if (turnBannerMine)  turnBannerMine.gameObject.SetActive(true);   // 내 턴만 보임
            } else {
                if (turnBannerOther) turnBannerOther.gameObject.SetActive(true);  // 상대 턴 전환은 모두에게
            }
            
            // 잘못된 참조(같은 오브젝트를 두 슬롯에 꽂은 경우) 탐지
            if (turnBannerMine && turnBannerOther && ReferenceEquals(turnBannerMine, turnBannerOther)) {
                Debug.LogError("[MeteorTapMiniGame] turnBannerMine과 turnBannerOther가 같은 오브젝트를 참조하고 있습니다!");
            }

            // 씬에 복수 인스턴스 가드(두 컴포넌트가 서로 켜는 상황 방지)
            if (FindObjectsOfType<MeteorTapMiniGame>(true).Length > 1) {
                Debug.LogWarning("[MeteorTapMiniGame] 씬에 MeteorTapMiniGame이 2개 이상 존재합니다. 배너 중복 노출 원인일 수 있습니다.");
            }
            
            // 배너는 "짧게" 보여주고 자동 숨김
            // 모든 플레이어: turnBannerOther = 상대 턴 전환 알림 (NEXT >>)
            // 내 턴인 플레이어만: turnBannerMine = 내 턴 알림 (MY TURN!)
            PlayTurnTransitionBanners(isMine);

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

        private void DoOneTapFXAndLogic()
        {
            currentTap++;

            // (A) 색상 순간 전환 (yellow → yellow1 → 후처리에서 단계색)
            StartCoroutine(CoStarFlash());  // ★ 추가 1

          
            // (B) 탭 사운드 랜덤(shine1/shine2)
            PlayRandomTapSfx();              // ★ 추가 2

            // 기존: vfx/유니모 표정
            if (sfxTap)     sfxTap.Play();
            if (vfxMeteor)  vfxMeteor.Play();
            if (unimoFace)  unimoFace.Play("Smile", 0, 0);
            
            float r = (float)currentTap / Mathf.Max(1, currentEndingCount);
            
            if (!_isFlashing)
                StartCoroutine(CoStarFlashThenApplyDanger(r));
            else
                ApplyDanger(r); // 중복 플래시 중이면 안전하게 스킵

            // (C) 중앙 별 쉐이크
            StartCoroutine(CoShakeStar());   // ★ 추가 3

            // (D) 하늘 별똥별 대각 낙하
            SpawnSkyMeteor();                // ★ 추가 4

            // 엔딩 도달 시 동기 종료
            if (currentTap >= currentEndingCount)
                photonView.RPC(nameof(RPC_OnEndingReached), RpcTarget.All);
        }
        
        private IEnumerator CoStarFlashThenApplyDanger(float ratioAfterTap)
        {
            if (!starRenderer || !starTapFlashMat)
            {
                // 플래시 자원 누락 시 그냥 위험도만 반영
                ApplyDanger(ratioAfterTap);
                yield break;
            }

            _isFlashing = true;

            // 1) yellow1로 '번쩍'
            var prev = starRenderer.material; // 개별 인스턴스화(material) 사용
            starRenderer.material = starTapFlashMat;

            yield return new WaitForSeconds(starFlashSec);

            // 2) 위험도 단계 색상으로 정착
            //    - 여기서 바로 prev로 롤백하지 않고, 기획대로 현재 위험도 색상(노말/경고)로 세팅
            ApplyDanger(ratioAfterTap);

            _isFlashing = false;
        }

        private void ApplyDanger(float r)
        {
            int tier = (r > 0.66f) ? 2 : ((r > 0.33f) ? 1 : 0);

            // 색상 전환
            if (starRenderer)
            {
                if (tier == 0 && starNormalMat)      starRenderer.material = starNormalMat;   // yellow
                else if (tier == 1 && starOrangeMat) starRenderer.material = starOrangeMat;   // orange
                else if (tier == 2 && starWarningMat)starRenderer.material = starWarningMat;  // red
            }

            // 단계가 바뀔 때만 change.wav 재생
            if (tier != _lastDangerTier)
            {
                _lastDangerTier = tier;
                if (changeClip)
                {
                    if (_changeSrc == null)
                    {
                        _changeSrc = gameObject.AddComponent<AudioSource>();
                        _changeSrc.playOnAwake = false;
                        _changeSrc.spatialBlend = 0f; // 2D
                    }
                    _changeSrc.PlayOneShot(changeClip);
                }
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
            if (IsAuthoritative()) // 온라인=마스터, 오프라인=항상 true
            {
                yield return new WaitForSeconds(afterDelay);
                if (!endedThisTurn && currentTap < currentEndingCount)
                {
                    TryNextTurnOrLocalFallback(); // ← 내부에서 LocalMiniGameBoot.NextLocalTurn() 호출
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

            StartCoroutine(CoEndingSequence_Ordered());
        }
        
        private IEnumerator CoEndingSequence_Ordered()
        {
            // 4) 별 상승(천천히)
            yield return StartCoroutine(CoStarRise(starRiseHeight, starRiseDuration,
                onHalf: () =>
                {
                    // 5) 중간 시점에 배경별 드러내기
                    if (bgHiddenStar != null) StartCoroutine(CoRevealBGStar(bgHiddenStar, 0.35f));
                }));

            // 6) 운석이 사선으로 떨어진다 (+ 낙하음이 있다면 여기서 재생)
            GameObject meteor = SpawnSkyMeteor();
            if (sfxMeteor) sfxMeteor.Play();              // 낙하/충돌계열 사운드
            yield return new WaitForSeconds(meteorFallTime);

            // 7) (보류) 불안 표정
            if (unimoFace) unimoFace.Play(animFacePanic, 0, 0);

            // 8) 폭발 VFX
            if (vfxExplosion) vfxExplosion.Play();

            // 운석 제거
            if (meteor) Destroy(meteor);

            // 탈락 처리 및 다음 턴/다음 라운드 흐름
            HandleEliminationAndAdvance();
        }
        
        private IEnumerator CoStarRise(float height, float duration, System.Action onHalf = null)
{
    if (starRoot == null || duration <= 0f) yield break;
    Vector3 s = starRoot.position;
    Vector3 e = s + Vector3.up * height;
    float t = 0f;
    bool halfCalled = false;

    while (t < duration)
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duration);
        starRoot.position = Vector3.Lerp(s, e, k);
        if (!halfCalled && k >= 0.5f)
        {
            halfCalled = true;
            onHalf?.Invoke();
        }
        yield return null;
    }
    starRoot.position = e;
}

private IEnumerator CoRevealBGStar(GameObject go, float fadeSec)
{
    if (!go) yield break;
    // CanvasGroup 있으면 페이드, 아니면 단순 활성
    var cg = go.GetComponent<CanvasGroup>();
    if (cg != null)
    {
        if (!go.activeSelf) go.SetActive(true);
        cg.alpha = 0f;
        float t = 0f;
        while (t < fadeSec)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, t / fadeSec);
            yield return null;
        }
        cg.alpha = 1f;
    }
    else
    {
        go.SetActive(true);
    }
}

private GameObject SpawnSkyMeteor()
{
    if (meteorPrefab == null || meteorSpawnTop == null) return null;

    float sx = Random.Range(meteorSpawnX.x, meteorSpawnX.y);
    float ex = sx + Random.Range(meteorEndXOffset.x, meteorEndXOffset.y);

    Vector3 start = meteorSpawnTop.position + new Vector3(sx, 0f, 0f);
    Vector3 end   = start + new Vector3(ex - sx, -meteorFallDist, 0f);

    var go = Instantiate(meteorPrefab, start, Quaternion.identity);
    StartCoroutine(CoMeteorFall(go.transform, start, end, meteorFallTime));
    return go;
}

private IEnumerator CoMeteorFall(Transform tf, Vector3 s, Vector3 e, float sec)
{
    if (!tf || sec <= 0f) yield break;
    float t = 0f;
    while (t < sec)
    {
        t += Time.deltaTime;
        tf.position = Vector3.Lerp(s, e, t / sec);
        yield return null;
    }
    tf.position = e;
}

private void HandleEliminationAndAdvance()
{
    // 현재 턴의 플레이어 탈락 처리(온라인=마스터 / 오프라인=로컬 폴백)
    if (IsAuthoritative())
    {
        int actor = (KYG.TurnManager.Instance != null)
            ? KYG.TurnManager.Instance.GetCurrentTurnActor()
            : -1;

        // 온라인: 매니저에 위임
        if (KYG.ShootingGameManager.Instance != null && Photon.Pun.PhotonNetwork.IsConnectedAndReady)
        {
            KYG.ShootingGameManager.Instance.Eliminate(actor);
            if (!KYG.ShootingGameManager.Instance.IsGameOver())
                TryNextTurnOrLocalFallback(); // 다음 턴
            // 게임오버면 ShootingGameManager가 마무리
        }
        else
        {
            // 로컬 1인 테스트: 다음 턴 순환(카드 순서 기반)
            TryNextTurnOrLocalFallback();
        }
    }
}
        
        // 2-1) 별 색상 순간 전환: normal ↔ flash
        private IEnumerator CoStarFlash()
        {
            if (starRenderer == null || starNormalMat == null || starTapFlashMat == null)
            yield break;

            var before = starRenderer.material;
            starRenderer.material = starTapFlashMat;
            yield return new WaitForSeconds(starFlashSec);
            // 위험도 단계(ApplyDanger)에서 warningMat로 바뀔 수 있으므로,
            // 여기서는 '기본'으로 되돌리되, 위에서 곧 ApplyDanger가 후처리
            starRenderer.material = starNormalMat;
        }

        // 2-2) 탭 사운드 랜덤
        private void PlayRandomTapSfx()
        {
            if (sfxTap == null || tapClips == null || tapClips.Length == 0) return;
            var clip = tapClips[Random.Range(0, tapClips.Length)];
            sfxTap.pitch = Random.Range(tapPitchMin, tapPitchMax);
            sfxTap.PlayOneShot(clip);
        }

        // 2-3) 중앙 별 좌우 흔들림(짧게)
        private IEnumerator CoShakeStar()
        {
            if (starRoot == null) yield break;
            Vector3 origin = starRoot.localPosition;
            float t = 0f;
        
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                float n = shakeEase.Evaluate(Mathf.Clamp01(t / shakeDuration)); // 0→1
                // 한 번 왼→오른쪽으로 스윙하는 느낌
                float phase = Mathf.Sin(n * Mathf.PI); // 0→π (한 사이클)
                float dx = phase * shakeAmplitude;
                starRoot.localPosition = origin + new Vector3(dx, 0f, 0f);
                yield return null;
            }
            starRoot.localPosition = origin;
        }

        /* 2-4) 하늘 별똥별 낙하(대각선)
        private void SpawnSkyMeteor()
        {
            if (meteorPrefab == null || meteorSpawnTop == null) return;
        
            float sx = Random.Range(meteorSpawnX.x, meteorSpawnX.y);
            float ex = sx + Random.Range(meteorEndXOffset.x, meteorEndXOffset.y);
        
            Vector3 start = meteorSpawnTop.position + new Vector3(sx, 0f, 0f);
            Vector3 end   = start + new Vector3(ex - sx, -meteorFallDist, 0f);
            StartCoroutine(CoMeteorFall(start, end, meteorFallTime));
        }*/

private IEnumerator CoMeteorFall(Vector3 start, Vector3 end, float dur)
{
    var go = Instantiate(meteorPrefab, start, Quaternion.identity);
    float t = 0f;
    while (t < dur)
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / dur);
        go.transform.position = Vector3.Lerp(start, end, k);
        yield return null;
    }
    Destroy(go);
}
        
        // (유틸) 텍스트를 짧게 보여주고 자동으로 숨김
        private IEnumerator CoFlashText(TMP_Text txt, float showSec, float fadeSec)
        {
            if (!txt) yield break;
            var go = txt.gameObject;

            // 보장: 보이도록
            if (!go.activeSelf) go.SetActive(true);
            txt.canvasRenderer.SetAlpha(1f);

            // 짧게 유지
            yield return new WaitForSeconds(showSec);

            // 페이드아웃
            if (fadeSec > 0f)
                txt.CrossFadeAlpha(0f, fadeSec, ignoreTimeScale: true);
            else
                txt.canvasRenderer.SetAlpha(0f);

            yield return new WaitForSeconds(Mathf.Max(0.01f, fadeSec));

            // 완전히 꺼두기
            go.SetActive(false);
        }

        // (추가) 턴 전환 시 배너 연출 총괄
        private void PlayTurnTransitionBanners(bool isMine)
        {
            // 1) 모두에게 "상대 턴으로 넘어갑니다(NEXT >>)" 배너를 짧게 노출
            //    turnBannerOther 를 공용 전환 배너로 사용
            if (turnBannerOther)
                StartCoroutine(CoFlashText(turnBannerOther, bannerShowTime, bannerFadeOut));

            // 2) 내 턴이면 "MY TURN!"(turnBannerMine)도 바로 이어서 짧게 노출
            if (isMine && turnBannerMine)
                StartCoroutine(CoFlashMyTurnAfterDelay(0.05f)); // 살짝 텀을 두고 노출
        }

        private IEnumerator CoFlashMyTurnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            yield return CoFlashText(turnBannerMine, bannerShowTime, bannerFadeOut);
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
