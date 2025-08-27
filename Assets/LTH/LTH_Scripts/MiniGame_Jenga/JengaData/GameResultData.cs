using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  메인 게임과의 연동을 위한 데이터 클래스
/// </summary>
public static class GameResultData
{
    private static readonly Dictionary<string, Dictionary<string, int>> minigameResults = new();

    public static void SetMinigameResult(string gameName, Dictionary<string, int> rankings)
    {
        minigameResults[gameName] = new Dictionary<string, int>(rankings);
    }

    public static Dictionary<string, int> GetMinigameResult(string gameName)
    {
        return minigameResults.TryGetValue(gameName, out var r) ? r : null;
    }

    public static bool TryGetMinigameResult(string gameName, out Dictionary<string, int> rankings)
    {
        if (minigameResults.TryGetValue(gameName, out var r))
        {
            rankings = new Dictionary<string, int>(r);
            return true;
        }
        rankings = null;
        return false;
    }

    public static void Clear(string gameName)
    {
        if (minigameResults.ContainsKey(gameName))
            minigameResults.Remove(gameName);
    }

    public static void ClearAll()
    {
        minigameResults.Clear();
    }
}