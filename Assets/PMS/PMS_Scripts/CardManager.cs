using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using DesignPattern;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ShootingScene;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Random = System.Random;

/// <summary>
/// 카드 선택 로비 전체 제어(Master 권위).
/// - 인원 수 만큼 카드(1..N) 생성/셔플
/// - 중복 선택 불가
/// - 전원 선택 시 일괄 공개 → 오름차순으로 turnOrder 계산
/// - 다음 씬으로 동기 전환
/// </summary>
public class CardManager : PunSingleton<CardManager>
{
    [Header("Prefabs & Layout")] [SerializeField]
    private Transform cardParent; // 카드를 놓을 Grid/HorizontalLayout

    [SerializeField] private GameObject cardUICanvas; //최종적으로 비활성화 시킬 UI
    [SerializeField] private ShootingScene.CardUI cardPrefab;

    [Header("Scene")] [SerializeField] private string nextSceneName = "PMS_ShootingTestScene";

    //private const string KEY_DECK_VALUES = "deckValues";
    //private const string KEY_CARD_OWNERS = "cardOwners";
    //private const string KEY_STATE = "state";
    //private const string KEY_TURN_ORDER = "turnOrder";

    private enum LobbyState : byte { Picking = 0, Revealing = 1, Done = 2 }

    // 로컬 캐시
    private List<ShootingScene.CardUI> _cards = new();
    public int[] _deckValues; // 섞인 숫자들
    private int[] _owners; // 각 index의 소유자 ActorNumber, 미선택 -1

    public bool allPicked = false;

    //----- flag / request ---- //
    private bool _requestPick = false;
    private bool _alreadyPicked = false;

    //---- card reveal animation -----//
    private float _revealSec = -1f;
    private double t0 = -1;

    private void Start()
    {
        // 씬 자동 동기화 권장
        // ------ 미니게임을 additive로 로컬에서 각자 올리기 때문에 automatically sync scene 을 해제해야 합니다. -------- //
        // PhotonNetwork.AutomaticallySyncScene = true;
        
    }

    #region Deck Build & Sync

    public void BuildAndBroadcastDeck()
    {
        int playerCount = Mathf.Clamp(PhotonNetwork.CurrentRoom.PlayerCount, 2, 4);
        _deckValues = Enumerable.Range(1, playerCount).ToArray();

        // 안정적 재현을 위해 시드 생성(방 생성 시간 기반)
        // int seed = (int)(PhotonNetwork.CurrentRoom.CreatedAt / 1000 % int.MaxValue);
        int seed = Guid.NewGuid().GetHashCode() ^ PhotonNetwork.ServerTimestamp;
        ShuffleInPlace(_deckValues, new Random(seed));

        _owners = Enumerable.Repeat(-1, _deckValues.Length).ToArray();

        var props = new Hashtable
        {
            { ShootingGamePropertyKeys.KEY_DECK_VALUES, _deckValues }, { ShootingGamePropertyKeys.KEY_CARD_OWNERS, _owners }, { ShootingGamePropertyKeys.KEY_STATE, (byte)LobbyState.Picking }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log($"[CardManager] Deck after shuffle: {string.Join(",", _deckValues)} (seed={seed})");

        BuildCardUIs();
    }

    public void TryInitFromRoomProps()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null || room.CustomProperties == null) return;

