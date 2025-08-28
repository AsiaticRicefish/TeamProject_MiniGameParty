using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// NoteType과 관련하여 정의한 클래스
    /// </summary>
    partial class Note : MonoBehaviour
    {
        private NoteType _type;
        public NoteType GetObstacleType() => _type;

        private NoteStatus _status;
        public NoteStatus Status { get { return _status; } set { _status = value; } }

        private int _noteId;
        public int NoteId { get { return _noteId; } set { _noteId = value; } }
        private int _lane;
        public int Lane { get { return _lane; } set { _lane = value; } }

        //초기화
        public void Init(int noteId, int lane)
        {
            _noteId = noteId;
            _lane = lane;
            _status = NoteStatus.None;
        }


        /// <summary>
        /// 장애물 노트의 종류에 따른 과열 스코어
        /// </summary>
        public int GetOverLoadScore()
        {
            //아직 판정바 근처에 Note가 접근하지 않았음에도 파괴를 시도한다면 과열 스택이 쌓이도록 해야 함.
            if (_status == NoteStatus.None) return 1;

            return _type switch
            {
                NoteType.Fake => 1,
                NoteType.Touch => 0,
                NoteType.Continue => 0,
                _ => 0,
            };
        }
    }
}