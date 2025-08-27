// using System;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
//
//
// namespace LDH_MainGame
// {
//     [Serializable]
//     public class RoundRecord
//     {
//         public int round;
//         public MiniGameResult result;
//         public DateTime time;
//     }
//
//     [Serializable]
//     public class PlayerAggregate
//     {
//         public string uid;
//         public int totalScore;
//         public int wins; // 1등 횟수
//         public Dictionary<int, int> placements = new(); // 등수 -> 횟수
//
//         public void ApplyPlacement(int place)
//         {
//             if (!placements.ContainsKey(place)) placements[place] = 0;
//             placements[place]++;
//             if (place == 1) wins++;
//         }
//     }
//     
//     public class MiniGameResult : MonoBehaviour
//     {
//         
//         public List<RoundRecord> rounds = new();
//         public Dictionary<string, PlayerAggregate> aggregates = new(); // uid -> agg
//
//         public void AddRound(int roundIndex, MiniGameResult res, Func<int, string> actorToUid)
//         {
//             rounds.Add(new RoundRecord { round = roundIndex, result = res, time = DateTime.Now });
//
//             // playerScore: ActorNumber -> 점수(또는 등수)
//             foreach (var kv in res.playerScore)
//             {
//                 var uid = actorToUid(kv.Key);
//                 if (string.IsNullOrEmpty(uid)) continue;
//
//                 if (!aggregates.TryGetValue(uid, out var agg))
//                 {
//                     agg = new PlayerAggregate { uid = uid };
//                     aggregates[uid] = agg;
//                 }
//
//                 agg.totalScore += kv.Value;
//
//                 // 등수가 점수 대신 들어오는 게임일 수도 있으니,
//                 // 필요하면 MiniGameResult.payloadJson에서 “place” 맵을 꺼내 반영해도 됨.
//                 // 여기서는 간단히 "낮은 수치가 더 좋은 등수"라고 가정할 땐 별도 로직으로 매핑하세요.
//             }
//         }
//
//         public PlayerAggregate GetAggregate(string uid) =>
//             aggregates.TryGetValue(uid, out var agg) ? agg : null;
//
//         public void ClearAll()
//         {
//             rounds.Clear();
//             aggregates.Clear();
//         }
//     }