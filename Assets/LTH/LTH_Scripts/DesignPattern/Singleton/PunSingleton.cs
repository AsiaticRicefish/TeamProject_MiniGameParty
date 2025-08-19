using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Photon 콜백 수신이 가능한 싱글톤 (DontDestroyOnLoad 여부 선택 가능)
/// </summary>

namespace DesignPattern
{
    public class PunSingleton<T> : MonoBehaviourPunCallbacks where T : MonoBehaviourPunCallbacks
    {
        private static T _instance;

        [Tooltip("true: 씬 이동 시 유지 / false: 씬마다 새로 생성됨")]
        [SerializeField]
        private bool isPersistent = true;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;

            if (isPersistent)
            {
                DontDestroyOnLoad(gameObject);
            }
            OnAwake();
        }

        protected virtual void OnAwake() { }

        public static void Release()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}