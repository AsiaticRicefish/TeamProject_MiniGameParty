using UnityEngine;
using Firebase;
using Firebase.Database;
// Firestore를 임포트했다면 아래 using도 켜두세요.
// using Firebase.Firestore;

public class FirebaseBootstrap : MonoBehaviour
{
    [Header("Realtime DB URL (콘솔 URL 그대로)")]
    [SerializeField] private string databaseUrl =
        "https://unimo-56ebc-default-rtdb.asia-southeast1.firebasedatabase.app";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void PreInit() { /* 여유: 다른 시스템보다 먼저 올라오게 */ }

    private async void Awake()
    {
        DontDestroyOnLoad(gameObject);

        var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dep != DependencyStatus.Available)
        {
            Debug.LogError($"[FirebaseBootstrap] Dependencies not available: {dep}");
            return;
        }

        var app = FirebaseApp.DefaultInstance;

        // (중요) Realtime DB URL을 "가장 먼저" 고정
        var db = FirebaseDatabase.GetInstance(app, databaseUrl);

        // 에디터/로컬에서는 퍼시스턴스 끔 (문제 회피)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        try
        {
            db.SetPersistenceEnabled(false);
        }
        catch { /* 이미 사용 중이면 무시 */ }
#endif

        // Firestore 패키지를 쓴다면(쓰지 않으면 생략):
        // try
        // {
        //     var fs = Firebase.Firestore.FirebaseFirestore.DefaultInstance;
        //     var s  = fs.Settings;
        //     s.PersistenceEnabled = false;     // 로컬 퍼시스턴스 OFF
        //     fs.Settings = s;
        // }
        // catch {}

        Debug.Log("[FirebaseBootstrap] Initialized. Persistence OFF in Editor/Dev.");
    }
}