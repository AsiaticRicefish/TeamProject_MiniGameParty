using System;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Managers;
using UnityEngine;
using Network;
using System.Threading.Tasks;
using LDH_Game;


namespace KYG.Auth
{
    public class GuestLoginManager : MonoBehaviourPunCallbacks
    {
        public static GuestLoginManager Instance { get; private set; }

        [Header("Firebase")]
        [Tooltip("Firebase Console > Realtime Database > 데이터베이스 URL")]
        [SerializeField] private string databaseUrl =
            "https://unimo-56ebc-default-rtdb.asia-southeast1.firebasedatabase.app"; // 프로젝트 URL로 교체
        
        [Header("Photon")]
        [SerializeField] private string defaultRegion = "asia";

        //[Header("Scene Names")]
        //[SerializeField] private string lobbySceneName = "LDH_MainScene";            // 로비(대기) 씬 이름
        //[SerializeField] private string gameplaySceneName = "PMS_ShootingTestScene"; // 실제 게임 씬 (예시)

        //[Header("Flow Options")]
        //[SerializeField] private bool loadLobbyOnJoinedRoom = true; // 룸 입장 시 로비씬 자동 로드

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
            //DontDestroyOnLoad(gameObject);

            
        }

        private void Start()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                var status = task.Result;
                if (status == DependencyStatus.Available)
                {
                    // Auth 준비
                    auth = FirebaseAuth.DefaultInstance;
                    // 여기서 DB를 바인딩 (URL은 인스펙터/직접 입력)
                    NicknameRegistry.ConfigureDatabase(databaseUrl);
                    
                    IsFirebaseReady = true;
                    Debug.Log("[GuestLoginManager] Firebase ready.");
                }
                else
                {
                    IsFirebaseReady = false;
                    Debug.LogError($"[GuestLoginManager] Firebase dependencies not available: {status}");
                }
            });

            PhotonNetwork.AutomaticallySyncScene = true; // 모든 씬 전환은 마스터가 동기화
            if (!string.IsNullOrEmpty(defaultRegion))
                PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = defaultRegion;
        }
        
        /// <summary>
        /// 게스트 로그인 + 닉네임 전역 예약 + Photon 접속 총괄
        /// - Firebase 의존성 확인 → Auth 준비
        /// - (중요) Realtime DB URL 강제 설정 (Editor/Standalone에서 필수)
        /// - 닉네임 전역 중복 체크: Realtime DB 트랜잭션(닉네임 예약)
        /// - OnDisconnect 예약/수동 해제 처리
        /// - Photon에 NickName/AuthValues 설정 후 접속
        /// - 로비 입장 후 uid CustomProperty는 NetworkManager에서 보정
        /// </summary>
        
        public async void CancelPendingLogin()
        {
            // 로딩 플래그 해제
            isConnecting = false;

            // 1) 닉네임 예약 해제(소유자일 때만)
            try
            {
                if (!string.IsNullOrEmpty(pendingNickname) && user != null)
                {
                    bool released = await NicknameRegistry.ReleaseIfOwnerAsync(user.UserId, pendingNickname);
                    Debug.Log($"[GuestLoginManager] Cancel: release '{pendingNickname}' owner={released}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GuestLoginManager] Cancel release exception: {e.Message}");
            }

            // 2) Photon 연결 중이면 끊기
            try
            {
                // 단순히 연결되어 있거나 연결 준비 중이면 Disconnect 호출
                if (PhotonNetwork.IsConnected || PhotonNetwork.IsConnectedAndReady)
                {
                    Debug.Log("[GuestLoginManager] Cancel: PhotonNetwork.Disconnect()");
                    PhotonNetwork.Disconnect();
                }
            }
            catch { /* ignore */ }

            // 3) UI 복구
            SetUILock(false);
            var ui = FindObjectOfType<GuestLoginUI>();
            if (ui != null)
            {
                ui.EnableNicknameRetry(); // 인풋 비우고 포커스
                ui.SafeSetHint("취소했습니다. 닉네임을 다시 입력하거나 뒤로 가세요.");
                ui.ShowSubmittingUI(false); // 로딩 off, 입력 on
                ui.ReturnToFirstIfWanted(); // (옵션) 처음 화면으로 돌아가기
            }

            // 4) 내부 상태 초기화
            pendingNickname = null;
        }

        
        /// <summary>
        /// Realtime Database URL을 명확히 지정 (Editor/Standalone 테스트에서 필수)
        /// </summary>
        private void SetupDatabaseUrl(FirebaseApp app)
        {
            try
            {
                if (app == null)
                {
                    Debug.LogError("[GuestLoginManager] FirebaseApp is null.");
                    return;
                }

                var current = app.Options?.DatabaseUrl?.ToString();
                if (string.IsNullOrEmpty(current))
                {
                    if (string.IsNullOrEmpty(databaseUrl) || !databaseUrl.StartsWith("https://"))
                    {
                        Debug.LogError("[GuestLoginManager] databaseUrl이 비어있거나 형식이 잘못되었습니다. Firebase 콘솔의 URL을 입력하세요.");
                        return;
                    }
                    app.Options.DatabaseUrl = new Uri(databaseUrl);
                    Debug.Log($"[GuestLoginManager] DatabaseUrl set: {app.Options.DatabaseUrl}");
                }
                else
                {
                    Debug.Log($"[GuestLoginManager] DatabaseUrl already set: {current}");
                }

                // 인스턴스 강제 확보 (널 가드 겸)
                var _ = FirebaseDatabase.GetInstance(app);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GuestLoginManager] SetupDatabaseUrl error: {e.Message}");
            }
        }
        
        
        /// <summary>
        /// 닉네임으로 게스트 로그인 시도
        /// </summary>
        
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

            // (UX) 현재 로컬 로비/룸에 동일 닉 있으면 즉시 경고만 출력 (최종 판정은 DB가 함)
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (string.Equals(p.NickName, nickname, StringComparison.OrdinalIgnoreCase))
                {
                    PromptRetry("중복 닉네임입니다. 다른 이름을 입력해주세요.");
                    return;
                }
            }
            
            isConnecting = true;
            pendingNickname = nickname;
            SetUILock(true); // 입력 잠금(스피너 등)
            
            // 1) 익명 로그인
            auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(async t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    isConnecting = false;
                    Debug.LogError($"[GuestLoginManager] Firebase anonymous sign-in failed: {t.Exception}");
                    return;
                }

                user = t.Result.User;
                Debug.Log($"[GuestLoginManager] Firebase sign-in ok. uid={user.UserId}");
                
                _ = SessionEnforcer.Instance?.StartForUidAsync(user.UserId);

                // 2) 닉네임 전역 예약 (트랜잭션 + 재시도 + 타임아웃 내장됨)
                Debug.Log($"[GuestLoginManager] Reserve START nick={pendingNickname}, uid={user.UserId}");
                bool reserved;
                try
                {
                    reserved = await NicknameRegistry.ReserveStrictAsync
                        (user.UserId, pendingNickname, maxRetry: 3, timeoutMs: 5000);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GuestLoginManager] Reserve EXCEPTION: {ex}");
                    reserved = false;
                    // 바로 재입력 유도
                    isConnecting = false;
                    PromptRetry("중복 닉네임입니다. 다른 이름으로 다시 시도하세요.");
                    return;
                }
                Debug.Log($"[GuestLoginManager] Reserve DONE nick={pendingNickname}, uid={user.UserId}, result={reserved}");

                // 예약 실패 분기
                if (!reserved)
                {
                    isConnecting = false;
                    // 화면에 문구 표시 + 입력창 복구
                    SetUILock(false);
                    var ui = FindObjectOfType<GuestLoginUI>();
                    if (ui != null)
                    {
                        ui.ShowErrorHint("중복 닉네임입니다. 다른 이름으로 다시 시도하세요."); // ← 빨간 글씨/토스트
                        ui.EnableNicknameRetry();  // 인풋 비움 + 포커스
                    }
                    pendingNickname = null;
                    return;
                }

                // 3) 연결 끊기면 자동 정리 예약 + 닉네임 프로필 반영 → Photon 접속
                await NicknameRegistry.BindOnDisconnectCleanupAsync(pendingNickname);
                var profile = new UserProfile { DisplayName = pendingNickname };
                user.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(_ =>
                {
                    ApplyPhotonIdentityAndConnect(user.UserId, pendingNickname);
                });
            });
        }

        
        
        
        private void WarnDuplicateNicknameUI()
        {
            Debug.LogWarning("중복 닉네임입니다.");
            var ui = FindObjectOfType<GuestLoginUI>();
            if (ui != null) ui.SendMessage("SafeSetHint", "중복 닉네임입니다.", SendMessageOptions.DontRequireReceiver);
        }
        
        /// <summary>
        /// 앱 종료 시 닉네임 해제
        /// </summary>
        private async void OnApplicationQuit()
        {
            if (!string.IsNullOrEmpty(pendingNickname) && user != null)
                await NicknameRegistry.ReleaseIfOwnerAsync(user.UserId, pendingNickname);
        }

        
        private async System.Threading.Tasks.Task FailAndRetry(string reason)
        {
            if (!string.IsNullOrEmpty(pendingNickname) && user != null)
                await NicknameRegistry.ReleaseIfOwnerAsync(user.UserId, pendingNickname);

            isConnecting = false;
            PromptRetry(reason);
        }
        
        private void PromptRetry(string hint)
        {
            SetUILock(false); // 입력 다시 활성화
            var ui = FindObjectOfType<GuestLoginUI>();
            if (ui != null)
            {
                ui.SafeSetHint(hint);   // “중복 닉네임입니다 …”
                ui.EnableNicknameRetry(); // 입력창 비우고 포커스
            }
            pendingNickname = null; // 상태 정리
        }

        private void SetUILock(bool on)
        {
            var ui = FindObjectOfType<GuestLoginUI>();
            if (ui != null) ui.SetInteractable(!on); // 버튼/입력창 잠금 토글
        }
        
        /// <summary>
        /// Photon 인증 정보 세팅 후 서버 연결 시작
        /// </summary>
        public void ApplyPhotonIdentityAndConnect(string uid, string nickname)
        {
            PhotonNetwork.NickName = nickname;
            PhotonNetwork.AuthValues = new AuthenticationValues(uid);

            // Debug 로그 추가
            Debug.Log($"[GuestLogin] Firebase UID={uid}, Nickname={PhotonNetwork.NickName}");
            Debug.Log($"[GuestLogin] Photon.AuthValues.UserId={PhotonNetwork.AuthValues?.UserId}");

            //game 리소스 다운 / 초기화 및 파이어베이스 데이터 로드 진행 후 서버로 연결하기 위해 game boot strap을 생성한다.(bootstrap 이 완료되면 자동으로 서버연결이 됩니다)
            GameObject gameBootstrap = new GameObject("Game Bootstrap", typeof(GameBootstrap));
            
            
            
            // if (!PhotonNetwork.IsConnected)
            // {
            //    // PhotonNetwork.ConnectUsingSettings();
            //     Debug.Log("[GuestLogin] Connecting to Photon...");
            // }
            // else if (!PhotonNetwork.InLobby)
            // {
            //     //PhotonNetwork.JoinLobby();
            // }
            //
            // var nm = NetworkManager.Instance;
            // if (nm != null)
            // {
            //     nm.ConnectedToMaster += () =>
            //     {
            //         Debug.Log("[GuestLogin] Handing off to NetworkManager");
            //         //nm.ConnectServer();
            //     };
            // }
        }

        // public override void OnConnectedToMaster()
        // {
        //     Debug.Log("[GuestLoginManager] ConnectedToMaster.");
        //     //SafeReapplyUid();
        //     
        //     if (user == null)
        //     {                   // 아직 Firebase 로그인 전
        //         Debug.Log("[GuestLoginManager] Photon connected before auth; skip JoinLobby until user != null");
        //         return;
        //     }
        //     
        //  
        //     if (user == null) return; // Firebase 로그인 전이면 패스
        //     
        //     
        //     // Photon 기본 로비 들어가기
        //     // if (PhotonNetwork.InLobby || PhotonNetwork.NetworkClientState == ClientState.JoiningLobby) return;  // 방어로직 추가
        //     //     PhotonNetwork.JoinLobby();
        // }
        
        // ----- NetworkManager로 기능 통합 ----- 0829(이도현)
        // public override void OnJoinedLobby()
        // {
        //     Debug.Log("[GuestLoginManager] JoinedLobby.");
        //     SafeReapplyUid();
        //     
        //     if (user == null) return;
        //
        //     // if (!PhotonNetwork.InRoom)
        //     //     PhotonNetwork.JoinOrCreateRoom("HUB-LOBBY",
        //     //         new RoomOptions { MaxPlayers = 8, IsOpen = true, IsVisible = false }, TypedLobby.Default);
        //
        //     //로비 씬으로 이동
        //     SceneManager.LoadScene(lobbySceneName);
        // }

        /*public override void OnJoinedRoom()
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
        }*/

        // 예: 특정 시점(로비 UI에서 “게임시작” 버튼)에서 실제 게임 씬으로 전환
        /*public void LoadGameplaySceneForAll()
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
        }*/

        /// <summary>
        /// (보강) 로비/룸 진입 타이밍에 uid 유실 시 재주입하고 싶다면 사용
        /// </summary>
        private void SafeReapplyUid()
        {
            try
            {
                if (user == null) return;
                var current = PhotonNetwork.LocalPlayer?.CustomProperties;
                var hasUid = current != null &&
                             current.ContainsKey("uid") &&
                             current["uid"] is string s &&
                             !string.IsNullOrEmpty(s);
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
