using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;
using Cysharp.Threading.Tasks;
using ExitGames.Client.Photon;
using Photon.Realtime;

namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    public class ShootingNetworkManager : PunSingleton<ShootingNetworkManager>, IGameComponent, IOnEventCallback
    {
        private string turnObserverId;
        private string SceneChangeObserverId;

        private Coroutine _setTurnCoroutine;

        //Controllers
        public NetworkTimer networkTimer;

        public Coroutine SetTurnCoroutine
        {
            get => _setTurnCoroutine;
            set
            {
                _setTurnCoroutine = value;
            }
        }

        protected override void OnAwake()
        {
            base.isPersistent = false;
        }

        public void Initialize()
        {
            networkTimer = new NetworkTimer();

            if (PhotonNetwork.IsMasterClient)
            {
                //마스터 클라이언트만 사용할 룸 프로퍼티 생성 및 구독 처리
                var props = new ExitGames.Client.Photon.Hashtable
                {
                    { ShootingGamePropertyKeys.State, "InitState" },
                    { ShootingGamePropertyKeys.Turn, 0 },
                    { ShootingGamePropertyKeys.Round, 1 },
                    ///{ ShootingGamePropertyKeyss.PlayerScore_Prefix + Player },
                };
                foreach (var player in PlayerManager.Instance.Players)
                {
                    string scoreKey = ShootingGamePropertyKeys.PlayerScore_Prefix + player.Value.PlayerId;
                    props.Add(scoreKey, 0);                                                                     // 초기 점수 0
                }

                PhotonNetwork.CurrentRoom.SetCustomProperties(props);

                ShootingGameRoomPropertyRegister();
            }
            else
            {
                ShootingGameRoomPropertyRegister();
            }
        }


        private void ShootingGameRoomPropertyRegister()
        {
            // 게임 상태 구독
            ShootingGameSceneChangeRoomPropertiesReigster();
        }

        public void ShootingGameSceneChangeRoomPropertiesReigster()
        {
            // 게임 상태 구독
            SceneChangeObserverId = RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.State, (value) =>
            {
                string newState = (string)value;
                ShootingGameManager.Instance.ChangeStateByName(newState);
            });
        }

        public void ShootingGameSceneChangeRoomPropertiesUnReigster()
        {
            if (!string.IsNullOrEmpty(SceneChangeObserverId))
            {
                RoomPropertyObserver.Instance.UnregisterObserverById(SceneChangeObserverId);
                SceneChangeObserverId = null;
            }
        }

        public void ShootingGameTurnAndRoundRoomPropertiesReigster()
        {
            // 게임 턴,라운드(int) 구독           
            // 람다를 변수에 저장해둠
            turnObserverId = RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.Turn, (value) =>
            {   
                // 모든 클라이언트에서 Turn Order (linkedlist)의 current를 업데이트한다.
                TurnManager.Instance.MoveToNextTurn();
                
                int newTurnIndex = (int)value;
                TurnManager.Instance.currentTurnIndex = newTurnIndex;

                int newRound = (int)RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.Round);
                TurnManager.Instance.currentRoundIndex = newRound;

                if (_setTurnCoroutine != null)
                {
                    StopCoroutine(_setTurnCoroutine);
                    _setTurnCoroutine = null;
                }
                StartCoroutine(TurnManager.Instance.SetCurrentTurn());
            });
        }

        public void ShootingGameTurnAndRoundRoomPropertiesUnReigster()
        {
            if (!string.IsNullOrEmpty(turnObserverId))
            {
                RoomPropertyObserver.Instance.UnregisterObserverById(turnObserverId);
                turnObserverId = null;
            }
        }

        //슈팅 게임 룸 프로퍼티 초기화 함수
        public void ClearShootingGameRoomProperties()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 초기화할 룸 프로퍼티 키들
            string[] gameKeys = { 
                ShootingGamePropertyKeys.State, 
                ShootingGamePropertyKeys.Turn, 
                ShootingGamePropertyKeys.Round,
                ShootingGamePropertyKeys.KEY_DECK_VALUES,
                ShootingGamePropertyKeys.KEY_CARD_OWNERS,
                ShootingGamePropertyKeys.KEY_STATE,
                ShootingGamePropertyKeys.KEY_TURN_ORDER,
             };

            var props = new ExitGames.Client.Photon.Hashtable();
            foreach (var key in gameKeys)
            {
                props[key] = null;  // 또는 초기값 설정 가능
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        //슈팅 게임 플레이어 프로퍼티 초기화 함수
        public void ClearShootingGamePlayerProperties()
        {
            //각자 초기화
            var props = new ExitGames.Client.Photon.Hashtable();

            //초기화할 플레이어 프로퍼티 키들
            string[] keys = { 
                ShootingGamePlayerPropertyKeys.MyPrefabName, 
                ShootingGamePlayerPropertyKeys.MyTurnIndex
            };

            foreach (var key in keys)
            {
                props[key] = null;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        #region 타이머 관련 호출 함수
        public void StartTimer(double duration, bool isLocal, double lead = 0.1)
        {
            double startAt,endAt;

            if (isLocal)
            {
                //로컬에서는 지연시간을 생각안해도됨
                startAt = PhotonNetwork.Time;
                endAt = startAt + duration;
            }
            else
            {
                //RPC 동기화에서는 지연이 발생하기 때문에 지연 시간을 고려해서 Start할 수 있도록
                startAt = PhotonNetwork.Time + lead;
                endAt = startAt + duration;

                //핑찍기
                double sendTime = PhotonNetwork.Time;
                photonView.RPC("OnReceiveRPC", RpcTarget.All, sendTime);
            }

            if (isLocal == true)
                networkTimer.OnStartTimer(startAt, endAt);
            else
            {
                if (!PhotonNetwork.IsMasterClient) return;
                photonView.RPC("RPC_StartTimer", RpcTarget.All, startAt, endAt);
            }
        }

        [PunRPC]
        void OnReceiveRPC(double sendTime)
        {
            double receiveTime = PhotonNetwork.Time;
            double delay = receiveTime - sendTime;
            Debug.Log($"Client {PhotonNetwork.LocalPlayer.NickName}: Delay = {delay * 1000} ms");
        }

        public void CancelTimer(bool isLocal)
        {
            if (isLocal)
                networkTimer.CancelTimer();
            else
            {
                if (!PhotonNetwork.IsMasterClient) return;
                photonView.RPC("RPC_CancelTimer", RpcTarget.All);
            }
        }

        [PunRPC]
        public void RPC_StartTimer(double startAt, double endAt)
        {
            Debug.Log("RPC를 통하여 모두에게 타이머 작동 시작!");

            networkTimer.OnStartTimer(startAt,endAt);
        }

        [PunRPC]
        public void RPC_CancelTimer()
        {
            Debug.Log("RPC를 통하여 모두에게 타이머 작동 캔슬요청!");
            networkTimer.CancelTimer();
        }
        #endregion

        //퍼즈 게임 Pause
        const byte EVT_PAUSE = 199;
        const byte EVT_RESUME = 199;

        public override void OnEnable()
        {
            base.OnEnable(); // 부모 호출 (다른 콜백들 등록)
            PhotonNetwork.AddCallbackTarget(this);
        }

        public override void OnDisable()
        {
            base.OnDisable(); // 부모 호출 (다른 콜백들 등록)
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        // 홈 버튼 등으로 백그라운드 진입 시
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                PhotonNetwork.RaiseEvent(
                    EVT_PAUSE,                                 // 이벤트 코드
                    PhotonNetwork.LocalPlayer.ActorNumber,     // 전송할 데이터
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    new SendOptions { Reliability = true }
                );
            }

            if (!pauseStatus)
            {
                PhotonNetwork.RaiseEvent(
                    EVT_RESUME,
                    PhotonNetwork.LocalPlayer.ActorNumber,
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    new SendOptions { Reliability = true }
                );
            }
        }

        // 수신 측에서는 OnEvent 콜백으로 처리
        void IOnEventCallback.OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == EVT_PAUSE)
            {
                int actorId = (int)photonEvent.CustomData;
                Debug.Log($"Player {actorId} 백그라운드 진입 감지");      
                
            }

            if (photonEvent.Code == EVT_RESUME)
            {
                int actorId = (int)photonEvent.CustomData;
                Debug.Log($"Player {actorId} 복귀 감지");              
            }
        }
    }
}