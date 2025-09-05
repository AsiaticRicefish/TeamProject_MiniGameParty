using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using LDH.LDH_Scripts.Network;
using Managers;
using Photon.Pun;
using UnityEngine;


namespace LDH_MainGame
{
    [RequireComponent(typeof(PhotonView))]
    public class PhotonViewSync : PunSingleton<PhotonViewSync>
    {
        [Header("초기화 설정")] [SerializeField] protected float timeout = 30f; // WaitForAllPlayersLoaded()에서 사용하는 안전장치

        private HashSet<int> completedPlayers = new();
        private HashSet<int> hasCoordniatorPlayers = new();
        private HashSet<int> activeCompletedPlayers = new();
        private PhotonViewCoordinator _coordinator;


        private bool _syncCompleted = true;
        public bool SyncCompleted => _syncCompleted;
        
        
        private void Awake()
        {
            Debug.Log($"[photon veiw sync awake] view id {photonView.ViewID}");
            completedPlayers.Clear();
            hasCoordniatorPlayers.Clear();
            activeCompletedPlayers.Clear();
        }

        public void Clear()
        {
            Debug.Log("[PhotonViewSync] Clear Hash Sets");
            completedPlayers.Clear();
            hasCoordniatorPlayers.Clear();
            activeCompletedPlayers.Clear();

            _syncCompleted = false;
        }

        /// <summary>
        /// 포톤 뷰 재할당 및 전체 싱크 맞추는 총괄 메서드
        /// </summary>
        /// <returns></returns>
        public IEnumerator SafePhotonViewSync(PhotonViewCoordinator coordinator)
        {
            _coordinator = coordinator;
            //0단계 : 코디네이터 캐싱 완료
            photonView.RPC(nameof(RPC_HasCoordination), RpcTarget.All,
                PhotonNetwork.LocalPlayer.ActorNumber);
            
            Debug.Log($"=== SafePhotonViewSync START ===");
            // 1단계 : 포톤뷰 조정이 필요하면 포톤뷰 조정 처리
            Debug.Log($"[PhotonViewSync] Step 1 : Coordinate PhotonView");
            yield return StartCoroutine(SyncSceneViews(coordinator));

            // 2단계 : 내 포톤뷰 조정이 완료됐다고 알림
            Debug.Log($"[PhotonViewSync] Step 2 : Notify complete photon view coordination on local");
            // 2-1 : 포톤뷰 아이디 조정이 완료되었는지 다시 체크
            yield return StartCoroutine(WaitUntilMyCoordinateDone());
            // 2-2 : 조정 완료를 알리기
            photonView.RPC(nameof(RPC_CompletePhotonViewCoordination), RpcTarget.All,
                PhotonNetwork.LocalPlayer.ActorNumber);


            // 3단계 : 모든 플레이어의 조정 완료를 대기
            Debug.Log($"[PhotonViewSync] Step 3 : WaitUntilAllPlayerCompleted");
            yield return StartCoroutine(WaitUntilAllPlayerCompleted());

            coordinator.ActiveObjects();
            
            // 4단계 : 내 포톤 뷰 오브젝트 모두 활성화 대기
            Debug.Log($"[PhotonViewSync] Step 4 : Notify complete photon view objects all active");
            yield return StartCoroutine(WaitUntilObjectsActive());
            // 4-1단계 : 내 포톤 뷰 오브젝트 활성화 완료를 알림
            photonView.RPC(nameof(RPC_CompleteObjectsActive), RpcTarget.All,
                PhotonNetwork.LocalPlayer.ActorNumber);
            
            
            // 5단계 : 모든 플레이어의 포톤 뷰 오브젝트 활성화를 대기
            Debug.Log($"[PhotonViewSync] Step 5 : WaitUntilAllObjectsActive");
            yield return StartCoroutine(WaitUntilAllPlayerActiveObjects());


            Debug.Log($"=== SafePhotonViewSync Completed ===");

            _coordinator = null;
        }


