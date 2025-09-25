using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_MainGame;
using LDH_Util;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Popup_GameResult : UI_Popup
    {
        [SerializeField] protected TMP_Text title;
        [SerializeField] protected TMP_Text subTitle;
        [SerializeField] protected UI_ScoreEntry scoreEntryPrefab;
        [SerializeField] protected Transform scoreEntryContent;


        [Header("Timings")] 
        protected float startDelay = 0.3f;
        protected float appearStagger = 0.3f; // 항목 등장 간격
        protected float afterAppearDelay = 1f; // 전부 등장 완료 후 대기
        protected float afterAwardDelay = 0.8f; // +1 연출 후 대기
        protected float rankRevealStagger = 0.5f; // 전체등수 공개 간격
        protected float endDelay = 2f;


        protected GamePlayer[] _playerResults;
        protected List<UI_ScoreEntry> _scoreEntries = new();

        
        public async UniTask SetData(int round, string gameName, GamePlayer[] players)
        {
            if (title) title.text = $"{round}라운드 결과";
            if (subTitle) subTitle.text = $"{gameName} 순위";

            _playerResults = players;

            //미니게임 순위 순으로 정렬되어 들어온 데이터
            for (int i = 0; i < players.Length; i++)
            {
                GamePlayer gp = players[i];

                var uiEntry = Instantiate(scoreEntryPrefab, scoreEntryContent);
                uiEntry.transform.SetSiblingIndex(i);

                Debug.Log(gp.PlayerId);

                Player player = null;
                foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
                {
                    if (p.CustomProperties.TryGetValue(
                            Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.Uid),
                            out object value) && value is string playerUid && playerUid.Equals(gp.PlayerId))
                    {
                        player = p;
                        break;
                    }
                }
                
                if (player == null)
                {
                    Debug.LogWarning("player is null");
                    return;
                }

                string profileId = player
                    .CustomProperties[
                        Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId)]
                    .ToString();

                await uiEntry.SetData(gp.Nickname, gp.LastMiniGameRank, gp.TotalRank, profileId, gp.Score, gp.WonThisRound);
                _scoreEntries.Add(uiEntry);
            }
        }


        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            cg.alpha = 1f;

            await UniTask.Delay(System.TimeSpan.FromSeconds(startDelay), cancellationToken: ct);    //0.3f;
            
            // 1) 좌→우 순차 등장
            var appearTasks = new System.Collections.Generic.List<UniTask>(_scoreEntries.Count);
            for (int i = 0; i < _scoreEntries.Count; i++)
            {
                appearTasks.Add(_scoreEntries[i].PlayAppearAsync(i * appearStagger, ct));
            }

            await UniTask.WhenAll(appearTasks);
            await UniTask.Delay(System.TimeSpan.FromSeconds(afterAppearDelay), cancellationToken: ct);  // 1초

            // 2) 1등(공동 포함) 하이라이트 + +1 연출
            var awardTasks = new System.Collections.Generic.List<UniTask>(_scoreEntries.Count);
            for (int i = 0; i < _scoreEntries.Count; i++)
            {
                if (_playerResults[i].WonThisRound)
                    awardTasks.Add(_scoreEntries[i].PlayWinnerAndAddPointAsync(ct));
            }

            await UniTask.WhenAll(awardTasks);
            await UniTask.Delay(System.TimeSpan.FromSeconds(afterAwardDelay), cancellationToken: ct);      


            // 미니게임 순위 가리기
            for (int i = 0; i < _scoreEntries.Count; i++)
            {
                _scoreEntries[i].HideMiniGameRank();
            }

            // 3) 전체 등수 공개(1등 → N등 순차)
            var revealOrder = _playerResults
                .Select((e, idx) => new { e.TotalRank, idx })
                .OrderBy(x => x.TotalRank) // 1,2,2,4...
                .ThenBy(x => x.idx) // 안정화
                .Select(x => x.idx)
                .ToArray();

            for (int k = 0; k < revealOrder.Length; k++)
            {
                _scoreEntries[revealOrder[k]].ShowTotalRank();
                await UniTask.Delay(System.TimeSpan.FromSeconds(rankRevealStagger), cancellationToken: ct);
            }
            
            await UniTask.Delay(System.TimeSpan.FromSeconds(endDelay), cancellationToken: ct);
    
        }
    }
}