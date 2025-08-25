using System.Collections;
using System.Collections.Generic;
using LDH_UI;
using Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers
{
    public static class Manager
    {
        public static PlayerManager Player => PlayerManager.Instance;           // PlayerManager
        public static UIManager UI => UIManager.Instance;                       // UI
        public static NetworkManager Network => NetworkManager.Instance;        // Network

        public static CameraManager Camera => CameraManager.Instance;         // CameraManager

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var manager = Object.Instantiate(Resources.Load<GameObject>("Prefabs/@Manager"));
            Object.DontDestroyOnLoad(manager);

            manager.AddComponent<PlayerManager>();
            manager.AddComponent<UIManager>();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {

        }
    }
}