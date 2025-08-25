using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DesignPattern;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class ShootingGameManager : PunSingleton<ShootingGameManager>, IGameComponent, IGameStartHandler
{
    private ShootingGameState currentState;

    //public UnimoEgg currentUnimo;

    public Dictionary<string, ShootingPlayerData> players = new(); // UID�� key�� ������ �÷��̾� ������
    private Dictionary<string, int> playerScores = new();        // �÷��̾ ����

    public int CurrentRound { get; private set; } = 0;
    public int MaxRounds { get; private set; } = 3;

    protected override void OnAwake()
    {
        base.isPersistent = false;          //���� ���� �ȿ����� ���� 
    }

    public void Initialize()
    {
        Debug.Log("[ShootingGameManager] - ���� ���� �ʱ�ȭ");
        InitializePlayers();                // �÷��̾� ���� ���� - ���� instantiate���� ���� �ʿ�� ����.
        ChangeState(new InitState());       //���� InitState �� ����
    }

    private void InitializePlayers()
    {
        // ���� �濡 ������ �ִ� ��� Photon �÷��̾� ����� ��ȸ
        foreach (var photonPlayer in PhotonNetwork.PlayerList)
        {
            // PhotonNetwork.PlayerList���� ���� �÷��̾� ��ü�� CustomProperties���� uid (Firebase UID)�� ����
            string uid = photonPlayer.CustomProperties["uid"] as string;

            // UID�� ������� PlayerManager���� �ش� �÷��̾��� GamePlayer ��ü�� ������
            var gamePlayer = PlayerManager.Instance.GetPlayer(uid);
            if (gamePlayer != null)
            {
                // GamePlayer�� �̴ϰ��� ���� ������ ���� - ShootingPlayerData
                gamePlayer.ShootingData = new ShootingPlayerData
                {
                    score = 0,
                    myTurnIndex = -1
                };

                // ShootingGame ��ųʸ��� �÷��̾���� uid ����
                players[uid] = gamePlayer.ShootingData;
                // ������ �����ϴ� playerScores ��ųʸ����� �ش� UID�� 0�� ��� (�ʱⰪ)
                playerScores[uid] = 0;
            }
            else
            {
                Debug.LogError($"[ShootingGameManager - InitializePlayers] {uid}�� �ش��ϴ� GamePlayer�� ã�� �� ����");
            }
        }
    }

    private void Update()
    {
        currentState?.Update();
    }

    //���� ���� ���� (���� ȣ�� ����!!!!!!!!!!!!!!!!!!!!!!, RPC ���� ȣ��)
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
            Debug.LogError("[ShootingGameManager] - ���� ���� ���� ����, Instance�� ������ �ȵ� ");
            return;
        }
        //������ Ŭ���̾�Ʈ�� ���� ������ �˸���.
        if (PhotonNetwork.IsMasterClient)
        {
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "InitState");
        }

        Debug.Log("[ShootingGameManager] - ���� ���� ����!");
        //�� Ÿ�̸Ӱ� ��� �ȴ�. 
    }

    public void ChangeStateByName(string stateName)
    {
        switch (stateName)
        {
            case "InitState": ChangeState(new InitState()); break;
            case "CardSelectState": ChangeState(new CardSelectState()); break;
            case "GamePlayState": ChangeState(new GamePlayState()); break;
            case "CheckGameWinnderState": ChangeState(new GamePlayState()); break;
            default:
                Debug.LogError($"[ChangeStateByName] {stateName}�� �ش��ϴ� ���°� �����ϴ�.");
                break;
        }
    }

    public void CheckGameWinner()
    {
        if(PhotonNetwork.IsMasterClient)
        {
            GameObject[] unimoEggList = GameObject.FindGameObjectsWithTag("UnimoEgg");
        }
        Debug.Log("[ShootingGameManager] - ����� ���");
    }

    public void EndGame()
    {
        Debug.Log("[ShootingGameManager] - ���� ����");
    }

    /*[PunRPC]
    public void RPC_ChangeState(string stateName)
    {
        switch (stateName)
        {
            case "InitState": ChangeState(new InitState()); break;
            case "CardSelectState": ChangeState(new CardSelectState()); break;
            default:
                Debug.Log($"[ShootingGameManager - RPC_ChangeState] - {stateName}�� �ش�Ǵ� ���°� ���� ���� �ʽ��ϴ�"); break;
        }
    }*/
}
