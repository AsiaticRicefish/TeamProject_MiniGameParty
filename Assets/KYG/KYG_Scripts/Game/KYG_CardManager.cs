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
using LDH_MainGame;

/// <summary>
/// 카드 선택 로비 전체 제어(Master 권위).
/// - 인원 수 만큼 카드(1..N) 생성/셔플
/// - 중복 선택 불가
/// - 전원 선택 시 일괄 공개 → 오름차순으로 turnOrder 계산
/// - 다음 씬으로 동기 전환
/// </summary>

namespace KYG
{
    
public class CardManager : PunSingleton<CardManager>
{
    [Header("Prefabs & Layout")]
    [SerializeField] private Transform cardParent;   // 카드를 놓을 Grid/HorizontalLayout
    [SerializeField] private KYG.CardUI cardPrefab;

    [Header("Scene")]
    [SerializeField] private string nextSceneName = "PMS_ShootingTestScene";

    private const string KEY_DECK_VALUES = "deckValues";
    private const string KEY_CARD_OWNERS = "cardOwners";
    private const string KEY_STATE       = "state";
    private const string KEY_TURN_ORDER  = "turnOrder";

    private enum LobbyState : byte { Picking = 0, Revealing = 1, Done = 2 }

    // 로컬 캐시
    private List<KYG.CardUI> _cards = new();
    private int[] _deckValues; // 섞인 숫자들
    private int[] _owners;     // 각 index의 소유자 ActorNumber, 미선택 -1

    public bool allPicked = false;
    private bool _hasPickedLocal = false;
    private System.Random _rng = new System.Random();
    private Coroutine _autoRevealCo;

    private void Start()
    {
        Debug.Log($"[CardManager] Start. PhotonViewID={photonView?.ViewID}");
        StartCoroutine(WaitAndInit());
    }
    
    private IEnumerator WaitAndInit()
    {
        // 1) 싱크 매니저가 올라올 때까지
        yield return new WaitUntil(() => PhotonViewSync.Instance != null);

        // 2) 포톤 뷰 싱크 완료까지
        yield return new WaitUntil(() => PhotonViewSync.Instance.SyncCompleted);

        // 3) 카드 부모가 활성화될 때까지 (Coordinator.ActiveObjects() 이후)
        if (cardParent != null)
            yield return new WaitUntil(() => cardParent.gameObject.activeInHierarchy);

        // 4) 이제 카드 생성/동기화 시작
        if (PhotonNetwork.IsMasterClient) BuildAndBroadcastDeck();
        else TryInitFromRoomProps();
    }
    
    private IEnumerator WaitForSyncThenInit()
    {
        yield return new WaitUntil(() => LDH_MainGame.PhotonViewSync.Instance.SyncCompleted);
        InitDeck();
    }

    private void InitDeck()
    {
        if (PhotonNetwork.IsMasterClient)
            BuildAndBroadcastDeck();
        else
            TryInitFromRoomProps();
    }
    
