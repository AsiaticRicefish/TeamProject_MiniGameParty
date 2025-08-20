using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 각 플레이어의 게임 내 상태(UID, 닉네임, 턴, 위치, 승리 여부 등)를 저장하고 관리
/// --------------------------------------------------------------------------------
/// 각 플레이어의 고유 식별 정보(Firebase UID, 닉네임) 보관
/// 게임 세션 중의 상태 관리 (턴 여부, 준비 여부, 보드 위치 등)
/// 미니게임 및 메인맵 결과 저장 (승리 여부, 승수 등)
/// 게임 흐름 제어 로직에서 기준 정보로 활용됨
/// </summary>


public class GamePlayer : MonoBehaviour
{
    #region 플레이어의 고유 정보
    public string PlayerId { get; private set; }    // Firebase UID
    public string Nickname { get; private set; }    // 플레이어 닉네임 (Photon)
    #endregion

    #region 플레이어 상태 정보
    public bool IsReady { get; private set; }       // 현재 플레이어 게임 입장 준비 상태
    public bool IsTurn { get; private set; }        // 현재 플레이어의 턴 여부
    #endregion

    public bool WinThisMiniGame { get; set; }       // 미니게임에서 승리 여부

    public int BoardPosition { get; set; }          // 현재 보드에서의 위치 (0부터 시작, 0은 시작점)


    // 전체 게임에서 이긴 횟수 (이건 순위 정렬이나 추후에 랭크에 사용하는 경우 사용)
    public int WinCount { get; set; }

    public GamePlayer(string id, string nickname)
    {
        PlayerId = id;
        Nickname = nickname;

        IsReady = false;
        IsTurn = false;

        WinCount = 0;
        BoardPosition = 0;
        WinThisMiniGame = false;
    }
}
