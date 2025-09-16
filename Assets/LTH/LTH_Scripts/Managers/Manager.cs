using System;
using System.Collections;
using System.Collections.Generic;
using Customization;
using Cysharp.Threading.Tasks;
using LDH_UI;
using Network;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Managers
{
    public static class Manager
    {
        public static PlayerManager Player => PlayerManager.Instance;           // PlayerManager
        public static UIManager UI => UIManager.Instance;                       // UI
        public static NetworkManager Network => NetworkManager.Instance;        // Network

        public static CameraManager Camera => CameraManager.Instance;         // CameraManager

        public static CustomizationManager Custom => CustomizationManager.Instance;     // Customizing
        

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // 멀티 터치 막기
            Input.multiTouchEnabled = false;
            
            
            var manager = Object.Instantiate(Resources.Load<GameObject>("Prefabs/@Manager"));
            Object.DontDestroyOnLoad(manager);

            manager.AddComponent<PlayerManager>();
            manager.AddComponent<UIManager>();
            manager.AddComponent<CameraManager>();

            SceneManager.sceneLoaded += OnSceneLoaded;
            
            
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            
        }
        
    }
}