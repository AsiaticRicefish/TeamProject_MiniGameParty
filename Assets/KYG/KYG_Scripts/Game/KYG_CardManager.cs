using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using DesignPattern;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Random = System.Random;

namespace KYG
{
    /// <summary>
    /// 카드 선택 로비 전체 제어(Master 권위).
    /// - 인원 수 만큼 카드(1..N) 생성/셔플
    /// - 중복 선택 불가(로컬 디바운스 + 서버 보장)
    /// - 전원 선택 시 네트워크 시간 동기 공개 → 오름차순으로 turnOrder 계산
    /// - 공개 연출 종료 후 UI 닫고 턴 순서 통지 → TurnManager 시작(코디/미니게임 활성 대기)
    /// </summary>
    public class CardManager : PunSingleton<CardManager>
    {
        [Header("Prefabs & Layout")]
        [SerializeField] private Transform  cardParent;   // 카드를 놓을 Grid/HorizontalLayout
        [SerializeField] private GameObject cardUICanvas; // 전체 카드 선택 UI 루트
        [SerializeField] private KYG.CardUI cardPrefab;

        private const string KEY_DECK_VALUES = "deckValues";
        private const string KEY_CARD_OWNERS = "cardOwners";
        private const string KEY_STATE       = "state";
        private const string KEY_TURN_ORDER  = "turnOrder";
        private const string KEY_REVEAL_T0   = "revealT0";
        private const string KEY_REVEAL_SEC  = "revealSec";

        private enum LobbyState : byte { Picking = 0, Revealing = 1, Done = 2 }

        // 로컬 캐시
        private readonly List<KYG.CardUI> _cards = new();
        private int[] _deckValues;     // 섞인 숫자들 (1..N)
        private int[] _owners;         // 각 index의 소유자 ActorNumber, 미선택 -1

        // 진행 상태
        private bool _requestPick = false;    // 연타 방지(요청 중)
        private bool _alreadyPicked = false;  // 이미 하나 선택 완료

        // 공개 타이밍(네트워크 시간 기준)
        private float  _revealSec = -1f;
        private double _t0 = -1;

        // 공개 코루틴 핸들(중복 방지용)
        private Coroutine _revealCo;

        private void Start()
        {
            StartCoroutine(InitRoutine());
        }

        private IEnumerator InitRoutine()
        {
            yield return new WaitUntil(() => PhotonNetwork.InRoom);

            if (cardParent != null)
                yield return new WaitUntil(() => cardParent.gameObject.activeInHierarchy);

            if (PhotonNetwork.IsMasterClient)
            {
                BuildAndBroadcastDeck();
            }
            else
            {
                // 비마스터: 프로퍼티가 생길 때까지 대기 후 초기화
                yield return new WaitUntil(() =>
                    PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(KEY_DECK_VALUES) &&
                    PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(KEY_CARD_OWNERS)
                );
                TryInitFromRoomProps();
            }

            Debug.Log($"[CardManager] Ready. Cards={_cards?.Count}, Deck={_deckValues?.Length}");
        }

        #region Deck Build & Sync
        private void BuildAndBroadcastDeck()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int n = Mathf.Clamp(PhotonNetwork.CurrentRoom.PlayerCount, 2, 4);
            _deckValues = Enumerable.Range(1, n).ToArray();

            // 안정적 재현을 위한 랜덤 시드(서버 타임 포함)
            int seed = Guid.NewGuid().GetHashCode() ^ PhotonNetwork.ServerTimestamp;
            ShuffleInPlace(_deckValues, new Random(seed));
            _owners = Enumerable.Repeat(-1, n).ToArray();

            var props = new Hashtable {
                { KEY_DECK_VALUES, _deckValues },
                { KEY_CARD_OWNERS, _owners     },
                { KEY_STATE, (byte)LobbyState.Picking }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);

            BuildCardUIs();
            RefreshInteractables();

            Debug.Log($"[CardManager] DECK n={n} values=[{string.Join(",", _deckValues)}] owners=[{string.Join(",", _owners)}] state=Picking seed={seed}");
        }

