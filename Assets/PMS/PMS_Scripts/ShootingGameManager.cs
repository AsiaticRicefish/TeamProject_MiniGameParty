using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Photon.Pun;
using DesignPattern;
using LDH_MainGame;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class ShootingGameManager : PunSingleton<ShootingGameManager>, IGameComponent, IGameStartHandler
{
    private CardManager CardManager;
    private ShootingGameState currentState;

    public event Action OnGameStarted;
    public event Action OnGameEnded;

    //public UnimoEgg currentUnimo;
    [SerializeField]private GameObject finishLine;

    public Dictionary<string, ShootingPlayerData> players = new(); // UID를 key로 가지는 플레이어 데이터
    private Dictionary<string, int> playerScores = new();        // 플레이어별 점수

    public int CurrentRound { get; private set; } = 0;
    public int MaxRounds { get; private set; } = 1;

    protected override void OnAwake()
    {
        base.isPersistent = false;          //슈팅 게임 안에서만 존재 
    }

    public void Initialize()
    {      
        CardManager = GameObject.FindObjectOfType<CardManager>();
        Debug.Log("[ShootingGameManager] - 슈팅 게임 초기화");
        InitializePlayers();                // 플레이어 정보 세팅 - 따로 instantiate에서 만들 필요는 없음.
        //ChangeState(new InitState());       //전부 InitState 씬 상태
    }

    private void InitializePlayers()
    {
        // 현재 방에 접속해 있는 모든 Photon 플레이어 목록을 순회
        foreach (var photonPlayer in PhotonNetwork.PlayerList)
        {
            // PhotonNetwork.PlayerList에서 꺼낸 플레이어 객체의 CustomProperties에서 uid (Firebase UID)를 추출
            string uid = photonPlayer.CustomProperties["uid"] as string;

            // UID를 기반으로 PlayerManager에서 해당 플레이어의 GamePlayer 객체를 가져옴
            //var gamePlayer = PlayerManager.Instance.GetPlayer(uid);

            // CreateOrGetPlayer를 사용하여 플레이어가 없으면 자동 생성
            var gamePlayer = PlayerManager.Instance.CreateOrGetPlayer(uid, photonPlayer.NickName);

            if (gamePlayer != null)
            {
                // GamePlayer에 미니게임 전용 데이터 생성 - ShootingPlayerData
                gamePlayer.ShootingData = new ShootingPlayerData
                {
                    score = 0,
                    myTurnIndex = -1
                };

                // ShootingGame 딕셔너리에 플레이어들을 uid 저장
                players[uid] = gamePlayer.ShootingData;
                // 점수를 저장하는 playerScores 딕셔너리에도 해당 UID로 0점 등록 (초기값)
                playerScores[uid] = 0;
            }
            else
            {
                Debug.LogError($"[ShootingGameManager - InitializePlayers] {uid}에 해당하는 GamePlayer를 찾을 수 없음");
            }
        }
    }

    private void Update()
    {
        currentState?.Update();
    }

    //게임 상태 변경 (로컬 호출 금지!!!!!!!!!!!!!!!!!!!!!!, RPC 통해 호출)
    //예외 : 콜백을 받지 못했을때 맨처음
    public void ChangeState(ShootingGameState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void OnGameStart()
    {
        if (JengaNetworkManager.Instance == null)
        {
            Debug.LogError("[ShootingGameManager] - 슈팅 게임 시작 오류, Instance가 생성이 안됨 ");
            return;
        }
        //마스터 클라이언트가 게임 시작을 알린다.
        if (PhotonNetwork.IsMasterClient)
        {
            // RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "InitState");
        }

        Debug.Log("[ShootingGameManager] - 슈팅 게임 시작!");
        //난 타이머가 없어도 된다. 
    }

    [PunRPC]
    private void InputOn()
    {
        OnGameStarted?.Invoke();
    }

    [PunRPC]
    private void InputOff()
    {
        OnGameEnded?.Invoke();
    }

    public void ChangeStateByName(string stateName)
    {
        switch (stateName)
        {
            case "InitState": ChangeState(new InitState()); break;
            case "CardSelectState": ChangeState(new CardSelectState());
                CardManager.enabled = true;
                break;
            case "GamePlayState": ChangeState(new GamePlayState()); break;
            case "CheckGameWinnderState": ChangeState(new CheckGameWinnderState()); break;
            default:
                Debug.LogError($"[ChangeStateByName] {stateName}에 해당하는 상태가 없습니다.");
                break;
        }
    }

    public void CheckGameWinner()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        UnimoEgg[] unimoEggList = GameObject.FindObjectsOfType<UnimoEgg>();

        UnimoEgg winnerUnimo = null;
        float minDistanceSqr = float.MaxValue;

        foreach(var unimoEgg in unimoEggList)
        {
            Vector3 worldDir = finishLine.transform.position - unimoEgg.transform.position;
            worldDir.y = 0f; // 높이 무시

            Vector2 flatDir = new Vector2(worldDir.x, worldDir.z);
            float distSqr = flatDir.sqrMagnitude;

            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                winnerUnimo = unimoEgg;
                     
            }
        }
        if (winnerUnimo != null)
            Debug.Log($"[ShootingGameManager] - 우승자 {winnerUnimo.ShooterUid}");

        //return winnerUnimo.ShooterUid;
        
        EndGame();
    }

    public void EndGame()
    {
        Debug.Log("[ShootingGameManager] - 게임 종료");
        if(PhotonNetwork.IsMasterClient)
            MainGameManager.Instance?.NotifyMiniGameFinish();
    }

    /*[PunRPC]
    public void RPC_ChangeState(string stateName)
    {
        switch (stateName)
        {
            case "InitState": ChangeState(new InitState()); break;
            case "CardSelectState": ChangeState(new CardSelectState()); break;
            default:
                Debug.Log($"[ShootingGameManager - RPC_ChangeState] - {stateName}에 해당되는 상태가 존재 하지 않습니다"); break;
        }
    }*/

    public void Timer()
    {
        
    }

    private void ResetTimer()
    {
        
    }
}
