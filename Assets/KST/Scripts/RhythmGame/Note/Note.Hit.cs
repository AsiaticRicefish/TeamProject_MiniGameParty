using UnityEngine;

namespace RhythmGame
{
    partial class Note : MonoBehaviour
    {
        [SerializeField] MeshFilter _mf;
        /// <summary>
        /// z축 길이 구하는 로직
        /// </summary>
        /// <returns></returns>
        public float GetZLength() =>
            Mathf.Abs(_mf.sharedMesh.bounds.size.z * transform.lossyScale.z);

        /// <summary>
        /// 최소 홀드 시간
        /// </summary>
        /// <returns></returns>
        public float GetHoldTime()
        {
            float align = Mathf.Abs(Vector3.Dot(transform.forward, _moveDir.normalized));
            float passSpeed = Mathf.Max(0f, _speed * align);
            return GetZLength() / passSpeed;
        }
    }
}