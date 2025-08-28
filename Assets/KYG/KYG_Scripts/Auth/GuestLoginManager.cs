using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.SceneManagement; // 필요시

namespace KYG.Auth
{
    public class GuestLoginManager : MonoBehaviourPunCallbacks
    {
        public static GuestLoginManager Instance { get; private set; }

        [Header("Photon")]
        [SerializeField] private string defaultRegion = "asia";

        [Header("Scene Names")]
        [SerializeField] private string lobbySceneName = "LDH_MainScene";            // 로비(대기) 씬 이름
        [SerializeField] private string gameplaySceneName = "PMS_ShootingTestScene"; // 실제 게임 씬 (예시)

        [Header("Flow Options")]
        [SerializeField] private bool loadLobbyOnJoinedRoom = true; // 룸 입장 시 로비씬 자동 로드

        public bool IsFirebaseReady { get; private set; }
        public bool IsPhotonConnected => PhotonNetwork.IsConnected;

        private FirebaseAuth auth;
        private FirebaseUser user;
        private string pendingNickname;
        private bool isConnecting;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                var status = task.Result;
                if (status == DependencyStatus.Available)
                {
                    auth = FirebaseAuth.DefaultInstance;
                    IsFirebaseReady = true;
                    Debug.Log("[GuestLoginManager] Firebase ready.");
                }
                else
                {
                    IsFirebaseReady = false;
                    Debug.LogError($"[GuestLoginManager] Firebase dependencies not available: {status}");
                }
            });

            PhotonNetwork.AutomaticallySyncScene = true; // ★ 씬 동기화 필수
            if (!string.IsNullOrEmpty(defaultRegion))
                PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = defaultRegion;
        }

        public void LoginAsGuestWithNickname(string nickname)
        {
            if (!IsFirebaseReady)
            {
                Debug.LogWarning("[GuestLoginManager] Firebase not ready yet.");
                return;
            }
            if (isConnecting)
            {
                Debug.Log("[GuestLoginManager] Already connecting...");
                return;
            }

            isConnecting = true;
            pendingNickname = nickname;

            auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    isConnecting = false;
                    Debug.LogError($"[GuestLoginManager] Firebase anonymous sign-in failed: {t.Exception}");
                    return;
                }

                user = t.Result.User;
                Debug.Log($"[GuestLoginManager] Firebase sign-in ok. uid={user.UserId}");

                var profile = new UserProfile { DisplayName = pendingNickname };
                user.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(_ =>
                {
                    ApplyPhotonIdentityAndConnect(user.UserId, pendingNickname);
                });
            });
        }

        private void ApplyPhotonIdentityAndConnect(string uid, string nickname)
        {
            PhotonNetwork.NickName = nickname;
            PhotonNetwork.AuthValues = new AuthenticationValues(uid);

            var props = new Hashtable { { "uid", uid } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            Debug.Log($"[GuestLoginManager] Photon properties set: uid={uid}, nick={nickname}");

            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
                Debug.Log("[GuestLoginManager] Connecting to Photon...");
            }
            else
            {
                if (!PhotonNetwork.InLobby)
                    PhotonNetwork.JoinLobby();
            }
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("[GuestLoginManager] ConnectedToMaster.");
            SafeReapplyUid();
            
            if (user == null)
            {                   // 아직 Firebase 로그인 전
                Debug.Log("[GuestLoginManager] Photon connected before auth; skip JoinLobby until user != null");
                return;
            }
            
            PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("[GuestLoginManager] JoinedLobby.");
            SafeReapplyUid();
            
            if (user == null) return;

            if (!PhotonNetwork.InRoom)
                PhotonNetwork.JoinOrCreateRoom("HUB-LOBBY",
                    new RoomOptions { MaxPlayers = 8, IsOpen = true, IsVisible = false }, TypedLobby.Default);
        }

        public override void OnJoinedRoom()
        {
            Debug.Log("[GuestLoginManager] JoinedRoom.");
            SafeReapplyUid();
            
            if (user == null) return; // 로그인 전이면 씬 로드 금지

            // 룸 입장 후 모든 Photon 플레이어를 등록해 GamePlayer 생성 보장
            //PlayerManager.Instance.EnsureAllPhotonPlayersRegistered(); // 파일에 이미 구현됨:contentReference[oaicite:0]{index=0}

            // 씬 전환(권장: MasterClient가 공용 씬 로드)
            if (loadLobbyOnJoinedRoom && PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"[GuestLoginManager] MasterClient loading lobby scene: {lobbySceneName}");
                PhotonNetwork.LoadLevel(lobbySceneName);
            }
            
            var hasUidProp = PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("uid", out var uv) && uv is string us && !string.IsNullOrEmpty(us);
            Debug.Log($"[GuestLoginManager] UID present? props:{hasUidProp}, value:{(hasUidProp ? uv : "null")}");
        }

        // 예: 특정 시점(로비 UI에서 “게임시작” 버튼)에서 실제 게임 씬으로 전환
        public void LoadGameplaySceneForAll()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogWarning("[GuestLoginManager] Only MasterClient can load gameplay scene.");
                return;
            }
            if (string.IsNullOrEmpty(gameplaySceneName))
            {
                Debug.LogWarning("[GuestLoginManager] gameplaySceneName is empty.");
                return;
            }
            Debug.Log($"[GuestLoginManager] MasterClient loading gameplay scene: {gameplaySceneName}");
            PhotonNetwork.LoadLevel(gameplaySceneName); // 전원 동기화
        }

        private void SafeReapplyUid()
        {
            try
            {
                if (user == null) return;
                var current = PhotonNetwork.LocalPlayer?.CustomProperties;
                var hasUid = current != null && current.ContainsKey("uid") && current["uid"] is string s && !string.IsNullOrEmpty(s);
                if (!hasUid)
                {
                    var props = new Hashtable { { "uid", user.UserId } };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                    if (!string.IsNullOrEmpty(pendingNickname))
                        PhotonNetwork.NickName = pendingNickname;

                    Debug.Log("[GuestLoginManager] Reapplied uid/nick to LocalPlayer.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GuestLoginManager] SafeReapplyUid error: {e.Message}");
            }
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            isConnecting = false;
            Debug.LogWarning($"[GuestLoginManager] Disconnected: {cause}");
        }

        public override void OnCustomAuthenticationFailed(string debugMessage)
        {
            Debug.LogWarning($"[GuestLoginManager] CustomAuth failed: {debugMessage}");
        }
    }
}
