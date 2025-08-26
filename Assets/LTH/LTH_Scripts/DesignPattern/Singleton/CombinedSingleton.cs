using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전역/씬 싱글톤 통합 구조
/// isPersistent = true → DontDestroyOnLoad 대상
/// isPersistent = false → 씬 종료 시 파괴됨
/// </summary>

namespace DesignPattern
{
    public class CombinedSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        [Tooltip("true: 씬 이동 시 유지 / false: 씬마다 새로 생성됨")]
        protected bool isPersistent = true;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 자동으로 씬에서 찾아서 할당
                    _instance = FindObjectOfType<T>();
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            // 게임오브젝트가 생성되면 자동으로 싱글톤 등록
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

            OnAwake(); // 선택 구현 가능
        }

        /// <summary>
        /// 필요한 경우 하위 클래스에서 Awake 이후 로직 구현
        /// </summary>
        protected virtual void OnAwake() { }

        /// <summary>
        /// 강제로 없애야 할 때 호출 (게임 종료나 로그아웃)
        /// <summary>
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