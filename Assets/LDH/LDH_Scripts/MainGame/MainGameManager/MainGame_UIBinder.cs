using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using LDH.LDH_Scripts.Test;
using Managers;
using Network;
using Photon.Realtime;
using UnityEngine;

namespace LDH_MainGame
{
    public class MainGame_UIBinder
    {
        private readonly MiniGameRegistry _registry;
        private readonly Action<int> _setLocalSlot;
        private readonly Action<int> _onClickReady;
        
        private MainGameDebugPanel _debugUI;
        private UI_Popup_PrivateRoom _readyPanel;
        private UI_GameInfo _gameInfo;

        private List<UI_Screen> _mainGameScreenUIs;
        private UI_Popup_QuitGame _quitPopup;

        private UI_Loading _loadingUI;
        private const string loadingThemePath = "Data/Lobby_Theme";


        // 생성자
        // 생성자
        public MainGame_UIBinder(MiniGameRegistry registry, Action<int> setLocalSlot, Action<int> onClickReady)
        {
            _registry = registry;
            _setLocalSlot = setLocalSlot;
            _onClickReady = onClickReady;
            _mainGameScreenUIs = new List<UI_Screen>();
        }


        public void SetDebugUI()
        {
            _debugUI = Manager.UI.CreateScreenUI<MainGameDebugPanel>();
            _mainGameScreenUIs.Add(_debugUI);
            SetActiveDebugUI(true);
        }

        public void SetActiveDebugUI(bool active)
        {
            if (active)
                Manager.UI.ShowScreenUI(_debugUI).Forget();
            else
                Manager.UI.CloseScreenUI(_debugUI, false).Forget();
        }

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


        public async UniTask CloseAllScreenUI()
        {
            List<UniTask> tasks = new List<UniTask>();
            
            foreach (UI_Screen screenUI in _mainGameScreenUIs)
            {
                tasks.Add(Manager.UI.CloseScreenUI(screenUI, true));
            }
            
            await UniTask.WhenAll(tasks);
        }

        
        public void ShowLoading()
        {
            // 로딩창 설정
            _loadingUI = Managers.Manager.UI.CreatePopupUI<UI_Loading>();
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
        
        public void SetLoadingProgress(float percent) => _loadingUI?.SetProgress(percent);
        

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