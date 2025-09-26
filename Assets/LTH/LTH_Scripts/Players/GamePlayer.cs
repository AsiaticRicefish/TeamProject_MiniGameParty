using System;
using System.Collections;
using System.Collections.Generic;
using LDH_MainGame;
using LDH_Util;
using Photon.Pun;
using UnityEngine;
using static LDH_Util.Define_LDH;

/// <summary>
/// 각 플레이어의 게임 내 상태(UID, 닉네임, 턴, 위치, 승리 여부 등)를 저장하고 관리
/// --------------------------------------------------------------------------------
/// 각 플레이어의 고유 식별 정보(Firebase UID, 닉네임) 보관
/// 게임 세션 중의 상태 관리 (턴 여부, 준비 여부, 보드 위치 등)
/// 미니게임 및 메인맵 결과 저장 (승리 여부, 승수 등)
/// 게임 흐름 제어 로직에서 기준 정보로 활용됨
/// </summary>
[Serializable]
public class GamePlayer
{
    #region 플레이어의 고유 정보

    public string PlayerId { get; private set; } // Firebase UID
    public string Nickname { get; private set; } // 플레이어 닉네임 (Photon)

    #endregion

    #region 플레이어 상태 정보

    public bool IsReady { get; private set; } // 현재 플레이어 게임 입장 준비 상태
    public bool IsTurn { get; private set; } // 현재 플레이어의 턴 여부

    public string CharacterId { get; private set; }
    public string EquipId { get; private set; }

    #endregion

    #region 미니게임 관련 데이터

    public JengaPlayerData JengaData { get; set; }
    public ShootingPlayerData ShootingData { get; set; }
    public RhythmPlayerData RhythmPlayerData { get; set; }

    #endregion

    #region 점수 / 랭킹 / 최종 보상

    public int Score { get; set; } // 누적 점수
    public int LastMiniGameRank { get; set; } // 최근 라운드(미니게임) 랭크
    public int TotalRank { get; set; } // 누적 점수 기준 종합 등수(동순위 반영)
    public bool WonThisRound { get; set; } // 이번 라운드 +1 여부


    //50 + 미니게임 당 10 (2등 -20, 3등 -30 , 4등 -40 공동순위일 경우 후순위 등수 적용)
    public int Reward
    {
        get
        {
            int round = MainGameManager.Instance?.PropertiesCtrl.GetRoomProps(RoomProps.Round, 1) ?? 0;
            if (round == 0)
            {
                Debug.LogWarning("Round is 0 !! Can't calculate reward");
                return 0;
            }
            
            return Mathf.Max((DefaultData.DefaultReward)
                   + (round * DefaultData.DefaultPointReward)
                   - (TotalRank * 10)
                   + (TotalRank == 1 ? 10 : 0), 0);
        } 
    }
    
    
    
    #endregion


    // 전체 게임에서 이긴 횟수 (이건 순위 정렬이나 추후에 랭크에 사용하는 경우 사용)

    public void Init(string id, string nickname, string cId = "", string eId = "")
    {
        PlayerId = id;
        Nickname = nickname;

        IsReady = false;
        IsTurn = false;

        CharacterId = cId;
        EquipId = eId;

        Score = 0;
        LastMiniGameRank = 0;
        TotalRank = 0;
        WonThisRound = false;

        JengaData = new JengaPlayerData(); // 미니게임 데이터 초기화
        ShootingData = new();
        RhythmPlayerData = new();
    }
}