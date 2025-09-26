using Photon.Pun;
using UnityEngine;

namespace YG
{
    
    /// <summary>
    /// Photon 기반 싱글톤.
    /// - 씬에 하나만 존재
    /// - DontDestroyOnLoad 적용 가능
    /// </summary>
    public class PunSingleton<T> : MonoBehaviourPunCallbacks where T : MonoBehaviour
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<T>();
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                // DontDestroyOnLoad(gameObject); // 필요시 주석 해제
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[PunSingleton] Duplicate {typeof(T).Name} destroyed.");
                Destroy(gameObject);
            }
        }
    }
}