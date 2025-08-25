using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

using Random = System.Random;

/// <summary>
/// 카드 선택 로비 전체 제어(Master 권위).
/// - 인원 수 만큼 카드(1..N) 생성/셔플
/// - 중복 선택 불가
/// - 전원 선택 시 일괄 공개 → 오름차순으로 turnOrder 계산
/// - 다음 씬으로 동기 전환
/// </summary>
public class CardManager : MonoBehaviourPunCallbacks
{
    [Header("Prefabs & Layout")]
    [SerializeField] private Transform cardParent;   // 카드를 놓을 Grid/HorizontalLayout
    [SerializeField] private ShootingScene.CardUI cardPrefab;

    [Header("Scene")]
    [SerializeField] private string nextSceneName = "PMS_ShootingTestScene";

    private const string KEY_DECK_VALUES = "deckValues";
    private const string KEY_CARD_OWNERS = "cardOwners";
    private const string KEY_STATE       = "state";
    private const string KEY_TURN_ORDER  = "turnOrder";

    private enum LobbyState : byte { Picking = 0, Revealing = 1, Done = 2 }

    // 로컬 캐시
    private List<ShootingScene.CardUI> _cards = new();
    private int[] _deckValues; // 섞인 숫자들
    private int[] _owners;     // 각 index의 소유자 ActorNumber, 미선택 -1

    private void Start()
    {
        // 씬 자동 동기화 권장
        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.IsMasterClient)
        {
            BuildAndBroadcastDeck();
        }
        else
        {
            // 이미 방에 deck이 있을 수 있으니 즉시 읽기 시도
            TryInitFromRoomProps();
        }
    }

    #region Deck Build & Sync
    private void BuildAndBroadcastDeck()
    {
        int playerCount = Mathf.Clamp(PhotonNetwork.CurrentRoom.PlayerCount, 2, 4);
        _deckValues = Enumerable.Range(1, playerCount).ToArray();

        // 안정적 재현을 위해 시드 생성(방 생성 시간 기반)
        int seed = (int)(PhotonNetwork.CurrentRoom.CreatedAt / 1000 % int.MaxValue);
        ShuffleInPlace(_deckValues, new Random(seed));

        _owners = Enumerable.Repeat(-1, _deckValues.Length).ToArray();

        var props = new Hashtable
        {
            { KEY_DECK_VALUES, _deckValues },
            { KEY_CARD_OWNERS, _owners },
            { KEY_STATE, (byte)LobbyState.Picking }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        BuildCardUIs();
    }

    private void TryInitFromRoomProps()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null || room.CustomProperties == null) return;

        if (room.CustomProperties.TryGetValue(KEY_DECK_VALUES, out var dvObj) &&
            room.CustomProperties.TryGetValue(KEY_CARD_OWNERS, out var ownObj))
        {
            _deckValues = (int[])dvObj;
            _owners     = (int[])ownObj;
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
        foreach (Transform t in cardParent) Destroy(t.gameObject);
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
            int owner   = _owners[i];
            bool free   = owner == -1;
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

        // Master에게 선택 요청
        photonView.RPC(nameof(RPC_TryPick), RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber, cardIndex);
    }

    [PunRPC]
    private void RPC_TryPick(int actorNumber, int cardIndex, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 방 상태 확인
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KEY_STATE, out var stObj)) return;
        if ((byte)stObj != (byte)LobbyState.Picking) return;

        if (cardIndex < 0 || cardIndex >= _owners.Length) return;
        if (_owners[cardIndex] != -1) return; // 이미 선택된 카드

        // 소유자 확정
        _owners[cardIndex] = actorNumber;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { KEY_CARD_OWNERS, _owners } });

        // 모두 선택했는지 확인
        bool allPicked = _owners.All(o => o != -1);
        if (allPicked)
        {
            // 상태 전환
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { KEY_STATE, (byte)LobbyState.Revealing } });
            // 모든 클라에 공개 지시
            photonView.RPC(nameof(RPC_RevealAll), RpcTarget.AllBuffered, _deckValues, _owners);

            // 턴 순서 계산(숫자 오름차순 → 카드 소유자의 ActorNumber)
            var pairs = new List<(int value, int owner)>();
            for (int i = 0; i < _deckValues.Length; i++)
                pairs.Add((_deckValues[i], _owners[i]));

            var order = pairs.OrderBy(p => p.value).Select(p => p.owner).ToArray();

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { KEY_TURN_ORDER, order },
                { KEY_STATE, (byte)LobbyState.Done }
            });

            // 다음 씬 전환
            PhotonNetwork.LoadLevel(nextSceneName);
        }
        else
        {
            // 선택 갱신만 반영되도록 각 클라 로컬 UI 갱신 요청
            photonView.RPC(nameof(RPC_OnPickUpdated), RpcTarget.AllBuffered, _owners);
        }
    }

    [PunRPC]
    private void RPC_OnPickUpdated(int[] ownersFromMaster)
    {
        _owners = ownersFromMaster;
        RefreshInteractables();
    }

    [PunRPC]
    private void RPC_RevealAll(int[] deckValues, int[] ownersFromMaster)
    {
        _deckValues = deckValues;
        _owners = ownersFromMaster;

        // 모든 카드 공개
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].RevealFace();
        }

        RefreshInteractables();
    }
    #endregion

    #region Photon Callbacks
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // 뒤늦게 입장한 클라가 즉시 동기화될 수 있도록 안전망
        if (propertiesThatChanged.ContainsKey(KEY_DECK_VALUES) ||
            propertiesThatChanged.ContainsKey(KEY_CARD_OWNERS))
        {
            TryInitFromRoomProps();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // 선택 중 누군가 이탈하면 Master가 남은 카드/인원을 재구성하는 로직을 여기에 추가 가능
        // (필요 시: 상태가 Picking일 때만 재빌드)
    }
    #endregion
}
