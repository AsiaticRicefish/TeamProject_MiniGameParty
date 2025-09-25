using System.Collections.Generic;
using Photon.Pun;
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

            _notes.Remove(note);

            note.Status = NoteStatus.None;

            // //속임수 블럭의 경우 미스처리 금지.
            // if (note.Type == NoteType.Fake)
            // {
            //     ScoreManager.Instance.RequestMissFake(note.NoteId);
            //     return;
            // }

            if (!TryGetLane(out int myLane)) return;
            //내 레인이 아닐경우 금지
            if (note.Lane != myLane) return;
            ScoreManager.Instance.RequestLaneMiss(note.NoteId);

            // note.ReturnPool();
        }

        void Despawn(Note note)
        {
            if (_notes.Remove(note))
                note.OnDespawn -= Despawn;
        }

        bool TryGetLane(out int lane)
        {
            lane = -1;
            if (!LaneManager.Instance || !PhotonNetwork.IsConnected) return false;
            return LaneManager.Instance.GetLane(PhotonNetwork.LocalPlayer.ActorNumber, out lane);
        }
    }
}