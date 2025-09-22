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

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // GPGS 활성화
        try
        {
            GooglePlayGames.PlayGamesPlatform.Activate();
            if (verbose) Debug.Log("[AuthBootstrapper] GPGS Activate()");
        }
        catch
        {
            Debug.LogWarning("[AuthBootstrapper] GPGS Activate 실패 또는 미설치 의심(에디터/패키지 확인).");
        }

        // 2) Firebase 준비 & Realtime DB URL 바인딩 (닉네임 예약 기능을 위해 필수)
        var app = FirebaseApp.DefaultInstance; // 없으면 내부 생성
        if (string.IsNullOrWhiteSpace(databaseUrl) || !databaseUrl.StartsWith("https://"))
        {
            Debug.LogError("[AuthBootstrapper] Realtime DB URL을 올바르게 설정하세요.");
        }
        else
        {
            // 우리의 닉네임 중복 트랜잭션 유틸은 반드시 이 바인딩이 선행되어야 함
            NicknameRegistry.ConfigureDatabase(databaseUrl);
        }

        // FirebaseAuth 핸들 확인 로그
        var auth = FirebaseAuth.DefaultInstance;
        if (verbose) Debug.Log($"[AuthBootstrapper] FirebaseAuth ready? {(auth != null)}");
    }
}
