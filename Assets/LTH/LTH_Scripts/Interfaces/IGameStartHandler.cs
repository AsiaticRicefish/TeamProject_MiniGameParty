using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 시작 시 호출되는 인터페이스
/// </summary>
public interface IGameStartHandler
{
    void OnGameStart(); // 게임 시작 알림
}