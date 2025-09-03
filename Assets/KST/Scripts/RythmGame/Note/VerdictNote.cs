using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// 판정바 근처에 노트가 있는지 여부에 따라 
    /// 과열판정 등이 달라질 수 있도록 하는 클래스
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VerdictNote : MonoBehaviour
    {
        //정확도 판정을 해당 판정바의 한 축을 기준으로 
        //얼마나 중앙에 있는지 여부에 따라 accuracy가 달라지도록 할 필요가 잇음.
        //note의 충돌 위치에 따라 status 변화를 줘도 될지도?


        private List<Note> _notes = new();//판정 바에 들어온 노트들
        public List<Note> Notes => _notes;

        /// <summary>
        /// 충돌체가 Note이면서 None 상태일 경우 Good 상태로 변경
        /// </summary>
        /// <param name="other"></param>
        void OnTriggerStay(Collider other)
        {
            if (!other.TryGetComponent(out Note note)) return;
            if (_notes.Contains(note)) return;

            _notes.Add(note);
            note.Status = NoteStatus.CanInteract;

            note.OnDespawn -= Despawn;
            note.OnDespawn += Despawn;
        }

        /// <summary>
        /// Note가 판정바 밖으로 나갔을 경우, None상태로 변경
        /// </summary>
        /// <param name="other"></param>
        void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent(out Note note)) return;
            if (!_notes.Contains(note)) return;

            note.ReturnPool();
        }

        void Despawn(Note note)
        {
            if (_notes.Remove(note))
                note.OnDespawn -= Despawn;
        }
    }
}