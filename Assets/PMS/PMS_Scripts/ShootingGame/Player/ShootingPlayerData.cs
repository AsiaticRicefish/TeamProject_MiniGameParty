using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// 미니게임 Shooting에서 각 플레이어의 데이터를 저장하는 클래스
[Serializable]
public class ShootingPlayerData
{
    public int score = 0;           //점수를 통한 순위 계산
    public int myTurnIndex = -1;
}