        if (room.CustomProperties.TryGetValue(ShootingGamePropertyKeys.KEY_DECK_VALUES, out var dvObj) &&
            room.CustomProperties.TryGetValue(ShootingGamePropertyKeys.KEY_CARD_OWNERS, out var ownObj))
        {
            _deckValues = (int[])dvObj;
            _owners = (int[])ownObj;
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
            card.Setup(idx, _deckValues[idx], TryPick); // 클릭 콜백 연결
            _cards.Add(card);
        }
    }

    private void RefreshInteractables()
    {
        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;

        for (int i = 0; i < _cards.Count; i++)
        {
            int owner = _owners[i];
            bool free = owner == -1;
            bool isMine = owner == myActor;


            if (free)
            {
                // 아직 선택되지 않은 카드 → 선택 가능(흰색/내 카드면 연녹)
                _cards[i].SetInteractable(true, isMine);
            }
            else
            {
                // 이미 누군가 선택한 카드 → 회색(내 카드면 연녹 고정)
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
    private void TryPick(int cardIndex)
    {
        if (!PhotonNetwork.InRoom) return;
        if (_requestPick) return; // 이미 pick에 대한 요청 처리를 한 상태
        if (_alreadyPicked) return; // 이미 선택 완료한 상태

        _requestPick = true;

        // Master에게 선택 요청
        photonView.RPC(nameof(RPC_TryPick), RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber, cardIndex);
    }

    [PunRPC]
    private void RPC_TryPick(int actorNumber, int cardIndex, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 방 상태 확인
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ShootingGamePropertyKeys.KEY_STATE, out var stObj)) return;
        if ((byte)stObj != (byte)LobbyState.Picking) return;

        if (cardIndex < 0 || cardIndex >= _owners.Length) return;
        if (_owners[cardIndex] != -1)
        {
            Debug.Log("[CardManager] 이미 선택된 카드입니다.");

            //요청자에게 실패 콜백
            photonView.RPC(nameof(RPC_PickResult), info.Sender, false, actorNumber,  -1);

            return; // 이미 선택된 카드
        }

        // 소유자 확정
        _owners[cardIndex] = actorNumber;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { ShootingGamePropertyKeys.KEY_CARD_OWNERS, _owners } });

        // 요청자에게 성공 콜백
        photonView.RPC(nameof(RPC_PickResult), RpcTarget.AllBuffered, true, actorNumber, cardIndex);


        // 모두 선택했는지 확인
        CheckAllPicked();
     
    }

    [PunRPC]
    private void RPC_PickResult(bool result, int actorNumber, int confirmedIndex)
    {
        if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            _requestPick = false; //요청 플래그 복구
            _alreadyPicked = result;
        }
      
        if (result)
            _owners[confirmedIndex] = actorNumber;
    }

    [PunRPC]
    private void RPC_OnPickUpdated(int[] ownersFromMaster)
    {
        _owners = ownersFromMaster;

        RefreshInteractables();
    }

    [PunRPC]
    private void RPC_RevealAll(int[] deckValues, int[] ownersFromMaster, double t0, float revealSec)
    {
        _deckValues = deckValues;
        _owners = ownersFromMaster;

        // // 모든 카드 공개
        // for (int i = 0; i < _cards.Count; i++)
        // {
        //     _cards[i].RevealFace();
        // }
        //
        // RefreshInteractables();

        StartCoroutine(RevealRoutine(t0));
    }

    [PunRPC]
    private void RPC_OnTurnOrderReady(int[] actorOrder)
    {
        #region Legacy

        // // 1) 내 턴 인덱스 계산(0-based)
        // int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
        // int myTurnIndex = System.Array.IndexOf(actorOrder, myActor);
        //
        // //2) 로컬 PlayerData에 반영
        // string myUid = PMS_Util.PMS_Util.GetMyUid();
        // var myPlayer = PlayerManager.Instance.GetPlayer(myUid);
        // if (myPlayer != null && myTurnIndex >= 0)
        // {
        //     myPlayer.ShootingData.myTurnIndex = myTurnIndex + 1;    // 1-based          1문제 - 동기화가 안된다. 턴인덱스 내꺼만 넣음.
        //     Debug.Log($"[CardManager] 내 턴 인덱스 확정: {myTurnIndex}");
        //
        //     var table = new Hashtable { { ShootingGamePlayerPropertyKeys.MyTurnIndex, myTurnIndex + 1 }};
        //     PhotonNetwork.LocalPlayer.SetCustomProperties(table);

        #endregion

        TurnManager.Instance.InitTurnOrder(actorOrder);

        if (PhotonNetwork.IsMasterClient)
        {
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "TurnCheckState");
        }

        #region Legacy

        // // 3) 카드 UI 비활성/숨김 (선택 UI 닫기) -> 여기서 하면 안될 것 같음
        // foreach (var c in _cards) c.gameObject.SetActive(false);
        // // 필요 시 카드 부모 패널도 끄기
        // if (cardParent != null) cardParent.gameObject.SetActive(false);

        // 4) 마스터만 첫 턴 시작
        /*if (PhotonNetwork.IsMasterClient)
        {
            // 0번 인덱스부터 시작
            ShootingScene.TurnManager.Instance.StartFirstTurn();
        }*/

        #endregion
        
    }

    #endregion


    #region Check All Picked

    private void CheckAllPicked()
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        int pickedPlayerCount = 0;
        int currentPlayerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        foreach (int ownerActorNum in _owners)
        {
            if (ownerActorNum != -1 && PhotonNetwork.CurrentRoom.GetPlayer(ownerActorNum) != null)
            {
                pickedPlayerCount++;
            }
        }

        bool isAllPicked = pickedPlayerCount == currentPlayerCount;
        if (isAllPicked)
        {
            // 상태 전환
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { ShootingGamePropertyKeys.KEY_STATE, (byte)LobbyState.Revealing } });

            // 마스터 서버 공개 시작 시간 처리
            t0 = PhotonNetwork.Time + 0.3; // 지연 감안한 여유 시간

            _revealSec = 1f * PhotonNetwork.CurrentRoom.PlayerCount;

            // 모든 클라에 공개 지시
            photonView.RPC(nameof(RPC_RevealAll), RpcTarget.AllBuffered, _deckValues, _owners, t0, _revealSec);


            // 턴 순서 계산(숫자 오름차순 → 카드 소유자의 ActorNumber)
            var pairs = new List<(int value, int owner)>();
            for (int i = 0; i < _deckValues.Length; i++)
                pairs.Add((_deckValues[i], _owners[i]));

            var order = pairs.OrderBy(p => p.value).Select(p => p.owner).ToArray();

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { ShootingGamePropertyKeys.KEY_TURN_ORDER, order }, { ShootingGamePropertyKeys.KEY_STATE, (byte)LobbyState.Done }
            });


            StartCoroutine(NotifyTurnOrderAfterReveal(t0, _revealSec, order));
        }
        else
        {
            // 선택 갱신만 반영되도록 각 클라 로컬 UI 갱신 요청
            photonView.RPC(nameof(RPC_OnPickUpdated), RpcTarget.AllBuffered, _owners);
        }
    }
    

    #endregion
    
    #region Card Reveal 연출 관련 로직

    private IEnumerator RevealRoutine(double t0)
    {
        while (PhotonNetwork.Time < t0) yield return null;
        for (int i = 0; i < _cards.Count; i++)
            yield return _cards[i].RevealFace(); // 내부에서 tweens/particles
    }

    private IEnumerator NotifyTurnOrderAfterReveal(double t0, float sec, int[] order)
    {
        if (!PhotonNetwork.IsMasterClient) yield break;

        yield return WaitUntilNetworkTime(t0 + sec);

        photonView.RPC(nameof(RPC_CloseCardUI), RpcTarget.All); //UI 끄기
        yield return null;
        photonView.RPC(nameof(RPC_OnTurnOrderReady), RpcTarget.AllBuffered, order); // 턴 order 알리기
    }

    private IEnumerator WaitUntilNetworkTime(double target)
    {
        while (PhotonNetwork.Time < target) yield return null;
    }

    [PunRPC]
    private void RPC_CloseCardUI()
    {
        // 3) 카드 UI 비활성/숨김 (선택 UI 닫기)
        foreach (var c in _cards) c.gameObject.SetActive(false);
        // 필요 시 카드 부모 패널도 끄기
        if (cardParent != null) cardParent.gameObject.SetActive(false);
        // 가장 상위 캔버스 끄기
        cardUICanvas?.gameObject.SetActive(false);
        Debug.Log("카드 ui 숨기기");
    }

    #endregion

    #region Photon Callbacks

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // 뒤늦게 입장한 클라가 즉시 동기화될 수 있도록 안전망
        if (propertiesThatChanged.ContainsKey(ShootingGamePropertyKeys.KEY_DECK_VALUES) ||
            propertiesThatChanged.ContainsKey(ShootingGamePropertyKeys.KEY_CARD_OWNERS))
        {
            TryInitFromRoomProps();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // 선택 중 누군가 이탈하면 Master가 남은 카드/인원을 재구성하는 로직을 여기에 추가 가능
        // (필요 시: 상태가 Picking일 때만 재빌드)
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ShootingGamePropertyKeys.KEY_STATE, out var stObj)) return;
        if ((byte)stObj != (byte)LobbyState.Picking) return;
        
        //picking 상태일 때 나간 플레이어가 선택한 카드가 있다면 제거 
        if (PhotonNetwork.IsMasterClient)
        {
            CheckAllPicked();
        }
        
        
    }

    #endregion


    private void autoCardSelect()
    {
        

    }
}