        private void TryInitFromRoomProps()
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room == null || room.CustomProperties == null) return;

            if (room.CustomProperties.TryGetValue(KEY_DECK_VALUES, out var dvObj) &&
                room.CustomProperties.TryGetValue(KEY_CARD_OWNERS, out var ownObj))
            {
                _deckValues = ToIntArray(dvObj);
                _owners     = ToIntArray(ownObj);
                if (_deckValues == null || _owners == null)
                {
                    Debug.LogError("[CardManager] Room props cast failed.");
                    return;
                }
                BuildCardUIs();
                RefreshInteractables();
            }
        }

        private static int[] ToIntArray(object obj)
        {
            if (obj is int[] ia) return ia;
            if (obj is object[] oa) return oa.Select(o => Convert.ToInt32(o)).ToArray();
            return null;
        }

        private void ShuffleInPlace<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        #endregion

        #region UI Build
        private void BuildCardUIs()
        {
            if (cardPrefab == null) { Debug.LogError("[CardManager] cardPrefab is null."); return; }
            if (cardParent == null) { Debug.LogError("[CardManager] cardParent is null."); return; }

            // 기존 제거
            foreach (Transform t in cardParent) Destroy(t.gameObject);
            _cards.Clear();

            for (int i = 0; i < _deckValues.Length; i++)
            {
                var card = Instantiate(cardPrefab, cardParent);
                int idx = i;
                card.Setup(idx, _deckValues[idx], (ci) => TryPick(ci)); // 클릭 콜백
                _cards.Add(card);
            }
        }

        private void RefreshInteractables()
        {
            int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
            bool iAlreadyPicked = (_owners != null) && Array.IndexOf(_owners, myActor) != -1;

            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;

                int owner = (_owners != null && i < _owners.Length) ? _owners[i] : -1;
                bool free  = owner == -1;
                bool isMine = owner == myActor;

                if (free)
                    _cards[i].SetInteractable(!iAlreadyPicked, false);   // 아직 선택 안된 카드 → 내가 이미 하나 집었으면 잠시 비활성
                else
                {
                    _cards[i].SetInteractable(false, isMine);
                    _cards[i].SetSelected(isMine);
                }
            }
        }
        #endregion

        #region Picking (로컬 디바운스 + 서버 보장)
        private void TryPick(int cardIndex)
        {
            if (!PhotonNetwork.InRoom) return;

            // 카드/UI가 아직 준비 안된 케이스 방지
            if (_cards == null || _cards.Count == 0 || _deckValues == null || _deckValues.Length == 0) return;
            if (cardIndex < 0 || cardIndex >= _cards.Count) return;
            if (_requestPick || _alreadyPicked) return;

            _requestPick = true;
            LockAllExcept(cardIndex);

            photonView.RPC(nameof(RPC_TryPick), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber, cardIndex);
        }

        private void LockAllExcept(int keepIndex)
        {
            if (_cards == null) return;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                if (i == keepIndex) continue;
                _cards[i].SetInteractable(false, false);
            }
        }

        private void UnlockAllFreeCards()
        {
            int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                bool free = (_owners != null && i < _owners.Length) ? _owners[i] == -1 : true;
                bool mine = (_owners != null && i < _owners.Length) ? _owners[i] == myActor : false;
                _cards[i].SetInteractable(free, mine);
            }
        }

        [PunRPC]
        private void RPC_TryPick(int actorNumber, int cardIndex, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 상태 확인
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KEY_STATE, out var stObj)) return;
            if ((byte)stObj != (byte)LobbyState.Picking) return;

            if (_owners == null || _deckValues == null) return;
            if (cardIndex < 0 || cardIndex >= _owners.Length) return;

            // 이미 선택된 카드?
            if (_owners[cardIndex] != -1)
            {
                photonView.RPC(nameof(RPC_PickRejected), info.Sender, cardIndex, 1);
                return;
            }

            // 이미 이 배우가 다른 카드를 갖고 있으면 거절(서버 측 보장)
            if (Array.IndexOf(_owners, actorNumber) != -1)
            {
                photonView.RPC(nameof(RPC_PickRejected), info.Sender, cardIndex, 2);
                return;
            }

            // 선택 확정
            _owners[cardIndex] = actorNumber;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
                { KEY_CARD_OWNERS, _owners }
            });

            // 진행 상황 브로드캐스트
            photonView.RPC(nameof(RPC_OnPickUpdated), RpcTarget.AllBuffered, _owners);
            Debug.Log($"[CardManager] Master Pick ok. owners=[{string.Join(",", _owners)}]");

            // 모두 선택?
            int selectedCount = _owners.Count(o => o != -1);
            int needCount     = PhotonNetwork.CurrentRoom.PlayerCount;

            if (selectedCount >= needCount)
            {
                // 1) 상태 전환(한 번만)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
                    { KEY_STATE, (byte)LobbyState.Revealing }
                });

                // 2) 네트워크 기준 공개 예약
                _t0        = PhotonNetwork.Time + 0.30f;
                _revealSec = 1f * PhotonNetwork.CurrentRoom.PlayerCount;

                // 3) 방 속성에도 기록 (RPC 유실 복구용)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
                    { KEY_REVEAL_T0, _t0 },
                    { KEY_REVEAL_SEC, _revealSec }
                });

                // 4) 턴 순서 미리 계산
                var order = Enumerable.Range(0, _deckValues.Length)
                    .Select(i => (_deckValues[i], _owners[i]))
                    .OrderBy(t => t.Item1)      // 낮은 숫자가 먼저
                    .Select(t => t.Item2)       // ActorNumber
                    .ToArray();

                // 5) 공개 시작(정확히 1회)
                photonView.RPC(nameof(RPC_RevealAll), RpcTarget.AllBuffered, _deckValues, _owners, _t0, _revealSec);

                // 6) 공개 끝나면 턴 통지(마스터 단독)
                StartCoroutine(CoNotifyTurnOrderAfterReveal(_t0, _revealSec, order));
            }
        }

        [PunRPC]
        private void RPC_PickRejected(int cardIndex, int reason)
        {
            _requestPick = false;
            _alreadyPicked = false;

            UnlockAllFreeCards();
            if (cardIndex >= 0 && cardIndex < _cards.Count && _cards[cardIndex] != null)
                _cards[cardIndex].SetInteractable(true, false);
        }

        [PunRPC]
        private void RPC_OnPickUpdated(int[] ownersFromMaster)
        {
            _owners = ownersFromMaster;
            _alreadyPicked = Array.IndexOf(_owners, PhotonNetwork.LocalPlayer.ActorNumber) != -1;
            _requestPick = false; // 요청 해제

            // ✔️ 선택 가능 카드들 다시 활성화
            RefreshInteractables();

            int selected = _owners.Count(o => o != -1);
            int need = PhotonNetwork.CurrentRoom.PlayerCount;
            Debug.Log($"[CardManager] Progress: {selected}/{need} picked");
        }
        #endregion

        #region Reveal & Turn Order
        [PunRPC]
        private void RPC_RevealAll(int[] deckValues, int[] ownersFromMaster, double t0, float revealSec)
        {
            Debug.Log("[CardManager] <<< RPC_RevealAll recv (synced)");
            _deckValues = deckValues;
            _owners     = ownersFromMaster;
            _t0         = t0;
            _revealSec  = revealSec;
            _alreadyPicked = true;

            if (_revealCo != null) StopCoroutine(_revealCo);
            _revealCo = StartCoroutine(CoSyncedReveal());
        }

        private IEnumerator CoSyncedReveal()
        {
            while (PhotonNetwork.Time < _t0) yield return null;

            for (int i = 0; i < _cards.Count; i++)
                _cards[i]?.RevealFace();

            RefreshInteractables();
            _revealCo = null;
        }

        private IEnumerator CoNotifyTurnOrderAfterReveal(double t0, float sec, int[] order)
        {
            if (!PhotonNetwork.IsMasterClient) yield break;

            while (PhotonNetwork.Time < t0 + sec) yield return null;

            photonView.RPC(nameof(RPC_CloseCardUI), RpcTarget.All);
            yield return null;

            photonView.RPC(nameof(RPC_OnTurnOrderReady), RpcTarget.AllBuffered, order);
            Debug.Log($"[CardManager] >>> RPC_OnTurnOrderReady sent order=[{string.Join(",", order)}]");
        }

        [PunRPC]
        private void RPC_CloseCardUI()
        {
            // 카드 UI 비활성
            if (_cards != null)
            {
                foreach (var c in _cards) if (c) c.gameObject.SetActive(false);
            }
            if (cardParent)  cardParent.gameObject.SetActive(false);
            if (cardUICanvas) cardUICanvas.SetActive(false);
        }

        [PunRPC]
        private void RPC_OnTurnOrderReady(int[] actorOrder)
        {
            if (actorOrder == null || actorOrder.Length == 0) return;

            // 1) 내 턴 인덱스 계산(1-based로 저장)
            int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
            int myTurnIndex0 = Array.IndexOf(actorOrder, myActor);
            int myTurnIndex1 = (myTurnIndex0 >= 0 ? myTurnIndex0 + 1 : -1);

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable { { "turnIndex", myTurnIndex1 } });

            Debug.Log($"[CardManager] 내 turnIndex={myTurnIndex1}");

            // 2) 카드 UI 닫기(중복호출 안전)
            if (_cards != null)
            {
                foreach (var c in _cards) if (c) c.gameObject.SetActive(false);
            }
            if (cardParent)   cardParent.gameObject.SetActive(false);
            if (cardUICanvas) cardUICanvas.SetActive(false);

            // 3) 마스터만 첫 턴 시작 (코디 완료 & 미니게임 활성까지 대기)
            if (PhotonNetwork.IsMasterClient)
                StartCoroutine(CoStartFirstTurn());
        }

        private IEnumerator CoStartFirstTurn()
        {
            // TurnManager 인스턴스 대기
            yield return new WaitUntil(() => KYG.TurnManager.Instance != null);

            // (중요) 포톤 뷰 코디네이션 완료까지 대기 (roots 활성 보장)
            while (LDH_MainGame.PhotonViewSync.Instance != null &&
                   !LDH_MainGame.PhotonViewSync.Instance.SyncCompleted)
            {
                yield return null;
            }

            // (중요) 미니게임 오브젝트가 "활성" 상태일 때까지 대기
            KYG.MeteorTapMiniGame mini = null;
            while ((mini = UnityEngine.Object.FindObjectOfType<KYG.MeteorTapMiniGame>(true)) == null ||
                   !mini.gameObject.activeInHierarchy)
            {
                yield return null;
            }

            Debug.Log("[CardManager] >>> StartFirstTurn preflight ok (SyncCompleted & MiniGame active)");

            // 이제 안전: 턴 세팅 후 시작
            KYG.TurnManager.Instance.SetupTurn();
            KYG.TurnManager.Instance.StartFirstTurn();
        }
        #endregion

        #region Photon Callbacks
        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (changed == null) return;

            var room = PhotonNetwork.CurrentRoom;
            if (room == null) return;

            if (changed.ContainsKey(KEY_DECK_VALUES) || changed.ContainsKey(KEY_CARD_OWNERS))
            {
                if ((_cards == null || _cards.Count == 0) && PhotonNetwork.CurrentRoom.CustomProperties != null)
                {
                    TryInitFromRoomProps();
                    Debug.Log("[CardManager] UI rebuilt from room props (late init).");
                }
            }

            // 공개 상태 감지: state == Revealing 이고, 덱/오너/시각 정보가 있으면 공개 시작
            if (room.CustomProperties.TryGetValue(KEY_STATE, out var stObj) &&
                (byte)stObj == (byte)LobbyState.Revealing)
            {
                // 덱/오너 캐시
                if (room.CustomProperties.TryGetValue(KEY_DECK_VALUES, out var dvObj))
                    _deckValues = ToIntArray(dvObj);
                if (room.CustomProperties.TryGetValue(KEY_CARD_OWNERS, out var owObj))
                    _owners = ToIntArray(owObj);

                // 공개 시점 & 길이 (없으면 최소값으로 보정)
                double t0 = room.CustomProperties.TryGetValue(KEY_REVEAL_T0, out var t0Obj) ? Convert.ToDouble(t0Obj) : (PhotonNetwork.Time + 0.1f);
                float  rs = room.CustomProperties.TryGetValue(KEY_REVEAL_SEC, out var rsObj) ? Convert.ToSingle(rsObj) : 1f;

                // 카드 UI가 아직 없으면 먼저 빌드
                if ((_cards == null || _cards.Count == 0) && _deckValues != null && _deckValues.Length > 0)
                {
                    BuildCardUIs();
                    RefreshInteractables();
                }

                // 이미 공개 코루틴이 돌고 있지 않다면 시작/갱신
                _t0 = t0; _revealSec = rs;
                if (_revealCo != null) StopCoroutine(_revealCo);
                _revealCo = StartCoroutine(CoSyncedReveal());
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            // 선택 중 누군가 이탈하면 Master가 남은 카드/인원을 재구성하는 로직을 여기에 추가 가능
            if (!PhotonNetwork.IsMasterClient) return;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KEY_STATE, out var stObj)) return;
            if ((byte)stObj != (byte)LobbyState.Picking) return;

            // 현재 로직에서는 단순히 진행 상황만 다시 판정(모두 선택 여부 등)
            int selectedCount = _owners?.Count(o => o != -1) ?? 0;
            int needCount     = PhotonNetwork.CurrentRoom.PlayerCount;

            if (selectedCount >= needCount)
            {
                _t0 = PhotonNetwork.Time + 0.3f;
                _revealSec = 1f * PhotonNetwork.CurrentRoom.PlayerCount;
                photonView.RPC(nameof(RPC_RevealAll), RpcTarget.AllBuffered, _deckValues, _owners, _t0, _revealSec);
            }
            else
            {
                photonView.RPC(nameof(RPC_OnPickUpdated), RpcTarget.AllBuffered, _owners);
            }
        }
        #endregion
    }
}
