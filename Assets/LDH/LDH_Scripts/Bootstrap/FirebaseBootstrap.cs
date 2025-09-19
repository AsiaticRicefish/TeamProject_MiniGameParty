using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Database;
using Firebase.Firestore;
using UnityEngine;

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
            
            App = FirebaseApp.DefaultInstance;
            
            //RTDB 
            Rtdb = FirebaseDatabase.GetInstance(rtdbUrl);
            if (rtdbPersistence)
                Rtdb.SetPersistenceEnabled(true); // 반드시 DB 사용 전 단계에서 호출
            Root = Rtdb.RootReference;

            // Firestore
            Firestore = FirebaseFirestore.DefaultInstance;
            Firestore.Settings.PersistenceEnabled = true;

            Debug.Log("[FirebaseBootstrap] RTDB & Firestore ready.");

        }
    }
}