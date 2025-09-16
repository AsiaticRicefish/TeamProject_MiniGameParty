using System;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Managers;
using UnityEngine;

public class AuthBootstrapper : MonoBehaviour
{
    [Header("Firebase Realtime DB URL (콘솔에서 복사)")]
    [SerializeField] private string databaseUrl = "https://<your-project-id>.firebaseio.com";

    [Header("Logs")]
    [SerializeField] private bool verbose = true;

    private static bool s_DbConfigured; // DB 초기화(퍼시스턴스+URL)를 딱 1회만 수행하도록 막는 가드


    private async void Awake()
    {
        DontDestroyOnLoad(gameObject);

       // 1) GPGS 활성화
       TryActivateGPGS();
       
       // 2) Firebase 준비 & Realtime DB URL 바인딩 (닉네임 예약 기능을 위해 필수)
       try
       {
           // 2-1. Firebase 의존성 확인 및 수정
           await EnsureFirebaseDependenciesAsync();
           
           // 2-2. RTDB 인스턴스에 URL 바인딩 + 오프라인 퍼시스턴스 ON (가장 먼저 1회)
           await ConfigureRealtimeDatabaseAsync();
           
           //2-3. DB를 참조/사용하는 유틸을 호출
           // 우리의 닉네임 중복 트랜잭션 유틸은 반드시 이 바인딩이 선행되어야 함
           NicknameRegistry.ConfigureDatabase(databaseUrl);
           
           // 4) Auth 핸들 확인
           var auth = FirebaseAuth.DefaultInstance;
           if (verbose) Debug.Log($"[AuthBootstrapper] FirebaseAuth ready? {(auth != null)}");
       }
       catch (Exception e)
       {
           Debug.LogException(e);
       }
    }

    
    private void TryActivateGPGS()
    {
        try
        {
            GooglePlayGames.PlayGamesPlatform.Activate();
            if (verbose) Debug.Log("[AuthBootstrapper] GPGS Activate()");
        }
        catch
        {
            Debug.LogWarning("[AuthBootstrapper] GPGS Activate 실패 또는 미설치 의심(에디터/패키지 확인).");
        }
    }

    /// <summary>
    /// Firebase 런타임 의존성(Play Services 등) 점검 및 자동 수정.
    /// </summary>
    private async UniTask EnsureFirebaseDependenciesAsync()
    {
        var deps = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (deps != DependencyStatus.Available)
            throw new Exception($"Firebase deps not available: {deps}");
    }
    
    
    /// <summary>
    /// RTDB URL에 바인딩된 인스턴스를 생성하고,
    /// '최초 사용 전에' 퍼시스턴스를 켠다.
    /// 이 메서드는 앱 생애주기 동안 단 1회만 실행되어야 한다.
    /// </summary>
    async UniTask ConfigureRealtimeDatabaseAsync()
    {
        if (s_DbConfigured) return;

        if (string.IsNullOrWhiteSpace(databaseUrl) || !databaseUrl.StartsWith("https://"))
            throw new Exception("[AuthBootstrapper] Realtime DB URL이 올바르지 않습니다.");

        // URL로 지정된 DB 인스턴스를 먼저 확보
        var db = FirebaseDatabase.GetInstance(databaseUrl);

#if !UNITY_WEBGL || UNITY_EDITOR
        db.SetPersistenceEnabled(true);
#else
        if (verbose) Debug.Log("WebGL 빌드에서는 RTDB persistence 미지원 → 스킵");
#endif
        // BackendManager에 바인딩
        await UniTask.WaitUntil(() => BackendManager.Instance != null);
        BackendManager.BindDataBase(databaseUrl, db);
        
        s_DbConfigured = true;
        await UniTask.Yield();
        if (verbose) Debug.Log("[AuthBootstrapper] RTDB configured (URL + persistence)");
    }
}
