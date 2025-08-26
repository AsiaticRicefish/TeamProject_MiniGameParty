using System;
using System.Collections;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KYG.Auth
{
    /// <summary>
    /// 닉네임만 받아 Firebase 익명 로그인 + Photon 접속 + 로비 씬 전환까지 처리
    /// - Firebase 의존성 체크 완료 전에는 어떠한 Firebase API도 호출하지 않음(예외 방지)
    /// - Photon 연결 후 Master → Lobby Join → 자동 씬 이동(옵션)
    /// </summary>
    public class GuestLoginManager : MonoBehaviourPunCallbacks
    {
        public static GuestLoginManager Instance { get; private set; }

        [Header("Photon")]
        [SerializeField] private string defaultRegion = "asia"; // "asia" 권장

        [Header("Scene")]
        [SerializeField] private bool autoLoadLobbyScene = true;     // 로비 참여 시 자동 로비 씬 이동
        [SerializeField] private string lobbySceneName = "LDH_MainScene";

        private FirebaseAuth _auth;
        private FirebaseUser _user;

        public bool IsFirebaseReady { get; private set; }
        public bool IsPhotonConnected => PhotonNetwork.IsConnected;

        // 중복 씬 로딩 방지
        private bool _isLoadingScene = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // ✅ Firebase 의존성 확인 및 초기화
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    _auth = FirebaseAuth.DefaultInstance;
                    IsFirebaseReady = true;
                    Debug.Log("[GuestLogin] Firebase ready.");
                }
                else
                {
                    Debug.LogError($"[GuestLogin] Firebase dependencies not available: {task.Result}");
                }
            });

            // Photon 설정
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.GameVersion = Application.version; // 같은 버전끼리 매칭
        }

        /// <summary>
        /// 외부(UI)에서 닉네임을 받아 전체 로그인 플로우 실행
        /// Firebase 준비 완료까지 잠깐 대기하여 "CheckDependencies running" 예외를 방지.
        /// </summary>
        public async void LoginAsGuestWithNickname(string nickname)
        {
            // ✅ Firebase 준비될 때까지 대기(최대 6초)
            int guard = 0;
            while (!IsFirebaseReady && guard < 120)
            {
                await Task.Delay(50);
                guard++;
            }
            if (!IsFirebaseReady)
            {
                Debug.LogError("[GuestLogin] Firebase not ready yet (timeout).");
                return;
            }

            // 닉네임 간단 검증
            nickname = SanitizeNickname(nickname);
            if (string.IsNullOrEmpty(nickname))
            {
                Debug.LogError("[GuestLogin] Invalid nickname.");
                return;
            }

            try
            {
                // Firebase 익명 로그인 (이미 로그인 상태면 재사용)
                _user = _auth.CurrentUser;
                if (_user == null)
                {
                    var result = await _auth.SignInAnonymouslyAsync(); // AuthResult
                    _user = result.User; // 실제 FirebaseUser
                    Debug.Log($"[GuestLogin] Firebase anonymous UID: {_user.UserId}");
                }
                else
                {
                    Debug.Log($"[GuestLogin] Reuse Firebase UID: {_user.UserId}");
                }

                // Firebase 프로필에 닉네임 저장(선택)
                await SetFirebaseDisplayNameIfNeeded(_user, nickname);

                // Photon 접속 설정
                PhotonNetwork.NickName = nickname;                       // 방/게임 내 표시명
                PhotonNetwork.AuthValues = new AuthenticationValues(_user.UserId); // 고유 식별자(UID)

                // 리전 고정(원하면 주석 해제/조정)
                var settings = PhotonNetwork.PhotonServerSettings.AppSettings;
                settings.FixedRegion = defaultRegion; // "asia" 권장
                // settings.BestRegionSummaryFromStorage = null; // 베스트 리전 저장치 무시

                if (!PhotonNetwork.IsConnected)
                {
                    PhotonNetwork.ConnectUsingSettings();
                    Debug.Log("[GuestLogin] Connecting to Photon...");
                }
                else
                {
                    Debug.Log("[GuestLogin] Already connected to Photon.");
                    // 이미 연결되어 있다면 바로 로비 참가 시도
                    PhotonNetwork.JoinLobby();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GuestLogin] Login failed: {e}");
            }
        }

        private string SanitizeNickname(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var trimmed = input.Trim();
            if (trimmed.Length > 16) trimmed = trimmed.Substring(0, 16);
            return trimmed;
        }

        private async Task SetFirebaseDisplayNameIfNeeded(FirebaseUser user, string nickname)
        {
            if (!string.IsNullOrEmpty(user.DisplayName) && user.DisplayName == nickname) return;
            var profile = new UserProfile { DisplayName = nickname };
            await user.UpdateUserProfileAsync(profile);
            await user.ReloadAsync(); // 캐시 갱신
            Debug.Log($"[GuestLogin] Firebase displayName set: {user.DisplayName}");
        }

        #region Photon Callbacks
        public override void OnConnectedToMaster()
        {
            Debug.Log("[GuestLogin] Photon Connected to Master.");
            PhotonNetwork.JoinLobby(); // 간단히 로비 참여
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogError($"[GuestLogin] Photon Disconnected: {cause}");
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("[GuestLogin] Joined Lobby.");
            if (autoLoadLobbyScene) LoadLobbySceneIfNeeded();
        }
        #endregion

        #region Scene Helpers
        /// <summary>
        /// 현재 씬이 로비 씬이 아니고, 빌드에 등록되어 있다면 로비 씬으로 전환
        /// </summary>
        private void LoadLobbySceneIfNeeded()
        {
            if (_isLoadingScene) return;

            var active = SceneManager.GetActiveScene().name;
            if (active == lobbySceneName)
            {
                Debug.Log("[GuestLogin] Already in Lobby scene.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
            {
                Debug.LogError($"[GuestLogin] Lobby scene '{lobbySceneName}' is not in Build Settings.");
                return;
            }

            StartCoroutine(CoLoadScene(lobbySceneName));
        }

        private IEnumerator CoLoadScene(string sceneName)
        {
            _isLoadingScene = true;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            _isLoadingScene = false;
        }
        #endregion
    }
}
