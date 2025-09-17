using System.Collections;
using UnityEngine;
using DesignPattern;
using Photon.Pun;
using DesignPattern;

namespace KYG
{
    /// <summary>
    /// 턴 진행의 단일 권위(마스터).
    /// - 카드 공개 후 첫 턴 시작
    /// - 현재 턴/라운드 브로드캐스트 (+ 이번 라운드 엔딩카운트 동기화)
    /// - 다음 턴 진행(씬 의존 오브젝트는 가드)
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        [Header("Rounds")]
        [SerializeField] private int totalRounds = 1; // 추후 확장 가능

        // 내부 상태(1-based)
        private int currentTurnIndex = 0;
        private int currentRound     = 1;

        // 전파 지연 대비(내 turnIndex 미전파 시 보류)
        private (int turn, int round, int ending)? _pendingTurn;

        protected override void OnAwake()
        {
            isPersistent = false; // 씬 생명주기 따름
        }

        public void Initialize()
        {
            Debug.Log("[TurnManager] Initialize");
        }

        /// <summary>마스터만 호출. 첫 라운드/턴 초기화.</summary>
        public void SetupTurn()
        {
            Debug.Log("[TurnManager] SetupTurn 호출됨");
            if (!PhotonNetwork.IsMasterClient) return;

            currentTurnIndex = 0;
            currentRound     = 1;

            // RoomPropertyObserver가 비활성일 수 있으므로 가드
            var obs = RoomPropertyObserver.Instance;
            if (obs != null)
                obs.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
            else
                Debug.LogWarning("[TurnManager] RoomPropertyObserver not ready. Skip setting state.");
        }

