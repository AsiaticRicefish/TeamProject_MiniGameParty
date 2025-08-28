using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using KYG.Auth; // GuestLoginManager
using ExitGames.Client.Photon;

/// <summary>
/// 전 씬 공통 UID 보정 가드:
/// - 마스터 접속/로비 입장/룸 입장/속성변경 시 LocalPlayer의 uid 누락을 자동 보정
/// - GuestLoginManager.SafeReapplyUid 와 같은 목적, but 전역 보강
/// </summary>
public class UidPersistenceGuard : MonoBehaviourPunCallbacks
{
    private const string UID_KEY = "uid";

    private static UidPersistenceGuard _instance;
    public static UidPersistenceGuard Instance => _instance;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void EnsureLocalUid()
    {
        var glm = GuestLoginManager.Instance; // Firebase 로그인 소스
        var local = PhotonNetwork.LocalPlayer;

        if (glm == null || local == null) return;

        // Firebase의 userId가 있고, LocalPlayer 커스텀에 uid가 비어있다면 재주입
        var user = typeof(GuestLoginManager)
            .GetField("user", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(glm) as Firebase.Auth.FirebaseUser;

        var uid = user?.UserId;
        if (string.IsNullOrEmpty(uid)) return;

        bool hasUid = local.CustomProperties != null &&
                      local.CustomProperties.ContainsKey(UID_KEY) &&
                      local.CustomProperties[UID_KEY] is string s &&
                      !string.IsNullOrEmpty(s);

        if (!hasUid)
        {
            var props = new Hashtable { { UID_KEY, uid } };
            local.SetCustomProperties(props);
            // Photon NickName도 보정 (선택)
            // if (!string.IsNullOrEmpty(user.DisplayName)) PhotonNetwork.NickName = user.DisplayName;

            Debug.Log("[UidPersistenceGuard] Reapplied uid to LocalPlayer.");
        }
    }

    public override void OnConnectedToMaster() => EnsureLocalUid();
    public override void OnJoinedLobby() => EnsureLocalUid();
    public override void OnJoinedRoom() => EnsureLocalUid();
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer != null && targetPlayer.IsLocal && changedProps != null && changedProps.ContainsKey(UID_KEY))
            EnsureLocalUid();
    }
}
