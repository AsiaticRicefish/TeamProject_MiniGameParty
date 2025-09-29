using System;
using System.Collections;
using System.Collections.Generic;
using Customization;
using Cysharp.Threading.Tasks;
using Data;
using Firebase.Database;
using LDH_UI;
using Network;
using Store;
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

        public static DataManager Data => DataManager.Instance;             // Data
        public static PurchaseManager Purchase => PurchaseManager.Instance;     //Purchase
        
        public static ParticleManager Particle => ParticleManager.Instance;     // Particle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // 멀티 터치 막기
            Input.multiTouchEnabled = false;
            
            //프레임 설정
            Application.targetFrameRate = 65;
            
            var manager = Object.Instantiate(Resources.Load<GameObject>("Prefabs/@Manager"));
            Object.DontDestroyOnLoad(manager);

            manager.AddComponent<DataManager>();
            manager.AddComponent<PlayerManager>();
            manager.AddComponent<UIManager>();
            manager.AddComponent<CameraManager>();
            manager.AddComponent<PurchaseManager>();


            SceneManager.sceneLoaded += OnSceneLoaded;
            
            
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            
        }
        
    }
}