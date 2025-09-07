using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum JengaGameState
{
    Waiting,
    Playing,
    Paused,
    Finished
}

// 카운트다운 상태 열거형
public enum CountdownState
{
    None,        // 카운트다운 없음
    InProgress,  // 카운트다운 진행중
    Completed    // 카운트다운 완료
}