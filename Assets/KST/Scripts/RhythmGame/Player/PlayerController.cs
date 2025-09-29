using System;
using System.Collections.Generic;
using Customization;
using Cysharp.Threading.Tasks;
using Managers;
using Photon.Pun;
using UnityEngine;

namespace RhythmGame
{
    [RequireComponent(typeof(PhotonView))]
    public class PlayerController : MonoBehaviourPun
    {
        public static Dictionary<int, Transform> AvatarByActor = new(); //액터넘버, 위치 매핑
        [SerializeField] private AvatarStruct avatarStruct;
        
        
        private void Start()
        {
            UnimoCombo playerCustomCombo = Manager.Data.Custom.CurrentCombo;
            
            // rpc로 모두에게 해당 플레이어 오브젝트 커스텀 데이터 보내기
            photonView.RPC(nameof(SetPlayerCustom), RpcTarget.AllViaServer, PhotonNetwork.LocalPlayer.UserId , playerCustomCombo.characterId, playerCustomCombo.equipId);
            
        }

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

        [PunRPC]
        public async void SetPlayerCustom(string uid, string charId, string equipId)
        {
            await Manager.Custom.ApplyToAvatarAsync(avatarStruct, charId, equipId);
            GameManager.Instance.AddCustomizedPlayer(uid);
        }
    }
}
