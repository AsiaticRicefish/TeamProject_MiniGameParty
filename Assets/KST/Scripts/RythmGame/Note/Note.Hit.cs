using UnityEngine;

namespace RhythmGame
{
    partial class Note : MonoBehaviour
    {
        public void RequestHit()
        {
            bool isGood = _status == NoteStatus.Good;
            LaneManager.Instance.RequestHit(NoteId, isGood);
        }
    }
}
