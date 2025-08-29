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
        private Vector3 _spawnPos;
        [SerializeField] private float _moveDist = 30f; //움직이는 거리
        private Vector3 _moveDir; // 월드 고정 이동 방향

        void Update()
        {
            transform.Translate(-_moveDir * (_speed * Time.deltaTime), Space.World);

            if ((transform.position - _spawnPos).sqrMagnitude >= _moveDist * _moveDist)
                _pooled.ReturnPool();
        }

        /// <summary>
        /// Note 속도 설정 메서드
        /// </summary>
        /// <param name="speed">Note의 속도를 결정하는 매개변수</param>
        public void SetSpeed(float speed) => _speed = speed;
        public void SetMoveDirection(Vector3 dir) => _moveDir = dir.normalized;
    }
}