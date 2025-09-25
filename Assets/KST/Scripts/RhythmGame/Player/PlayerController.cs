using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace RhythmGame
{
    [RequireComponent(typeof(PhotonView))]
    public class PlayerController : MonoBehaviourPun
    {
        public static Dictionary<int, Transform> AvatarByActor = new(); //액터넘버, 위치 매핑

        void OnEnable()
        {
            if (photonView && photonView.Owner != null) //포톤뷰 및 owner가 정상적으로 할당된 경우
                AvatarByActor[photonView.OwnerActorNr] = transform; //딕셔너리에 등록
        }

        void OnDisable()
        {
            if (photonView && photonView.Owner != null)
                AvatarByActor.Remove(photonView.OwnerActorNr);
        }
    }
}
