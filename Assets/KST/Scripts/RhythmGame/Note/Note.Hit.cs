using UnityEngine;

namespace RhythmGame
{
    partial class Note : MonoBehaviour
    {
        [SerializeField] Renderer[] rr;
        [SerializeField] Collider cd;
        bool _isWaiting; //히트 대기 상태
        public bool IsWaiting => _isWaiting;
        [SerializeField] GameObject _bubbleGo;

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

        public void SetWait(bool b) => _isWaiting = b;

        public void Invisible()
        {
            cd.enabled = false;
            foreach (var r in rr)
                r.enabled = false;
        }
        public void Visible()
        {
            cd.enabled = true;
            foreach (var r in rr)
                r.enabled = true;
        }

        public void BubblePop()
        {
            if (_bubbleGo != null)
                _bubbleGo.SetActive(false);
        }

        public void BubbleInit()
        {
            if (_bubbleGo != null)
                _bubbleGo.SetActive(true);
        }

    }
}