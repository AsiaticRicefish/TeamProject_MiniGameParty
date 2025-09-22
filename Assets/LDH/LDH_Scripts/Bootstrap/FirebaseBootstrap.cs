using System;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Database;
using Firebase.Firestore;
using UnityEngine;
using System.Linq; 

namespace LDH_Game
{
    public static class FirebaseBootstrap
    {
        public static FirebaseApp App { get; private set; }
        public static FirebaseDatabase Rtdb { get; private set; }
        public static DatabaseReference Root { get; private set; }
        public static FirebaseFirestore Firestore { get; private set; }
        
        public static async UniTask InitializeAsync(string rtdbUrl, bool rtdbPersistence = true, bool fsPersistence = true)
        {
            // FirebaseApp 의존성 확인
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError($"[FirebaseBootstrap] deps: {dependencyStatus}");
                return;
            }

            // --- 1) 에디터 클론/두 번째 에디터 구분용 앱 이름 만들기 ---
            // ParrelSync를 쓰면 클론 구분자를 붙이고, 아니어도 에디터에서 임의 suffix로 분리 가능.
            string appName = "MainApp";
#if UNITY_EDITOR
            try
            {
                // ParrelSync가 있으면
                var t = Type.GetType("ParrelSync.ClonesManager, ParrelSync");
                if (t != null && (bool)t.GetMethod("IsClone")!.Invoke(null, null))
                {
                    var arg = (string)t.GetMethod("GetArgument")!.Invoke(null, null);
                    appName = $"MainApp_clone_{arg}";
                }
                else
                {
                    // ParrelSync가 없어도, 에디터 2개를 동시에 띄우는 상황 대비해 프로세스 ID를 suffix로 사용
                    appName = $"MainApp_editor_{System.Diagnostics.Process.GetCurrentProcess().Id}";
                }
            }
            catch { /* 무시해도 됨 */ }
#endif
            // --- 2) DefaultInstance의 옵션을 복사해서 같은 프로젝트 설정으로 새 App 생성/재사용 ---
            var defaultApp = FirebaseApp.DefaultInstance;                // 이미 만들어져 있을 수 있음
            var def = defaultApp.Options;

            var opts = new AppOptions
            {
                AppId       = def.AppId,
                ApiKey      = def.ApiKey,
                ProjectId   = def.ProjectId,
                MessageSenderId = def.MessageSenderId,
                StorageBucket     = def.StorageBucket,
                DatabaseUrl = new Uri(rtdbUrl) // RTDB URL 명시
            };
            
            // 이미 동일 이름 App 있으면 재사용, 없으면 생성
            App = FirebaseApp.Create(opts, appName);
            
            //RTDB 
            Rtdb = FirebaseDatabase.GetInstance(rtdbUrl);
            if (rtdbPersistence)
                Rtdb.SetPersistenceEnabled(true); // 반드시 DB 사용 전 단계에서 호출
            Root = Rtdb.RootReference;

            // --- 4) Firestore 인스턴스: App 기반으로 얻고 'Settings를 먼저 세팅' ---
            Firestore = FirebaseFirestore.GetInstance(App);

            // 에디터에서 동시 실행 시 충돌 방지: 기본값 false 권장
#if UNITY_EDITOR          
            Firestore.Settings.PersistenceEnabled = false;
#else
            fsSettings.PersistenceEnabled = fsPersistence;
#endif
            Debug.Log("[FirebaseBootstrap] RTDB & Firestore ready.");

        }
    }
}