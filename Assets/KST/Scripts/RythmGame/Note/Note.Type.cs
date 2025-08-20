using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// NoteType과 관련하여 정의한 클래스
    /// </summary>
    partial class Note : MonoBehaviour
    {
        public Obstacle _type;
        public Obstacle GetObstacleType() => _type;

        /// <summary>
        /// 장애물 노트의 종류에 따른 과열 스코어
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