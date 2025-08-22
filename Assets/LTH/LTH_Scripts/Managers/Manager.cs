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
        // ����: �Ŵ��� ����/��ϸ� ���
        // public static FirebaseManager Firebase => FirebaseManager.Instance;
        // public static NetworkManager Network => NetworkManager.Instance;
        // public static SoundManager Sound => SoundManager.Instance;
        public static UIManager UI => UIManager.Instance;           // UI
        public static NetworkManager Network => NetworkManager.Instance;        // Network

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // ���� �Ŵ����� ����
            var manager = Object.Instantiate(Resources.Load<GameObject>("Prefabs/@Manager"));
            Object.DontDestroyOnLoad(manager);

           // manager.AddComponent<FirebaseManager>();
           // manager.AddComponent<SoundManager>();
           
           manager.AddComponent<UIManager>();
           
           // manager.AddComponent<NetworkManager>();   // NetworkManager는 프로퍼티 설정이 필요해서 Prefab에 직접 추가함

            // ���� ���� �Ŵ����� �� ������ ��ü������ ����
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // ���� �̵��� �� �ʱ�ȭ�� �� �ʿ��� �κ��� ���� �� ���Ƽ� �ۼ��ص� �޼��� (����)
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {

        }
    }
}