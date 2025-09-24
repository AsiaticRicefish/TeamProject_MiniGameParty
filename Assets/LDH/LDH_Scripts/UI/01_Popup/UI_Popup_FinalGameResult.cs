using System.Linq;
using System.Threading;
using Customization;
using Cysharp.Threading.Tasks;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static LDH_Util.Define_LDH;


namespace LDH_UI
{
    public class UI_Popup_FinalGameResult : UI_Popup_GameResult
    {
        private float afterWinnerDelay = 3.5f;

        protected override void Init()
        {
            base.Init();
            afterAwardDelay = 2f;
        }

        public async UniTask SetData(GamePlayer[] players)
        {
            if (title) title.text = $"최종 결과";
            if (subTitle) subTitle.text = $"최종 순위";
            
            _playerResults = players;
            
            //미니게임 순위 순으로 정렬되어 들어온 데이터
            for (int i = 0; i < players.Length; i++)
            {
                GamePlayer gp = players[i];

                var uiEntry = Instantiate(scoreEntryPrefab, scoreEntryContent);
                uiEntry.transform.SetSiblingIndex(i);
                
                await uiEntry.SetData(gp.Nickname, gp.LastMiniGameRank, gp.TotalRank, gp.CharacterId, gp.Score,
                    gp.WonThisRound, Define_LDH.DefaultData.DefaultRewardCurrency, gp.Reward);
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

            // 2) 우승자 연출
            var winnerUI = Manager.UI.CreatePopupUI<UI_Popup_Winner>();


            string uidKey = PlayerProps.GetPlayerInfoKey(PlayerProps.PlayerInfoKey.Uid);
            string winnerNickname = _playerResults[0].Nickname;
            string winnerUid = _playerResults[0].PlayerId;
            Player winnerPlayer = PhotonNetwork.CurrentRoom.Players.Values.FirstOrDefault(p =>
                p.CustomProperties != null &&
                p.CustomProperties.TryGetValue(uidKey, out var v) &&
                v is string uid &&
                uid == winnerUid);
            
            if (winnerPlayer == null)
            {
                Debug.LogError("Winner is null");
                return;
            }

            bool isWinner = winnerPlayer.IsLocal;
            
            string winnerCharID = winnerPlayer.CustomProperties[PlayerProps.GetPlayerInfoKey(PlayerProps.PlayerInfoKey.CharacterId)].ToString();
            string winnerEquipID = winnerPlayer.CustomProperties[PlayerProps.GetPlayerInfoKey(PlayerProps.PlayerInfoKey.EquipId)].ToString();
            
            await winnerUI.SetData(winnerNickname, new UnimoCombo(winnerCharID, winnerEquipID), isWinner );
            await Manager.UI.ShowPopupUI(winnerUI);
            await UniTask.Delay(System.TimeSpan.FromSeconds(afterWinnerDelay), cancellationToken: ct);  // 2초
            await Manager.UI.ClosePopupUI(winnerUI);
            
            
            // 3) 최종 보상 연출
            var rewardTasks = new System.Collections.Generic.List<UniTask>(_scoreEntries.Count);
            for (int i = 0; i < _scoreEntries.Count; i++)
            {
                rewardTasks.Add(_scoreEntries[i].ShowReward(ct));
            }
            await UniTask.WhenAll(rewardTasks);
            await UniTask.Delay(System.TimeSpan.FromSeconds(afterAwardDelay), cancellationToken: ct);  // 2초
            
        }
    }
}