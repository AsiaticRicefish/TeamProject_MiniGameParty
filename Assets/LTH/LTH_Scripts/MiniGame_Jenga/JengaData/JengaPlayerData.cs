using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 미니게임 Jenga에서 각 플레이어의 데이터를 저장하는 클래스
/// GamePlayer에는 기본적인 정보(UID, 닉네임 등)가 저장되고,
/// 각 미니게임 당 사용되는 데이터는 따로 분리하는 것이 좋을 것 같아 생성한 클래스
/// </summary>

[Serializable]
public class JengaPlayerData
{
    public int score = 0;
    public bool isAlive = true;
    public bool isFinished = false;

    public Vector3 towerPosition; // 개별 타워 위치
    public Vector3 lastTouchPosition;

    public float gameStartTime; // 게임 시작 시간 (순위 계산용)
    public float lastSuccessTime; // 마지막 블록 성공 시간 (or 총 소요 시간)
}