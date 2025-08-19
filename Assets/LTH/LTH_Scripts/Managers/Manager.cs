using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers
{
    public static class Manager
    {
        // 역할: 매니저 생성/등록만 담당
        // public static FirebaseManager Firebase => FirebaseManager.Instance;
        // public static NetworkManager Network => NetworkManager.Instance;
        // public static SoundManager Sound => SoundManager.Instance;
        // public static UIManager UI => UIManager.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // 전역 매니저만 생성
            var manager = Object.Instantiate(Resources.Load<GameObject>("Prefabs/@Manager"));
            Object.DontDestroyOnLoad(manager);

           // manager.AddComponent<FirebaseManager>();
           // manager.AddComponent<NetworkManager>();
           // manager.AddComponent<SoundManager>();
           // manager.AddComponent<UIManager>();

            // 씬별 게임 매니저는 각 씬에서 자체적으로 생성
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 씬이 이동될 때 초기화가 또 필요한 부분이 있을 것 같아서 작성해둔 메서드 (선택)
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {

        }
    }
}