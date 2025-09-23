using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_UI.Screen_MainGame;
using LDH_Util;
using LDH.LDH_Scripts.Test;
using Managers;
using Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LDH_MainGame
{
    public class MainGame_UIBinder
    {
        private readonly MiniGameRegistry _registry;
        private readonly Action<int> _setLocalSlot;
        private readonly Action<int> _onClickReady;
        
        // screen ui
        private UI_Screen_Introduce _introScreen;
        
        // popup
        private UI_Loading _loadingUI;
        private UI_Popup_SlotMachine _pickingUI;
        private UI_Popup_PrivateRoom _readyPanel;
        private UI_GameInfo _gameInfo;
        private UI_Popup_GameResult _resultPanel;
        private UI_Popup_Reward _rewardPanel;
        private UI_Popup_QuitGame _quitPopup;
        private UI_Popup_GameEnd _gameEndPopup;
        
        // const variable
        private const string loadingThemePath = "Data/Lobby_Theme";
        
        // 생성자
        public MainGame_UIBinder(MiniGameRegistry registry, Action<int> setLocalSlot, Action<int> onClickReady)
        {
            _registry = registry;
            _setLocalSlot = setLocalSlot;
            _onClickReady = onClickReady;
        }

        #region Intro UI

        public async UniTask BuildIntroScreen(Player[] players)
        {
            _introScreen = Manager.UI.CreateScreenUI<UI_Screen_Introduce>();
            
            await _introScreen.SetData(players);

            await Manager.UI.ShowScreenUI(_introScreen);
        }

        public async UniTask CloseIntroScreen()
        {
            if (_introScreen == null) return;
            await Manager.UI.CloseScreenUI(_introScreen, true);
            _introScreen = null;
        }
        
        #endregion

        #region SlotMachine

        public async UniTask BuildSlotMachine(List<string> candidates, int targetIndex, int currentRound)
        {
            _pickingUI = Manager.UI.CreatePopupUI<UI_Popup_SlotMachine>();
            await _pickingUI.SetData(candidates, targetIndex, currentRound);
            await Manager.UI.ShowPopupUI(_pickingUI);

        }
        
        public async UniTask CloseSlotMachine()
        {
            if (_pickingUI== null) return;
            await Manager.UI.ClosePopupUI(_pickingUI);
            _pickingUI = null;
        }
        
        public async UniTask PullHandle()
        {
            if(_pickingUI==null) return;
            await _pickingUI.PullHandle();
        }

        #endregion
        
        #region Ready Panel

        public void BuildReadyPanel(MiniGameInfo mini, Player[] players, bool isMaster, out int localSlot)
        {
            _readyPanel = Manager.UI.CreatePopupUI<UI_Popup_PrivateRoom>("UI_Popup_ReadyPanel");
            _gameInfo = _readyPanel.GetComponent<UI_GameInfo>();
            _gameInfo?.SetGameName(mini.gameName);
            _gameInfo?.SetPlayerCount(players.Length);

            _readyPanel.ResetAllSlots(isMaster);

            int ls = -1;
            foreach (var pl in players)
            {
                int slot = (int)pl.CustomProperties[Define_LDH.PlayerProps.SlotIndex];
                _readyPanel.SetPlayerPanel(slot, false, pl.IsLocal, pl.IsMasterClient, pl.NickName);
                _readyPanel[slot].SetInviteActive(false);
                if (pl.IsLocal) ls = slot;
            }

            _setLocalSlot(ls);
            localSlot = ls;

            foreach (var panel in _readyPanel.PlayerPanels)
                if (panel != null)
                    panel.ReadyClicked += _onClickReady;

            UniTask.Void(async () =>
            {
                await Manager.UI.ShowPopupUI(_readyPanel);
            });
        }

        public void UpdateReady(int mask) => _readyPanel?.UpdateReadyByMask(mask);

        public async UniTask CloseReadyPanel()
        {
            if (_readyPanel == null)
            {
                Debug.Log("ready panel is null");
                return;
            }

            foreach (var panel in _readyPanel.PlayerPanels)
                if (panel != null)
                    panel.ReadyClicked -= _onClickReady;

            await Manager.UI.ClosePopupUI(_readyPanel); // 패널 닫힐 때까지 기다리기
            _readyPanel = null;
            _gameInfo = null;
        }


        #endregion
        
        #region Loading / 결과 집계중 Loading

        public void ShowLoadingToLobby()
        {
            // 로딩창 설정
            _loadingUI = Manager.UI.CreatePopupUI<UI_Loading>();
            UI_LoadingTheme theme = Resources.Load<UI_LoadingTheme>(loadingThemePath);
            _loadingUI.ApplyTheme(theme);
            _loadingUI.SetBigDescription("로비로 이동 중...");
            _loadingUI.SetSmallDescription("잠시만 기다려 주세요.");
            _loadingUI.SetProgress(0f);
            
            
            // 로딩 UI 이벤트 설정
            _loadingUI.onSceneLoaded = (s) =>
            {
                if (!s.name.Equals(Manager.Network.LobbySceneName, StringComparison.Ordinal))
                    return;
                
                _loadingUI.SetProgress(1f);
                _loadingUI.AutoCloseAfter(1.5f, _loadingUI.destroyCancellationToken).Forget();
            };
            
            // 로딩창 띄우기
            Manager.UI.ShowPopupUI(_loadingUI).Forget();
        }

        public void ShowLoadingForResult()
        {
            _loadingUI = Manager.UI.CreatePopupUI<UI_Loading>();
            UI_LoadingTheme theme = Resources.Load<UI_LoadingTheme>(loadingThemePath);
            _loadingUI.ApplyTheme(theme);
            _loadingUI.SetTitle("라운드 종료");
            _loadingUI.SetBigDescription("결과 집계 중...");
            _loadingUI.SetSmallDescription("점수와 순위를 확인하는 중입니다.\n잠시만 기다려 주세요.");
            
            // 로딩창 띄우기
            Manager.UI.ShowPopupUI(_loadingUI).Forget();
        }

        public void CloseLoadingForResult()
        {
            if (_loadingUI == null)
            {
                Debug.LogError("Loading UI is null!!!");
                return;
            }
            
            _loadingUI.RequestClose();
        }
        
        public void SetLoadingProgress(float percent) => _loadingUI?.SetProgress(percent);


        #endregion

        #region Game End

        public async UniTask ShowGameEnd()
        {
            _gameEndPopup = Manager.UI.CreatePopupUI<UI_Popup_GameEnd>();
            await Manager.UI.ShowPopupUI(_gameEndPopup);
            await UniTask.Delay(TimeSpan.FromSeconds(1.8f));
            await Manager.UI.ClosePopupUI(_gameEndPopup);
            _gameEndPopup = null;
        }

        #endregion

        #region Score Panel / Result Panel / Reward Panel

        public async UniTask BuildScorePanel(int round, string gameName, GamePlayer[] players)
        {
            _resultPanel = Manager.UI.CreatePopupUI<UI_Popup_GameResult>();
            await  _resultPanel.SetData(round, gameName, players);
            await Manager.UI.ShowPopupUI(_resultPanel);
        }
        
        public void CloseScorePanel()
        {
            if(_resultPanel == null) return;
            Manager.UI.ClosePopupUI(_resultPanel).Forget();
            _resultPanel = null;
        }

        
        public async UniTask BuildFinalRewardPanel(GamePlayer[] players)
        {
            _resultPanel = Manager.UI.CreatePopupUI<UI_Popup_FinalGameResult>();
            await  _resultPanel.GetComponent<UI_Popup_FinalGameResult>().SetData(players);
            await Manager.UI.ShowPopupUI(_resultPanel);
        }

        public async UniTask ShowRewardPopup(GamePlayer localPlayer)
        {
            _rewardPanel = Manager.UI.CreatePopupUI<UI_Popup_Reward>();
            await _rewardPanel.SetData(localPlayer);
            await Manager.UI.ShowPopupUI(_rewardPanel);
        }

        #endregion

        #region 게임 강제 종료 팝업

        public void ShowQuitPopup()
        {
            if (_quitPopup != null) return;
            _quitPopup = Manager.UI.CreatePopupUI<UI_Popup_QuitGame>();
            Manager.UI.ShowPopupUI(_quitPopup).Forget();
        }

        public void CloseQuitPopup()
        {
            if (_quitPopup == null) return;
            Manager.UI.ClosePopupUI(_quitPopup).Forget();
            _quitPopup = null;
        }
        

        #endregion
        
    }
}