    private IEnumerator AutoRevealAfter(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            // 이미 모두 선택되면 중단
            if (_owners.Count(o => o != -1) >= PhotonNetwork.CurrentRoom.PlayerCount)
                yield break;
            t += Time.deltaTime;
            yield return null;
        }
        // 남은 free 카드를 아직 미선택 플레이어에게 랜덤 배정
        var freeIdx = Enumerable.Range(0, _owners.Length).Where(i => _owners[i] == -1).ToList();
        var needActors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber)
            .Where(a => !_owners.Contains(a)).ToList();
        var rng = new System.Random();
        foreach (var a in needActors)
        {
            if (freeIdx.Count == 0) break;
            int pick = freeIdx[rng.Next(freeIdx.Count)];
            freeIdx.Remove(pick);
            _owners[pick] = a;
        }
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable{
            { "cardOwners", _owners }
        });
        // 여기서 selected==PlayerCount → 기존 로직에 의해 Reveal/Turn 시작
    }

    #region Deck Build & Sync
    private void BuildAndBroadcastDeck()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int n = PhotonNetwork.CurrentRoom.PlayerCount;
        _deckValues = Enumerable.Range(1, n).OrderBy(_ => _rng.Next()).ToArray();
        _owners     = Enumerable.Repeat(-1, n).ToArray();

        var props = new ExitGames.Client.Photon.Hashtable {
            { KEY_DECK_VALUES, _deckValues },
            { KEY_CARD_OWNERS, _owners     },
            { KEY_STATE, (byte)LobbyState.Picking }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        BuildCardUIs();
        RefreshInteractables();

        Debug.Log($"[CardManager] DECK n={n} values=[{string.Join(",", _deckValues)}] owners=[{string.Join(",", _owners)}] state=Picking");
    }
    
    private static int[] ToIntArray(object obj)
    {
        if (obj is int[] ia) return ia;
        if (obj is object[] oa) return oa.Select(o => Convert.ToInt32(o)).ToArray();
        return null;
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
        if (cardParent != null)
        {
            foreach (Transform t in cardParent) Destroy(t.gameObject);
        } 
        _cards.Clear();

        for (int i = 0; i < _deckValues.Length; i++)
        {
            var card = Instantiate(cardPrefab, cardParent);
            int idx = i;
            card.Setup(idx, _deckValues[idx], (ci) => TryPick(ci, card)); // 클릭 콜백 연결
            _cards.Add(card);
        }
    }

    private void RefreshInteractables()
    {
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        bool iAlreadyPicked = (_owners != null) && System.Array.IndexOf(_owners, myActor) != -1;

        for (int i = 0; i < _cards.Count; i++)
        {
            int owner = _owners[i];
            bool free = owner == -1;
            bool isMine = owner == myActor;

            if (free)
                _cards[i].SetInteractable(!iAlreadyPicked, false);
            else
            {
                _cards[i].SetInteractable(false, isMine);
                _cards[i].SetSelected(isMine);
            }
        }
    }
    #endregion

    #region Picking
    /// <summary>
    /// 로컬에서 카드 클릭 시 호출(모든 검증은 Master가 처리)
    /// </summary>
    private void TryPick(int cardIndex, KYG.CardUI sender = null)
    {
        Debug.Log($"[CardManager] TryPick send -> actor={PhotonNetwork.LocalPlayer.ActorNumber}, idx={cardIndex}");
        if (!PhotonNetwork.InRoom) return;
        if (_hasPickedLocal) return;         // 중복 클릭 방지

        _hasPickedLocal = true;

        if (sender != null) sender.SetSelected(true);
        else if (cardIndex >= 0 && cardIndex < _cards.Count && _cards[cardIndex] != null)
            _cards[cardIndex].SetSelected(true);

        LockAllExcept(cardIndex);            // 승인/거절 올 때까지 임시 잠금

        photonView.RPC(nameof(RPC_TryPick), RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber, cardIndex);
    }
    
    private void LockAllExcept(int keepIndex)
    {
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
    Debug.Log($"[CardManager] RPC_TryPick recv -> actor={actorNumber}, idx={cardIndex}");
    if (!PhotonNetwork.IsMasterClient) return;

    if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KEY_STATE, out var stObj)) return;
    if ((byte)stObj != (byte)LobbyState.Picking) return;

    if (_owners == null || _deckValues == null) return;
    if (cardIndex < 0 || cardIndex >= _owners.Length) return;

    // 이미 선택된 카드?
    if (_owners[cardIndex] != -1)
    {
        photonView.RPC(nameof(RPC_PickRejected), PhotonNetwork.CurrentRoom.GetPlayer(actorNumber), cardIndex, 1);
        return;
    }

    // 이미 이 배우가 다른 카드를 갖고 있으면 거절 (서버 측 “한 사람 한 장” 보장)
    if (System.Array.IndexOf(_owners, actorNumber) != -1)
    {
        photonView.RPC(nameof(RPC_PickRejected), PhotonNetwork.CurrentRoom.GetPlayer(actorNumber), cardIndex, 2);
        return;
    }

    // 선택 확정
    _owners[cardIndex] = actorNumber;

    PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {
        { KEY_CARD_OWNERS, _owners }
    });

    // “모두 선택” 판정은 덱 길이 기준이 아니라 “선택 수 >= 현재 인원 수”로 보강
    int selectedCount = _owners.Count(o => o != -1);
    int needCount     = PhotonNetwork.CurrentRoom.PlayerCount;

    Debug.Log($"[CardManager] Master Pick ok. owners=[{string.Join(",", _owners)}], selected={selectedCount}/{needCount}");

    if (selectedCount >= needCount)   // ✅ 전원 선택
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { KEY_STATE, (byte)LobbyState.Revealing } });

        photonView.RPC(nameof(RPC_RevealAll), RpcTarget.AllBuffered, _deckValues, _owners);
        Debug.Log("[CardManager] >>> RPC_RevealAll sent");          // ✅ 이 로그가 마스터에 반드시 찍혀야 함

        var order = Enumerable.Range(0, _deckValues.Length)
            .Select(i => (_deckValues[i], _owners[i]))
            .OrderBy(t => t.Item1)
            .Select(t => t.Item2)
            .ToArray();

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
            { KEY_TURN_ORDER, order },
            { KEY_STATE, (byte)LobbyState.Done }
        });

        photonView.RPC(nameof(RPC_OnTurnOrderReady), RpcTarget.AllBuffered, order);
        Debug.Log($"[CardManager] >>> RPC_OnTurnOrderReady sent order=[{string.Join(",", order)}]");
    }
    else
    {
        photonView.RPC(nameof(RPC_OnPickUpdated), RpcTarget.AllBuffered, _owners);
        Debug.Log("[CardManager] RPC_OnPickUpdated sent");
    }
}
    
    [PunRPC]
    private void RPC_PickRejected(int cardIndex, int reason)
    {
        Debug.Log($"[CardManager] PickRejected idx={cardIndex}, reason={reason}");
        _hasPickedLocal = false;
        UnlockAllFreeCards();  // 로컬에서 임시 잠금 풀기
        if (cardIndex >= 0 && cardIndex < _cards.Count && _cards[cardIndex] != null)
            _cards[cardIndex].SetInteractable(true, false);
    }

    [PunRPC]
    private void RPC_OnPickUpdated(int[] ownersFromMaster)
    {
        _owners = ownersFromMaster;
        _hasPickedLocal = System.Array.IndexOf(_owners, PhotonNetwork.LocalPlayer.ActorNumber) != -1;
        RefreshInteractables();

        int selected = _owners.Count(o => o != -1);
        int need = PhotonNetwork.CurrentRoom.PlayerCount;
        Debug.Log($"[CardManager] Progress: {selected}/{need} picked");
        // TODO: Text로 "1/2 선택됨 - 상대 선택 대기" 표기하면 UX 훨씬 명확
    }

    [PunRPC]
    private void RPC_RevealAll(int[] deckValues, int[] ownersFromMaster)
    {
        Debug.Log("[CardManager] <<< RPC_RevealAll recv");
        _deckValues = deckValues;
        _owners     = ownersFromMaster;
        _hasPickedLocal = true;

        for (int i = 0; i < _cards.Count; i++) _cards[i]?.RevealFace();
        RefreshInteractables();
    }
    
    [PunRPC]
    private void RPC_OnTurnOrderReady(int[] actorOrder)
    {
        // 1) 내 턴 인덱스 계산
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        int myTurnIndex0 = System.Array.IndexOf(actorOrder, myActor); // 0-based
        int myTurnIndex1 = (myTurnIndex0 >= 0 ? myTurnIndex0 + 1 : -1);

        // 2) Player CustomProperties에 저장 (의존성 제거)
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable { { "turnIndex", myTurnIndex1 } });

        // 3) (기존) 카드 UI 닫기 - 널가드
        if (_cards != null)
        {
            foreach (var c in _cards) if (c) c.gameObject.SetActive(false);
        }
        if (cardParent) cardParent.gameObject.SetActive(false);

        // 4) 마스터만 첫 턴 시작 - TurnManager 존재 확인
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(CoStartFirstTurn());
        }
    }
    private IEnumerator CoStartFirstTurn()
    {
        // TurnManager가 씬에 활성화될 때까지 기다림
        yield return new WaitUntil(() => KYG.TurnManager.Instance != null);

        KYG.TurnManager.Instance.SetupTurn();
        KYG.TurnManager.Instance.StartFirstTurn();
    }
    
    #endregion

    #region Photon Callbacks
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed == null) return;

        // 디버그
        Debug.Log("[CardManager] Room props updated: " + string.Join(",", changed.Keys.Cast<object>()));

        bool deckChanged  = changed.ContainsKey(KEY_DECK_VALUES);
        bool ownerChanged = changed.ContainsKey(KEY_CARD_OWNERS);

        if (!deckChanged && !ownerChanged) return;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        // 안전 캐스팅 (object[] -> int[])
        int[] dv = room.CustomProperties.TryGetValue(KEY_DECK_VALUES, out var dvObj) ? ToIntArray(dvObj) : null;
        int[] ow = room.CustomProperties.TryGetValue(KEY_CARD_OWNERS, out var owObj) ? ToIntArray(owObj) : null;

        // ---- FAIL-SAFE 1: 카드 컨테이너가 꺼져 있으면 켠다 ----
        if (cardParent != null && !cardParent.gameObject.activeInHierarchy)
            cardParent.gameObject.SetActive(true);

        // ---- FAIL-SAFE 2: 카드가 아직 안 만들어졌으면 지금 만든다 ----
        bool needBuild = (_cards == null || _cards.Count == 0) && dv != null && ow != null;
        if (needBuild)
        {
            _deckValues = dv;
            _owners     = ow;

            BuildCardUIs();       // 프리팹/부모 null 가드 포함
            RefreshInteractables();
            return;
        }

        // 이미 UI가 있다면 소유만 동기화
        if (ow != null)
        {
            _owners = ow;
            RefreshInteractables();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // 선택 중 누군가 이탈하면 Master가 남은 카드/인원을 재구성하는 로직을 여기에 추가 가능
        // (필요 시: 상태가 Picking일 때만 재빌드)
    }
    #endregion
}
}
