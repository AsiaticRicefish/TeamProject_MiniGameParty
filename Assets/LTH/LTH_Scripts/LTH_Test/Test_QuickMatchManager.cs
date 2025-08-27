using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Test_QuickMatchManager : MonoBehaviourPunCallbacks
{
    [Header("UI (Optional)")]
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Match Options")]
    [SerializeField] private string gameSceneName = "JengaScene";
    [SerializeField] private bool useRandomRoom = false;
    [SerializeField] private string fixedRoomName = "DEV_JENGA";
    [SerializeField] private byte maxPlayers = 4;
    [SerializeField] private int minPlayersToStart = 2;

    [Header("Timing/Retry")]
    [SerializeField] private float uidWaitTimeoutSec = 5f;
    [SerializeField] private float createRetryBackoffSec = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool autoConnectOnPlay = false;
    [SerializeField] private string gameVersion = "jenga-v1";
    [SerializeField] private bool verboseLog = true;

    private Coroutine startRoutine;
    private bool connecting;                 // 매치메이킹 버튼 연타 방지
    private string lastTriedRoomName;        // CreateRoom 재시도용

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;

        // 버튼이 연결되어 있으면 클릭 이벤트 바인딩(옵션)
        if (quickMatchButton)
        {
            quickMatchButton.onClick.RemoveListener(OnClick_QuickMatch);
            quickMatchButton.onClick.AddListener(OnClick_QuickMatch);
        }
    }

    private void Start()
    {
        SetUI(true);
        SetStatus("Ready");

        // 이미 방 안이라면(미니게임에서 로비로 돌아온 케이스) 곧바로 게이트 시도
        if (PhotonNetwork.InRoom)
        {
            SetStatus($"In room: {PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
            TryStartGate();
            return;
        }

        // 방 밖이라면 평소대로 매치메이킹
        if (autoConnectOnPlay) OnClick_QuickMatch();
    }


    #region UI helpers
    private void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        if (verboseLog) Debug.Log($"[QuickMatch] {msg}");
    }

    private void SetUI(bool interactable)
    {
        if (quickMatchButton) quickMatchButton.interactable = interactable;
    }
    #endregion

    #region Public entry
    public void OnClick_QuickMatch()
    {
        if (connecting) return;
        connecting = true;
        SetUI(false);

        // 이미 방 안이면 재-조인 시도 대신 바로 게이트
        if (PhotonNetwork.InRoom)
        {
            SetStatus("Already in a room. Waiting for start...");
            TryStartGate();
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Already connected. Joining room...");
            EnsureAuthAndNickEarly();    // 가능하면 조인 전에 설정
            TryJoinOrCreate();
        }
        else
        {
            SetStatus("Connecting to Master...");
            EnsureAuthAndNickEarly();    // 연결 전에 AuthValues/NickName 선세팅
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void OnClick_LeaveRoom()
    {
        if (!PhotonNetwork.InRoom) return;
        SetStatus("Leaving room...");
        PhotonNetwork.LeaveRoom();
    }
    #endregion

    #region Photon callbacks
    public override void OnConnectedToMaster()
    {
        SetStatus("Connected to Master. Joining room...");
        TryJoinOrCreate();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        SetStatus($"No random room. Creating... ({message})");
        CreateRoomRandom();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        SetStatus($"Create failed: {message} → retry after backoff");
        StartCoroutine(CoRetryCreateAfterBackoff());
    }

    public override void OnCreatedRoom()
    {
        if (verboseLog) Debug.Log("[QuickMatch] OnCreatedRoom");
    }

    public override void OnJoinedRoom()
    {
        SetStatus($"Joined: {PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");

        EnsureLocalCustomProps(); // cp["uid"], cp["nickname"] 세팅(조인 직후 보강)
        DumpRoomState();

        TryStartGate();           // 마스터가 시작 게이트 진입
        connecting = false;       // 조인 성공 시 버튼 언락
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        SetStatus($"Player joined: {newPlayer.NickName} [{PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}]");
        TryStartGate();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        SetStatus($"Player left: {otherPlayer.NickName} [{PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}]");
        TryStartGate();
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changedProps)
    {
        if (PhotonNetwork.IsMasterClient && changedProps != null && changedProps.ContainsKey("uid"))
            TryStartGate();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        SetStatus($"Master switched → {newMasterClient.NickName}");
        TryStartGate();
    }

    public override void OnLeftRoom()
    {
        SetStatus("Left room.");
        SetUI(true);
        connecting = false;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetStatus($"Disconnected: {cause}");
        SetUI(true);
        connecting = false;
    }
    #endregion

    #region Join/Create flow
    private void TryJoinOrCreate()
    {
        // 매치메이킹 API는 오직 콜백 체인에서만 호출(다른 스크립트에서 호출 금지!)
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Not ready for matchmaking yet.");
            return;
        }

        if (useRandomRoom)
        {
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            var opt = new RoomOptions { MaxPlayers = maxPlayers, IsOpen = true, IsVisible = true };
            lastTriedRoomName = fixedRoomName;
            PhotonNetwork.JoinOrCreateRoom(lastTriedRoomName, opt, TypedLobby.Default);
        }
    }

    private void CreateRoomRandom()
    {
        lastTriedRoomName = $"ROOM_{Random.Range(1000, 9999)}";
        var opt = new RoomOptions { MaxPlayers = maxPlayers, IsOpen = true, IsVisible = true };
        PhotonNetwork.CreateRoom(lastTriedRoomName, opt, TypedLobby.Default);
    }

    private IEnumerator CoRetryCreateAfterBackoff()
    {
        yield return new WaitForSeconds(createRetryBackoffSec);
        if (useRandomRoom) CreateRoomRandom();
        else
        {
            // 고정방인데 충돌나면 접미사 붙여 재시도
            lastTriedRoomName = $"{fixedRoomName}_{Random.Range(100, 999)}";
            var opt = new RoomOptions { MaxPlayers = maxPlayers, IsOpen = true, IsVisible = true };
            PhotonNetwork.CreateRoom(lastTriedRoomName, opt, TypedLobby.Default);
        }
    }
    #endregion

    #region Start gate (Master only)
    private void TryStartGate()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (startRoutine != null) StopCoroutine(startRoutine);
        startRoutine = StartCoroutine(CoStartWhenReady());
    }

    private IEnumerator CoStartWhenReady()
    {
        // 1) 인원 충족
        while (PhotonNetwork.CurrentRoom == null ||
               PhotonNetwork.CurrentRoom.PlayerCount < minPlayersToStart)
            yield return null;

        // 2) 모든 플레이어의 uid 세팅 대기(타임아웃 허용)
        yield return CoWaitAllPlayerUids(uidWaitTimeoutSec);

        // 3) 씬 로드(마스터만)
        if (PhotonNetwork.IsMasterClient && !string.IsNullOrEmpty(gameSceneName))
        {
            SetStatus("Loading game scene...");
            PhotonNetwork.LoadLevel(gameSceneName);
        }

        startRoutine = null;
    }

    private IEnumerator CoWaitAllPlayerUids(float timeout)
    {
        float end = Time.time + timeout;
        while (Time.time < end)
        {
            var list = PhotonNetwork.PlayerList;
            bool allReady = list != null && list.Length >= minPlayersToStart &&
                            System.Array.TrueForAll(list, p =>
                                p.CustomProperties != null &&
                                p.CustomProperties.TryGetValue("uid", out var v) &&
                                v is string s && !string.IsNullOrEmpty(s));
            if (allReady) yield break;
            yield return null;
        }
        Debug.LogWarning("[QuickMatch] UID wait timed out. Proceeding anyway.");
    }
    #endregion

    #region Identity helpers
    private void EnsureAuthAndNickEarly()
    {
        // 연결 전에 AuthValues/NickName을 세팅하면 handshake에 반영됨
        string uid = null;
        try { uid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId; } catch { /* no-op */ }

        if (string.IsNullOrEmpty(uid))
        {
            // Photon AuthValues 또는 기존 NickName을 폴백으로 사용
            uid = !string.IsNullOrEmpty(PhotonNetwork.AuthValues?.UserId)
                ? PhotonNetwork.AuthValues.UserId
                : (string.IsNullOrEmpty(PhotonNetwork.NickName) ? null : PhotonNetwork.NickName);
        }
        if (string.IsNullOrEmpty(uid))
            uid = $"guest-{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";

        PhotonNetwork.AuthValues = new AuthenticationValues(uid);
        if (string.IsNullOrEmpty(PhotonNetwork.NickName))
            PhotonNetwork.NickName = uid;
    }

    private void EnsureLocalCustomProps()
    {
        // 조인 직후에도 cp에 uid/nickname이 없으면 보강
        var uid = PhotonNetwork.AuthValues?.UserId;
        if (string.IsNullOrEmpty(uid)) uid = PhotonNetwork.NickName;

        var props = new Hashtable
        {
            { "uid", uid },
            { "nickname", PhotonNetwork.NickName }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }
    #endregion

    #region Debug
    private void DumpRoomState()
    {
        if (!verboseLog) return;
        foreach (var p in PhotonNetwork.PlayerList)
        {
            p.CustomProperties?.TryGetValue("uid", out var uid);
        }
    }
    #endregion
}