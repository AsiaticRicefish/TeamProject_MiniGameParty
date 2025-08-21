using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// 판정바 근처에 노트가 있는지 여부에 따라 
    /// 과열판정 등이 달라질 수 있도록 하는 클래스
    /// </summary>
    public class VerdictNote : MonoBehaviour
    {

        /// <summary>
        /// 충돌체가 Note이면서 None 상태일 경우 Good 상태로 변경
        /// </summary>
        /// <param name="other"></param>
        void OnTriggerStay(Collider other)
        {
            if (other.TryGetComponent(out Note note))
            {
                if (note.Status == NoteStatus.None)
                {
                    note.Status = NoteStatus.Good;
                }
            }
        }

        /// <summary>
        /// Note가 판정바 밖으로 나갔을 경우, None상태로 변경
        /// </summary>
        /// <param name="other"></param>
        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out Note note))
            {
                if (note.Status != NoteStatus.None)
                {
                    note.Status = NoteStatus.None;
                }
            }
        }
    }
}