using System;
using System.Collections.Generic;
using Customization;
using Cysharp.Threading.Tasks;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using PMS_Util;
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
            Debug.Log($"owner actornubmer : {photonView.OwnerActorNr}");
            int actorNumber = photonView.OwnerActorNr;
            Player ownerPlayer = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);

            if (ownerPlayer.CustomProperties.TryGetValue(
                    Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.Uid), out var value) &&
                value is string uid)
            {
                // uid로 커스텀 정보 가져오기(game player에 있음)
                GamePlayer gp = PlayerManager.Instance.GetPlayer(uid);
                if (gp == null)
                {
                    Debug.LogError("Game Player is null!!");
                    return;
                }
                
                // 해당 플레이어 커스텀을 적용하고 적용 완료 했음을 알리기
                SetPlayerCustom(gp.PlayerId, gp.CharacterId, gp.EquipId);
            }
           
            
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


        public async void SetPlayerCustom(string uid, string charId, string equipId)
        {
            await Manager.Custom.ApplyToAvatarAsync(avatarStruct, charId, equipId);
            GameManager.Instance.AddCustomizedPlayer(uid);
        }
    }
}
