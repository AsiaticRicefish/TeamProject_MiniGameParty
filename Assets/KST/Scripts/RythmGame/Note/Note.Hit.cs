using UnityEngine;

namespace RhythmGame
{
    partial class Note : MonoBehaviour
    {
        public void RequestHit()
        {
            bool isGood = _status == NoteStatus.CanInteract;
            LaneManager.Instance.RequestHit(NoteId, isGood);
        }
    }
}
