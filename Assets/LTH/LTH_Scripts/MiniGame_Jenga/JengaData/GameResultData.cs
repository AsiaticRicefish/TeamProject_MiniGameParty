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
    private class RoundEntry
    {
        public string GameId;                   // 어떤 게임인지 (id)
        public string GameName;                 // 어떤 게임인지 (이름)
        public Dictionary<string, int> Rankings; // UID -> Rank(1,2,...)
        public bool Consumed;                    // 점수 반영 등으로 이미 사용했는지
    }

    private static readonly List<RoundEntry> rounds = new();
    private static readonly Dictionary<string, List<int>> indexByGame = new();  // 게임별로 라운드 인덱스를 모아둔 것


    private static void EnsureSize(int round)
    {
        while (rounds.Count <= round) rounds.Add(null);

    }
    
    /// <summary> 
    /// 결과 저장 (덮어씀). 외부에서 건드릴 수 없도록 방어 복사. 
    /// </summary>
    public static void SetRoundResult(int round, string gameId, string gameName, Dictionary<string,int> rankings)
    {
        if (round < 0 || string.IsNullOrEmpty(gameName) || rankings == null) return;
        EnsureSize(round); // 인덱스에 바로 값을 쓰기 위해서 round+1개의 원소가 있도록 

        rounds[round] = new RoundEntry
        { 
            GameId   = gameId,     
            GameName = gameName,
            Rankings = new Dictionary<string, int>(rankings),
            Consumed  = false
        };
        
        if (!indexByGame.TryGetValue(gameName, out var list))
        {
            //등록된게 없다면 새로운 리스트로 생성
            list = new List<int>();
            indexByGame[gameId] = list;
        }
        if (!list.Contains(round)) list.Add(round);
        
    }

    /// <summary> 
    /// 결과 조회 (복사본 반환). 없으면 null. 
    /// </summary>
    public static Dictionary<string, int> GetRoundResult(int round, out string gameId, out string gameName)
    {
        gameName = null;
        gameId = null;
        Dictionary<string, int> rankings = null;

        return TryGetRoundResult(round,  out gameId, out gameName, out rankings) ? rankings : null;
    }

    /// <summary> 
    /// 결과 조회 Try 버전 (복사본 반환). 
    /// </summary>
    public static bool TryGetRoundResult(int round, out string gameId, out string gameName, out Dictionary<string,int> rankings)
    {
        // 유효성 확인
        gameId = null; gameName = null; rankings = null;
        if (round < 0 || round >= rounds.Count) return false;
        var e = rounds[round];
        if (e?.Rankings == null) return false;

        gameId = e.GameId;
        gameName = e.GameName;
        rankings = new Dictionary<string,int>(e.Rankings);      // 복사본 반환
        return true;
    }

    /// <summary>
    /// 한 번만 소비: 아직 소비되지 않았다면 결과를 반환하고 Consumed=true로 표기.
    /// (점수 반영 루틴에서 이 메서드만 쓰면 중복 반영 방지 가능)
    /// </summary>
    public static bool TryConsumeRound(int round, out string gameName, out Dictionary<string,int> rankings)
    {
        gameName = null; rankings = null;
        if (round < 0 || round >= rounds.Count) return false;
        var e = rounds[round];
        if (e?.Rankings == null || e.Consumed) return false;

        e.Consumed = true;
        gameName = e.GameName;
        rankings = new Dictionary<string,int>(e.Rankings);
        return true;
    }

    /// <summary> 
    /// 개별 UID의 순위를 얻는다. (없으면 false) 
    /// </summary>
    public static bool TryGetRankForRound(int round, string uid, out int rank)
    {
        rank = 0;         
        if (round < 0 || round >= rounds.Count || string.IsNullOrEmpty(uid)) return false;
        
        var e = rounds[round];
        return e?.Rankings != null && e.Rankings.TryGetValue(uid, out rank);
    }

    /// <summary> 
    /// 1등 UID 목록(동순위 모두) 반환. 없으면 빈 배열. 
    /// </summary>
    public static string[] GetTop1FromRound(int round)
    {
        if (round < 0 || round >= rounds.Count) return System.Array.Empty<string>();
        var e = rounds[round];
        if (e?.Rankings == null || e.Rankings.Count == 0) return System.Array.Empty<string>();

        int best = e.Rankings.Values.Min();
        return e.Rankings.Where(kv => kv.Value == best).Select(kv => kv.Key).ToArray();
    }

    /// <summary> 
    /// 상위 N명(동순위 포함) UID 배열 반환. (정렬 기준: rank asc) 
    /// </summary>
    public static string[] GetTopFromRound(int round, int n)
    {
        if (n <= 0 || round < 0 || round >= rounds.Count) return System.Array.Empty<string>();
        var e = rounds[round];
        if (e?.Rankings == null || e.Rankings.Count == 0) return System.Array.Empty<string>();

        var ordered = 
            e.Rankings.OrderBy(kv => kv.Value)      // 랭크 순 오름차순 정렬
                .ThenBy(kv => kv.Key)               // UID 사전 순 정렬   
                .ToList();
        
        // n이 플레이 인원수보다 많으면 전체 결과 반환
        if (n >= ordered.Count) return ordered.Select(kv => kv.Key).ToArray();
        
        // n이 플레이 인원수보다 적으면 컷
        int cutoffRank = ordered[n - 1].Value;      //N번째의 랭크값이 컷오프 기준
        return ordered.Where(kv => kv.Value <= cutoffRank).Select(kv => kv.Key).ToArray();  // 컷오프 등수인 사람까지 포함해서 결과 반환
    }

    /// <summary>
    /// 특정 게임의 "가장 최근 라운드" 결과를 가져오기 (기존 gameName 기반 API 대체용)
    /// </summary>
    /// <returns></returns>
    public static bool TryGetLatestByGame(string gameName, out int round, out Dictionary<string, int> rankings)
    {
        round = -1; rankings = null;
        if (string.IsNullOrEmpty(gameName) || !indexByGame.TryGetValue(gameName, out var list) || list.Count == 0)
            return false;

        round = list.Max();
        var e = rounds[round];
        if (e?.Rankings == null) return false;

        rankings = new Dictionary<string,int>(e.Rankings);
        return true;
    }
    
    /// <summary> 
    /// 소비 플래그 해제. 재반영이 정말 필요할 때만 사용. 
    /// </summary>
    public static bool ResetConsumeRound(int round)
    {
        if (round < 0 || round >= rounds.Count) return false;
        var e = rounds[round];
        if (e == null) return false;
        e.Consumed = false;
        return true;
    }
    
    /// <summary> 
    /// 전체 결과 삭제 
    /// </summary>
    public static void ClearAll()
    {
        rounds.Clear();
        indexByGame.Clear();
    }
    
}