        /// <summary>마스터만 첫 턴 시작.</summary>
        public void StartFirstTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            currentTurnIndex = 1; // 1번부터
            BroadcastCurrentTurn();
        }

        /// <summary>마스터만 다음 턴으로.</summary>
        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // (슈팅씬 잔존 의존성 가드)
            if (EggManager.Instance != null && EggManager.Instance.photonView != null)
                EggManager.Instance.photonView.RPC("ClearCurrentEgg", RpcTarget.All);
            else
                Debug.LogWarning("[TurnManager] EggManager not found in this scene. Skip clearing egg.");

            int tries = PhotonNetwork.CurrentRoom.PlayerCount + 2; // 안전 가드
            do
            {
                currentTurnIndex++;
                if (currentTurnIndex > PhotonNetwork.CurrentRoom.PlayerCount)
                {
                    currentTurnIndex = 1;
                    currentRound++;
                }
                tries--;
            }
            while (tries > 0 && IsTurnIndexEliminated(currentTurnIndex));

            BroadcastCurrentTurn();
        }
        
        private bool IsTurnIndexEliminated(int turnIndex1Based)
        {
            // turnIndex == X 인 Actor 찾기 → 탈락 여부 확인
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.CustomProperties != null &&
                    p.CustomProperties.TryGetValue("turnIndex", out var v) &&
                    v is int idx && idx == turnIndex1Based)
                {
                    return KYG.ShootingGameManager.Instance != null &&
                           KYG.ShootingGameManager.Instance.IsEliminated(p.ActorNumber);
                }
            }
            return false;
        }
        
        public int GetCurrentTurnActor()
        {
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.CustomProperties != null &&
                    p.CustomProperties.TryGetValue("turnIndex", out var v) &&
                    v is int idx && idx == currentTurnIndex)
                {
                    // 탈락자는 제외
                    if (KYG.ShootingGameManager.Instance != null &&
                        KYG.ShootingGameManager.Instance.IsEliminated(p.ActorNumber))
                        continue;
                    return p.ActorNumber;
                }
            }
            return -1;
        }

        /// <summary>현재 턴/라운드 + 이번 라운드 엔딩카운트를 모든 클라에 브로드캐스트.</summary>
        public void BroadcastCurrentTurn()
        {
            int alive = KYG.ShootingGameManager.Instance != null
                ? KYG.ShootingGameManager.Instance.ActiveCount
                : PhotonNetwork.CurrentRoom.PlayerCount;

            int ending = ComputeEndingCount(currentRound, alive); // ★ 생존자 수 반영
            photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All,
                currentTurnIndex, currentRound, ending);
        }

        // 라운드/인원에 따른 엔딩카운트 산출(미니게임 규칙과 동일)
        private int ComputeEndingCount(int roundIndex, int alivePlayers)
        {
            Vector2Int range = new Vector2Int(20, 30);
            if (roundIndex == 2) range = new Vector2Int(15, 25);
            else if (roundIndex >= 3) range = new Vector2Int(10, 20);

            int ending = Random.Range(range.x, range.y + 1);
            ending -= Mathf.Max(0, 4 - alivePlayers) * 2; // 4인 기준 보정
            return Mathf.Max(3, ending);
        }

        /// <summary>
        /// 현재 턴 통지 수신 → 내 턴 판정 → 미니게임 활성 보장 후 Init 호출
        /// (turnIndex 전파 지연/미니게임 비활성 레이스 모두 방지)
        /// </summary>
        [PunRPC]
        private void RPC_SetCurrentTurn(int turnIndex, int roundIndex, int sharedEndingCount)
        {
            // 1) 내 turnIndex가 아직 미전파면 재시도 예약
            int myTurnIdx = -1;
            if (PhotonNetwork.LocalPlayer.CustomProperties != null &&
                PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("turnIndex", out var v))
                myTurnIdx = v is int i ? i : -1;

            if (myTurnIdx == -1)
            {
                _pendingTurn = (turnIndex, roundIndex, sharedEndingCount);
                StartCoroutine(RetryWhenTurnIndexReady());
                Debug.LogWarning("[TurnManager] turnIndex가 아직 없음 → 잠시 후 재시도");
                return;
            }

            bool isMyTurn = (turnIndex == myTurnIdx);
            Debug.Log($"[TurnManager] 현재 라운드={roundIndex}, 턴={turnIndex}, 내턴?={isMyTurn}, ending={sharedEndingCount}");

            // 2) 미니게임 활성 보장 후 Init
            StartCoroutine(EnsureMiniAndInit(isMyTurn, roundIndex, sharedEndingCount));
        }

        private IEnumerator RetryWhenTurnIndexReady()
        {
            float t = 0f;
            while (t < 1.0f) // 최대 1초
            {
                if (PhotonNetwork.LocalPlayer.CustomProperties != null &&
                    PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("turnIndex"))
                {
                    if (_pendingTurn.HasValue)
                    {
                        var p = _pendingTurn.Value;
                        _pendingTurn = null;
                        RPC_SetCurrentTurn(p.turn, p.round, p.ending);
                    }
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }
            Debug.LogWarning("[TurnManager] turnIndex 재시도 타임아웃");
        }

        private IEnumerator EnsureMiniAndInit(bool isMyTurn, int roundIndex, int sharedEndingCount)
        {
            // (선택) 코디네이션 완료까지 대기 → roots 활성 보장
            while (LDH_MainGame.PhotonViewSync.Instance != null &&
                   !LDH_MainGame.PhotonViewSync.Instance.SyncCompleted)
                yield return null;

            // 미니게임 오브젝트가 "활성"될 때까지 보장
            KYG.MeteorTapMiniGame mini = null;
            while ((mini = UnityEngine.Object.FindObjectOfType<KYG.MeteorTapMiniGame>(true)) == null ||
                   !mini.gameObject.activeInHierarchy)
                yield return null;

            int aliveCount = KYG.ShootingGameManager.Instance != null
                ? KYG.ShootingGameManager.Instance.ActiveCount
                : PhotonNetwork.CurrentRoom.PlayerCount;
            mini.InitTurnWithEnding(isMyTurn, roundIndex, aliveCount, sharedEndingCount);
        }

        // (옵션) 일정 시간 후 자동 턴 전환
        public void StartTurnCorutine(float delay)
        {
            StartCoroutine(TurnChangeDelay(delay));
        }

        private IEnumerator TurnChangeDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            NextTurn();
        }

        public void EndTurn() { /* 필요 시 구현 */ }
    }
}
