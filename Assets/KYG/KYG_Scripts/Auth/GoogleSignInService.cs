#if GOOGLE_SIGNIN
using System.Threading.Tasks;
using Firebase.Auth;
using Google;
using UnityEngine;

namespace KYG.Auth
{
    /// <summary>
    /// Google Sign-In Unity 플러그인으로 구글 계정에 로그인하고,
    /// 획득한 ID 토큰으로 Firebase Auth에 Credential 로그인.
    /// - Android: Firebase 콘솔에 SHA-1/웹 클라이언트 ID 등록 필수
    /// - iOS: URL Types, REVERSED_CLIENT_ID 설정 필요
    /// </summary>
    public class GoogleSignInService : MonoBehaviour
    {
        [Header("Firebase Console의 OAuth 2.0 클라이언트(웹) ID")]
        [SerializeField] private string webClientId;

        // 구글 로그인 구성
        GoogleSignInConfiguration _config;

        void Awake()
        {
            // RequestIdToken: FirebaseAuth로 넘길 토큰(ID Token) 요청
            _config = new GoogleSignInConfiguration
            {
                WebClientId = webClientId,
                RequestEmail = true,
                RequestIdToken = true
            };
        }

        /// <summary>
        /// 구글 로그인 플로우 후 Firebase Auth로 Credential 로그인.
        /// </summary>
        public async Task<(bool ok, string error)> SignInWithGoogleAsync()
        {
            try
            {
                // 구성 적용
                GoogleSignIn.Configuration = _config;

                // 구글 계정 선택 → 사용자 동의 → 토큰 획득
                var user = await GoogleSignIn.DefaultInstance.SignIn();

                // Firebase Auth에 전달할 자격 증명 생성
                string idToken = user.IdToken;
                var cred = GoogleAuthProvider.GetCredential(idToken, null);

                // Firebase 로그인
                await FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(cred);

                return (true, null);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GoogleSignIn] {ex}");
                return (false, "구글 로그인 실패");
            }
        }
    }
}
#endif