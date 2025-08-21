using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// ������ ��ó�� ��Ʈ�� �ִ��� ���ο� ���� 
    /// �������� ���� �޶��� �� �ֵ��� �ϴ� Ŭ����
    /// </summary>
    public class VerdictNote : MonoBehaviour
    {

        /// <summary>
        /// �浹ü�� Note�̸鼭 None ������ ��� Good ���·� ����
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
        /// Note�� ������ ������ ������ ���, None���·� ����
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