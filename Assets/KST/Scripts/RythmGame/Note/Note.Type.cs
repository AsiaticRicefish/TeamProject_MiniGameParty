using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// NoteType�� �����Ͽ� ������ Ŭ����
    /// </summary>
    partial class Note : MonoBehaviour
    {
        public Obstacle _type;
        public Obstacle GetObstacleType() => _type;

        /// <summary>
        /// ��ֹ� ��Ʈ�� ������ ���� ���� ���ھ�
        /// </summary>
        public int GetOverLoadScore()
        {
            return _type switch
            {
                Obstacle.Fake => 1,
                Obstacle.Touch => 0,
                Obstacle.Continue => 0,
                _ => 0,
            };
        }
    }
}