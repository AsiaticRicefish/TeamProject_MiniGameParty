using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Auth;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class TestMatching : MonoBehaviourPunCallbacks
{
    [Header("UI (Optional)")]
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Match Options")]
    [SerializeField] private string gameSceneName = "PMS_ShootingTestScene";
    [SerializeField] private bool useRandomRoom = false;
    [SerializeField] private string fixedRoomName = "DEV_SHOOTING";
    [SerializeField] private byte maxPlayers = 4;
    [SerializeField] private int minPlayersToStart = 2;

    [Header("Timing/Retry")]
    [SerializeField] private float uidWaitTimeoutSec = 5f;
    [SerializeField] private float createRetryBackoffSec = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool autoConnectOnPlay = false;
    [SerializeField] private string gameVersion = "shooting-v1";
    [SerializeField] private bool verboseLog = true;

    private Coroutine startRoutine;
    private bool connecting;                 // ��ġ����ŷ ��ư ��Ÿ ����
    private string lastTriedRoomName;        // CreateRoom ��õ���

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;

        // ��ư�� ����Ǿ� ������ Ŭ�� �̺�Ʈ ���ε�(�ɼ�)
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
        if (autoConnectOnPlay) OnClick_QuickMatch();
    }

    //private void OnDisable()
    //{
    //    if (startRoutine != null) StopCoroutine(startRoutine);
    //    startRoutine = null;
    //    connecting = false;
    //}

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

        if (PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("Already connected. Joining room...");
            EnsureAuthAndNickEarly();    // �����ϸ� ���� ���� ����
            TryJoinOrCreate();
        }
        else
        {
            SetStatus("Connecting to Master...");
            EnsureAuthAndNickEarly();    // ���� ���� AuthValues/NickName ������
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
        SetStatus($"Create failed: {message} �� retry after backoff");
        StartCoroutine(CoRetryCreateAfterBackoff());
    }

    public override void OnCreatedRoom()
    {
        if (verboseLog) Debug.Log("[QuickMatch] OnCreatedRoom");
    }

    public override void OnJoinedRoom()
    {
        SetStatus($"Joined: {PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");

        EnsureLocalCustomProps(); // cp["uid"], cp["nickname"] ����(���� ���� ����)
        DumpRoomState();

        TryStartGate();           // �����Ͱ� ���� ����Ʈ ����
        connecting = false;       // ���� ���� �� ��ư ���
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
        SetStatus($"Master switched �� {newMasterClient.NickName}");
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
        // ��ġ����ŷ API�� ���� �ݹ� ü�ο����� ȣ��(�ٸ� ��ũ��Ʈ���� ȣ�� ����!)
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
            // �������ε� �浹���� ���̻� �ٿ� ��õ�
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
        // 1) �ο� ����
        while (PhotonNetwork.CurrentRoom == null ||
               PhotonNetwork.CurrentRoom.PlayerCount < minPlayersToStart)
            yield return null;

        // 2) ��� �÷��̾��� uid ���� ���(Ÿ�Ӿƿ� ���)
        yield return CoWaitAllPlayerUids(uidWaitTimeoutSec);

        // 3) �� �ε�(�����͸�)
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
        // ���� ���� AuthValues/NickName�� �����ϸ� handshake�� �ݿ���
        string uid = null;
        try { uid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId; } catch { /* no-op */ }

        if (string.IsNullOrEmpty(uid))
        {
            // Photon AuthValues �Ǵ� ���� NickName�� �������� ���
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
        // ���� ���Ŀ��� cp�� uid/nickname�� ������ ����
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
