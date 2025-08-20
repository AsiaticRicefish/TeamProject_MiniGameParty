using DesignPattern;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Note�� �÷��̾� �������� �̵��Ѵٴ� �������� ���� Ŭ����
    /// </summary>

    [RequireComponent(typeof(PooledObject))]
    partial class Note : MonoBehaviour
    {
        private float _speed;
        private PooledObject _pooled;

        void Awake() => _pooled = GetComponent<PooledObject>();
        void Update()
        {
            transform.Translate(_speed * Time.deltaTime * Vector2.down);

            if (transform.position.y < -6f)
                _pooled.ReturnPool();
        }

        /// <summary>
        /// Note �ӵ� ���� �޼���
        /// </summary>
        /// <param name="speed">Note�� �ӵ��� �����ϴ� �Ű�����</param>
        public void SetSpeed(float speed) => _speed = speed;
        public void ReturnPool() => _pooled.ReturnPool();
    }
}