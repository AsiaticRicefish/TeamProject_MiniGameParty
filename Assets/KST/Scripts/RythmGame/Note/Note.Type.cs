using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// NoteType�� �����Ͽ� ������ Ŭ����
    /// </summary>
    partial class Note : MonoBehaviour
    {
        private NoteType _type;
        public NoteType GetObstacleType() => _type;

        private NoteStatus _status;
        public NoteStatus Status { get { return _status; }  set { _status = value; } }

        /// <summary>
        /// ��ֹ� ��Ʈ�� ������ ���� ���� ���ھ�
        /// </summary>
        public int GetOverLoadScore()
        {
            //���� ������ ��ó�� Note�� �������� �ʾ������� �ı��� �õ��Ѵٸ� ���� ������ ���̵��� �ؾ� ��.
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