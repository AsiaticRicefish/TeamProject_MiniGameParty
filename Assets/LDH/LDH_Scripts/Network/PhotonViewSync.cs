using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LDH.LDH_Scripts.Network;
using Photon.Pun;
using UnityEngine;


namespace LDH_MainGame
{
    public class PhotonViewSync : MonoBehaviourPun
    {
        public static PhotonViewSync Instance { get; private set; }
        

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }


        public IEnumerator SyncSceneViewsAndActivate()
        {
            // 씬이 올라와 Coordinator가 준비될 때까지 대기
            yield return new WaitUntil(() => PhotonViewCoordinator.Instance != null);

            var sceneViews = PhotonViewCoordinator.Instance.GetSceneViews();

            bool prev = PhotonNetwork.IsMessageQueueRunning;
            PhotonNetwork.IsMessageQueueRunning = false;

            if (PhotonNetwork.IsMasterClient)
            {
                var ids = new int[sceneViews.Length];

                for (int i = 0; i < sceneViews.Length; i++)
                {
                    var pv = sceneViews[i];

                    // 남아있던 값 초기화(안전)
                    if (pv.ViewID != 0) pv.ViewID = 0;

                    if (!PhotonNetwork.AllocateViewID(pv))
                        Debug.LogError($"AllocateViewID failed: {pv?.name}");

                    ids[i] = pv.ViewID;
                }

                // 1) 마스터는 로컬 적용 + 활성화
                PhotonViewCoordinator.Instance.ApplyIdsAndActivate(ids);

                // 2) 다른 클라에 전파 (Buffered: 늦게 입장해도 적용)
                photonView.RPC(nameof(Rpc_AssignSceneViewIDs), RpcTarget.OthersBuffered, ids);
            }
            // 비마스터는 RPC 수신 시 ApplyIdsAndActivate가 실행됨

            yield return null;
            PhotonNetwork.IsMessageQueueRunning = prev;
        }

        [PunRPC]
        void Rpc_AssignSceneViewIDs(int[] ids)
        {
            PhotonViewCoordinator.Instance?.ApplyIdsAndActivate(ids);
        }
    }
}