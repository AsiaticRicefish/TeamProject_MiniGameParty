using System;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Realtime;
using UnityEngine;

namespace LDH_MainGame
{
    public class MainGame_UIBinder
    {
        private readonly MiniGameRegistry _registry;
        private readonly Action<int> _setLocalSlot;
        private readonly Action<int> _onClickReady;
        
        private UI_Popup_PrivateRoom _readyPanel;
        private UI_GameInfo _gameInfo;
        
        // 생성자
        // 생성자
        public MainGame_UIBinder(MiniGameRegistry registry, Action<int> setLocalSlot, Action<int> onClickReady)
        {
            _registry = registry;
            _setLocalSlot = setLocalSlot;
            _onClickReady = onClickReady;
        }

        
        public void BuildReadyPanel(MiniGameInfo mini, Player[] players, bool isMaster, out int localSlot)
        {
            _readyPanel = Manager.UI.CreatePopupUI<UI_Popup_PrivateRoom>("UI_Popup_ReadyPanel");
            _gameInfo   = _readyPanel.GetComponent<UI_GameInfo>();
            _gameInfo?.SetGameName(mini.gameName);
            _gameInfo?.SetPlayerCount(players.Length);

            _readyPanel.ResetAllSlots(isMaster);

            int ls = -1;
            foreach (var pl in players)
            {
                int slot = (int)pl.CustomProperties[Define_LDH.PlayerProps.SlotIndex];
                _readyPanel.SetPlayerPanel(slot, false, pl.IsLocal, pl.IsMasterClient);
                _readyPanel[slot].SetInviteActive(false);
                if (pl.IsLocal) ls = slot;
            }
            _setLocalSlot(ls);
            localSlot = ls;

            foreach (var panel in _readyPanel.PlayerPanels)
                if (panel != null) panel.ReadyClicked += _onClickReady;

            Manager.UI.ShowPopupUI(_readyPanel).Forget();
        }
        
        public void UpdateReady(int mask) => _readyPanel?.UpdateReadyByMask(mask);

        public void CloseReadyPanel()
        {
            if (_readyPanel == null) return;
            foreach (var panel in _readyPanel.PlayerPanels)
                if (panel != null) panel.ReadyClicked -= _onClickReady;

            UniTask.Void(async () => { await Manager.UI.ClosePopupUI(_readyPanel); });
            _readyPanel = null; _gameInfo = null;
        }


    }
}