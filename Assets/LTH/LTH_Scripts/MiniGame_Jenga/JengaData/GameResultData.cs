using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


/// <summary>
/// 메인 게임과의 연동을 위한 결과 저장소
/// - 결과 저장/조회
/// - 1회성 소비(consume) 지원으로 중복 점수 반영 방지
/// - 간편 헬퍼 제공 (개별 순위, 상위 N명)
/// </summary>

public static class GameResultData
{
    // 내부 저장 구조: 게임명 -> (결과, 소비여부)
    private class Entry
    {
        public Dictionary<string, int> Rankings; // UID -> Rank(1,2,...)
        public bool Consumed;                    // 점수 반영 등으로 이미 사용했는지
    }

    private static readonly Dictionary<string, Entry> store = new();

    /// <summary> 
    /// 결과 저장 (덮어씀). 외부에서 건드릴 수 없도록 방어 복사. 
    /// </summary>
    public static void SetMinigameResult(string gameName, Dictionary<string, int> rankings)
    {
        if (string.IsNullOrEmpty(gameName) || rankings == null) return;

        store[gameName] = new Entry
        {
            Rankings = new Dictionary<string, int>(rankings),
            Consumed = false
        };
    }

    /// <summary> 
    /// 결과 조회 (복사본 반환). 없으면 null. 
    /// </summary>
    public static Dictionary<string, int> GetMinigameResult(string gameName)
    {
        return store.TryGetValue(gameName, out var e) && e.Rankings != null
            ? new Dictionary<string, int>(e.Rankings)
            : null;
    }

    /// <summary> 
    /// 결과 조회 Try 버전 (복사본 반환). 
    /// </summary>
    public static bool TryGetMinigameResult(string gameName, out Dictionary<string, int> rankings)
    {
        if (store.TryGetValue(gameName, out var e) && e.Rankings != null)
        {
            rankings = new Dictionary<string, int>(e.Rankings);
            return true;
        }
        rankings = null;
        return false;
    }

    /// <summary>
    /// 한 번만 소비: 아직 소비되지 않았다면 결과를 반환하고 Consumed=true로 표기.
    /// (점수 반영 루틴에서 이 메서드만 쓰면 중복 반영 방지 가능)
    /// </summary>
    public static bool TryConsume(string gameName, out Dictionary<string, int> rankings)
    {
        rankings = null;
        if (!store.TryGetValue(gameName, out var e) || e.Rankings == null) return false;
        if (e.Consumed) return false;

        e.Consumed = true;
        rankings = new Dictionary<string, int>(e.Rankings);
        return true;
    }

    /// <summary> 
    /// 개별 UID의 순위를 얻는다. (없으면 false) 
    /// </summary>
    public static bool TryGetRankFor(string gameName, string uid, out int rank)
    {
        rank = 0;
        if (!store.TryGetValue(gameName, out var e) || e.Rankings == null || string.IsNullOrEmpty(uid))
            return false;
        return e.Rankings.TryGetValue(uid, out rank);
    }

    /// <summary> 
    /// 1등 UID 목록(동순위 모두) 반환. 없으면 빈 배열. 
    /// </summary>
    public static string[] GetTop1(string gameName)
    {
        if (!store.TryGetValue(gameName, out var e) || e.Rankings == null || e.Rankings.Count == 0)
            return System.Array.Empty<string>();

        int best = e.Rankings.Values.Min();
        return e.Rankings.Where(kv => kv.Value == best).Select(kv => kv.Key).ToArray();
    }

    /// <summary> 
    /// 상위 N명(동순위 포함) UID 배열 반환. (정렬 기준: rank asc) 
    /// </summary>
    public static string[] GetTop(string gameName, int n)
    {
        if (n <= 0) return System.Array.Empty<string>();
        if (!store.TryGetValue(gameName, out var e) || e.Rankings == null || e.Rankings.Count == 0)
            return System.Array.Empty<string>();

        return e.Rankings.OrderBy(kv => kv.Value).Take(n).Select(kv => kv.Key).ToArray();
    }

    /// <summary> 
    /// 소비 플래그 해제. 재반영이 정말 필요할 때만 사용. 
    /// </summary>
    public static void ResetConsume(string gameName)
    {
        if (store.TryGetValue(gameName, out var e)) e.Consumed = false;
    }

    /// <summary> 
    /// 해당 게임 결과 삭제 
    /// </summary>
    public static void Clear(string gameName)
    {
        if (store.ContainsKey(gameName)) store.Remove(gameName);
    }

    /// <summary> 
    /// 전체 결과 삭제 
    /// </summary>
    public static void ClearAll()
    {
        store.Clear();
    }
}