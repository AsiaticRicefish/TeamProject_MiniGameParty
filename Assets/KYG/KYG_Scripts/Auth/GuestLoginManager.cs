using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace KYG.Auth
{
    /// <summary>
    /// 닉네임만 받아 Firebase 익명 로그인 + Photon 접속까지 처리하는 매니저
    /// </summary>
    public class GuestLoginManager : MonoBehaviourPunCallbacks
    {
        public static GuestLoginManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private string defaultRegion = "asia"; // Photon Region (예: "asia", "kr"가 없으면 "asia" 권장)
        
        private FirebaseAuth _auth;
        private FirebaseUser _user;

        public bool IsFirebaseReady { get; private set; }
        public bool IsPhotonConnected => PhotonNetwork.IsConnected;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // 1) Firebase 의존성 확인 및 초기화
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

            // Photon 기본 옵션
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.GameVersion = Application.version; // 같은 버전끼리 매칭
        }

        /// <summary>
        /// 외부(UI)에서 닉네임을 받아 전체 로그인 플로우 실행
        /// </summary>
        public async void LoginAsGuestWithNickname(string nickname)
        {
            if (!IsFirebaseReady)
            {
                Debug.LogError("[GuestLogin] Firebase not ready yet.");
                return;
            }

            // 2) 닉네임 간단 검증
            nickname = SanitizeNickname(nickname);
            if (string.IsNullOrEmpty(nickname))
            {
                Debug.LogError("[GuestLogin] Invalid nickname.");
                return;
            }

            try
            {
                // 3) Firebase 익명 로그인 (이미 로그인 상태면 재사용)
                _user = _auth.CurrentUser;
                if (_user == null)
                {
                    var result = await _auth.SignInAnonymouslyAsync(); // AuthResult 리턴
                    _user = result.User; // 실제 FirebaseUser 가져오기
                    Debug.Log($"[GuestLogin] Firebase anonymous UID: {_user.UserId}");
                }
                else
                {
                    Debug.Log($"[GuestLogin] Reuse Firebase UID: {_user.UserId}");
                }

                // 4) Firebase 프로필에 닉네임 저장(선택)
                await SetFirebaseDisplayNameIfNeeded(_user, nickname);

                // 5) Photon 접속 설정
                PhotonNetwork.NickName = nickname;              // 방/게임 내 표시명
                PhotonNetwork.AuthValues = new AuthenticationValues(_user.UserId); // 고유 식별자(UID)

                // 6) 리전+세팅으로 접속
                var settings = PhotonNetwork.PhotonServerSettings.AppSettings;
                // 특정 리전을 강제하고 싶으면 아래 두 줄 사용
                settings.FixedRegion = defaultRegion; // "asia" 권장
                // settings.BestRegionSummaryFromStorage = null; // 베스트 리전 저장치 무시하고 고정 리전 사용

                if (!PhotonNetwork.IsConnected)
                {
                    PhotonNetwork.ConnectUsingSettings();
                    Debug.Log("[GuestLogin] Connecting to Photon...");
                }
                else
                {
                    Debug.Log("[GuestLogin] Already connected to Photon.");
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
            // 공백/제어문자 제거, 길이 제한 등 최소 필터
            var trimmed = input.Trim();
            if (trimmed.Length > 16) trimmed = trimmed.Substring(0, 16);
            return trimmed;
        }

        private async Task SetFirebaseDisplayNameIfNeeded(FirebaseUser user, string nickname)
        {
            // 이미 동일하면 스킵
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
            // 필요 시 로비 참여/빠른 매칭
            PhotonNetwork.JoinLobby(); // 간단히 로비 참여
            // 또는 바로 Quick Match 로직 호출
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogError($"[GuestLogin] Photon Disconnected: {cause}");
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("[GuestLogin] Joined Lobby.");
        }
        #endregion
    }
}
