using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  메인 게임과의 연동을 위한 데이터 클래스
/// </summary>
public static class GameResultData
{
    private static Dictionary<string, Dictionary<string, int>> minigameResults = new();

    public static void SetMinigameResult(string gameName, Dictionary<string, int> rankings)
    {
        minigameResults[gameName] = rankings;
    }

    public static Dictionary<string, int> GetMinigameResult(string gameName)
    {
        return minigameResults.ContainsKey(gameName) ? minigameResults[gameName] : null;
    }

    public static void ClearResults()
    {
        minigameResults.Clear();
    }
}