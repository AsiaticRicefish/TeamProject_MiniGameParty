using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace RhythmGame
{
    [RequireComponent(typeof(PhotonView))]
    public class PlayerVerdict : MonoBehaviourPun
    {
        public static Dictionary<int, Transform> VerdictByActor = new(); //판정바 액터넘버, 위치 매핑

        void OnEnable()
        {
            if (photonView && photonView.Owner != null) //포톤뷰 및 owner가 정상적으로 할당된 경우
                VerdictByActor[photonView.OwnerActorNr] = transform; //딕셔너리에 등록
        }

        void OnDisable()
        {
            if (photonView && photonView.Owner != null)
                VerdictByActor.Remove(photonView.OwnerActorNr);
        }
    }
}