        /// <summary>
        /// 마스터가 포톤뷰 아이디 재할당 및 싱크
        /// </summary>
        private IEnumerator SyncSceneViews(PhotonViewCoordinator coordinator)
        {
            // 씬이 올라와 Coordinator가 준비될 때까지 대기
            yield return new WaitUntil(() => coordinator != null);

            var sceneViews = coordinator.GetSceneViews();

            if (PhotonNetwork.IsMasterClient)
            {
                var ids = new int[sceneViews.Length];

                for (int i = 0; i < sceneViews.Length; i++)
                {
                    var pv = sceneViews[i];

                    // 남아있던 값 초기화(안전)
                    if (pv.ViewID != 0) pv.ViewID = 0;
                    int id = PhotonNetwork.AllocateViewID(0); // ⬅ Owner=0 (씬/룸 소유)
                    // if (!PhotonNetwork.AllocateViewID(0))
                    //     Debug.LogError($"AllocateViewID failed: {pv?.name}");
                    pv.ViewID = id;
                    ids[i] = id;
                }

                // 1) 마스터는 로컬 적용 + 활성화
                coordinator.ApplyIds(ids);

                yield return StartCoroutine(WaitUntilAllPlayerHasCoordinator());

                // 2) 다른 클라에 전파 (Buffered: 늦게 입장해도 적용)
                photonView.RPC(nameof(Rpc_AssignSceneViewIDs), RpcTarget.OthersBuffered, ids);
            }
            // 비마스터는 RPC 수신 시 ApplyIds가 실행됨

            yield return null;
        }
        
        
        private IEnumerator WaitUntilMyCoordinateDone()
        {
            Debug.Log("[PhotonViewSync] Wait until my coordinate done ");

            while (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                yield return null;

            float timer = 0f;
            while (!_coordinator.IsComplete)
            {
                timer += Time.deltaTime;
                if (timer > timeout)
                {
                    Debug.LogError($"[PhotonViewSync] !!!! WaitUntilAllPlayerCompleted Time Out!!!!");
                    yield break;
                }

                yield return null;
            }

            Debug.Log($"[PhotonViewSync] My coordinate done!");
        }
        

        /// <summary>
        /// 모든 플레이어가 포톤 뷰 조정을 마칠 때까지 대기
        /// </summary>
        private IEnumerator WaitUntilAllPlayerCompleted()
        {
            Debug.Log("[PhotonViewSync] Wait until all players completed");

            while (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                yield return null;

            float timer = 0f;
            while (completedPlayers.Count < PhotonNetwork.CurrentRoom.PlayerCount)
            {
                timer += Time.deltaTime;
                if (timer > timeout)
                {
                    Debug.LogError($"[PhotonViewSync] !!!! WaitUntilAllPlayerCompleted Time Out!!!!");
                    yield break;
                }

                yield return null;
            }

            Debug.Log($"[PhotonViewSync] All players complete coordination!");

            _syncCompleted = true;
        }

        
        private IEnumerator WaitUntilAllPlayerHasCoordinator()
        {
            Debug.Log("[PhotonViewSync] Wait until all players have coordinator ");

            while (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                yield return null;

            float timer = 0f;
            while (hasCoordniatorPlayers.Count < PhotonNetwork.CurrentRoom.PlayerCount)
            {
                timer += Time.deltaTime;
                if (timer > timeout)
                {
                    Debug.LogError($"[PhotonViewSync] !!!! WaitUntilAllPlayerHasCoordinator Time Out!!!!");
                    yield break;
                }

                yield return null;
            }

            Debug.Log($"[PhotonViewSync] All Player has coordinator!");
        }

        private IEnumerator WaitUntilObjectsActive()
        {
            Debug.Log("[PhotonViewSync]  Wait until my objects all active ");
            
            while (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                yield return null;

            float timer = 0f;
            while (!_coordinator.IsActiveAll)
            {
                timer += Time.deltaTime;
                if (timer > timeout)
                {
                    Debug.LogError($"[PhotonViewSync] !!!! WaitUntilObjectsActive Time Out!!!!");
                    yield break;
                }

                yield return null;
            }

            Debug.Log($"[PhotonViewSync] My  objects all active!");
            
        }
        
        private IEnumerator WaitUntilAllPlayerActiveObjects()
        {
            Debug.Log("[PhotonViewSync] Wait until all players have coordinator ");

            while (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                yield return null;

            float timer = 0f;
            while (activeCompletedPlayers.Count < PhotonNetwork.CurrentRoom.PlayerCount)
            {
                timer += Time.deltaTime;
                if (timer > timeout)
                {
                    Debug.LogError($"[PhotonViewSync] !!!!  WaitUntilAllPlayerActiveObjects Time Out!!!!");
                    yield break;
                }

                yield return null;
            }

            Debug.Log($"[PhotonViewSync] All Player all objects active!");
        }
        #region RPC

        [PunRPC]
        public void Rpc_AssignSceneViewIDs(int[] ids)
        {
            Debug.Log("Rpc_AssignSceneViewIDs 호출");
            _coordinator?.ApplyIds(ids);
        }

        [PunRPC]
        public void RPC_CompletePhotonViewCoordination(int playerActorNumber)
        {
            completedPlayers.Add(playerActorNumber);
            Debug.Log(
                $"Player ActorNumber({playerActorNumber}), NickName ({PhotonNetwork.CurrentRoom.GetPlayer(playerActorNumber).NickName}) Complete photon view coordination ({completedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
            
        }
        
        [PunRPC]
        public void RPC_HasCoordination(int playerActorNumber)
        {
            hasCoordniatorPlayers.Add(playerActorNumber);
            Debug.Log(
                $"Player ActorNumber({playerActorNumber}), NickName ({PhotonNetwork.CurrentRoom.GetPlayer(playerActorNumber).NickName}) Has coordinator ({completedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
            
        }
        
        
        [PunRPC]
        public void RPC_CompleteObjectsActive(int playerActorNumber)
        {
            activeCompletedPlayers.Add(playerActorNumber);
            Debug.Log(
                $"Player ActorNumber({playerActorNumber}), NickName ({PhotonNetwork.CurrentRoom.GetPlayer(playerActorNumber).NickName}) Complete all objects active. ({completedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
            
        }

        #endregion
    }
}