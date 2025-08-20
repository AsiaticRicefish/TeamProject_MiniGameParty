using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  ���� ���Ӱ��� ������ ���� ������ Ŭ����
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