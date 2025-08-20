using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace KYG.Auth
{
    /// <summary>
    /// Firebase SDK 의존성을 확인하고 초기화하는 컴포넌트.
    /// - 가장 먼저 실행되는 객체로 두고 DontDestroyOnLoad 처리.
    /// - 다른 시스템은 FirebaseInit.IsReady 플래그로 초기화 완료를 확인.
    /// </summary>
    
    public class FirebaseInit : MonoBehaviour
    {
        /// <summary>
        /// 파이어베이스 의존성(Play Services 등) 준비 완료 여부.
        /// </summary>
        
        public static bool IsReady { get; private set; }

        async void Awake()
        {
            DontDestroyOnLoad(gameObject);
            // 씬전환 시에도 유지
            await InitializeFirebase();
            // Firebase 의존성 체크 및 준비
        }

        /// <summary>
        /// Firebase 의존성 자동 설치/수정(체크) 후 사용 준비
        /// </summary>
        
        private async Task InitializeFirebase()
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            // CheckAndFixDependenciesAsync: 런타임에서 필요한 의존 요소를 점검하고 가능한 경우 자동 수정
            
            if (status == DependencyStatus.Available)
            {
                IsReady = true;
                Debug.Log("[Firebase] Ready");
            }
            else
            {
                // 에디터/디바이스에 Google Play Services 등 누락 시 여기서 실패할 수 있음
                IsReady = false;
                Debug.LogError($"[Firebase] Dependencies not available: {status}");
            }
        }
    }
}