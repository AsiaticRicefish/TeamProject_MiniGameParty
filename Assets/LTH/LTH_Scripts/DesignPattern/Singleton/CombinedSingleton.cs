using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ����/�� �̱��� ���� ����
/// isPersistent = true �� DontDestroyOnLoad ���
/// isPersistent = false �� �� ���� �� �ı���
/// </summary>

namespace DesignPattern
{
    public class CombinedSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        [Tooltip("true: �� �̵� �� ���� / false: ������ ���� ������")]
        protected bool isPersistent = true;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    // �ڵ����� ������ ã�Ƽ� �Ҵ�
                    _instance = FindObjectOfType<T>();
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            // ���ӿ�����Ʈ�� �����Ǹ� �ڵ����� �̱��� ���
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

            OnAwake(); // ���� ���� ����
        }

        /// <summary>
        /// �ʿ��� ��� ���� Ŭ�������� Awake ���� ���� ����
        /// </summary>
        protected virtual void OnAwake() { }

        /// <summary>
        /// ������ ���־� �� �� ȣ�� (���� ���ᳪ �α׾ƿ�)
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