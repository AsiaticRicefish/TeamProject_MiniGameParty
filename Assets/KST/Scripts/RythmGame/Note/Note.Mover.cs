using DesignPattern;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Note가 플레이어 방향으로 이동한다는 가정으로 만든 클래스
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
        /// Note 속도 설정 메서드
        /// </summary>
        /// <param name="speed">Note의 속도를 결정하는 매개변수</param>
        public void SetSpeed(float speed) => _speed = speed;
        public void ReturnPool() => _pooled.ReturnPool();
    }
}