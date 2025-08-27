using System;
using System.Collections;
using System.Collections.Generic;
using LDH_Util;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using RP = LDH_Util.Define_LDH.RoomProps;
using PP = LDH_Util.Define_LDH.PlayerProps;

namespace LDH_MainGame
{
    public class MainGame_PropertiesController
    {
        private readonly Func<bool> _inRoom;
        private readonly Func<bool> _isJoined;
        private readonly Func<bool> _notLeaving;
        
        public MainGame_PropertiesController(Func<bool> inRoom, Func<bool> isJoined, Func<bool> notLeaving)
        {
            _inRoom = inRoom; _isJoined = isJoined; _notLeaving = notLeaving;
        }

        
        // --- 변경 가능한지 조건 체크
        private bool CanSet() => _inRoom() && _isJoined() && _notLeaving();

        
        // ---  Set Properties / Get Properties
        public T GetRoomProps<T>(string key, T fallback)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room?.CustomProperties == null) return fallback;
            if (!room.CustomProperties.ContainsKey(key)) return fallback;
            try { return (T)room.CustomProperties[key]; } catch { return fallback; }
        }
        
        public void SetRoomProps(Dictionary<string, object> kv)
        {
            if (!CanSet()) return;
            var ht = new Hashtable();
            foreach (var (k, v) in kv) if (v != null) ht[k] = v;
            PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
        }
        
        public void SetRoomProps(string key, object value)
        {
            if (!CanSet()) return;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { key, value } });
        }


        #region PlayerProps

        public static int GetSlotIndex(Player p)
        {
            if (p == null) return -1;
            if (p.CustomProperties != null &&
                p.CustomProperties.TryGetValue(PP.SlotIndex, out var v) &&
                v is int idx) return idx;
            return -1;
        }

        public static bool GetReady(Player p)
        {
            return p != null &&
                   p.CustomProperties != null &&
                   p.CustomProperties.TryGetValue(PP.InGameReady, out var v) &&
                   v is bool b && b;
        }

        public static bool GetDone(Player p)
        {
            return p != null &&
                   p.CustomProperties != null &&
                   p.CustomProperties.TryGetValue(PP.InGameDone, out var v) &&
                   v is bool b && b;
        }

        public static void SetLocalReady(bool ready)
        {
            PhotonNetwork.LocalPlayer?.SetCustomProperties(new Hashtable { { PP.InGameReady, ready } });
        }

        public static void SetLocalDone(bool done)
        {
            PhotonNetwork.LocalPlayer?.SetCustomProperties(new Hashtable { { PP.InGameDone, done } });
        }

        public static void ClearLocalInGameProperties()
        {
            var ht = new Hashtable { { PP.InGameReady, false }, { PP.InGameDone, false } };
            PhotonNetwork.LocalPlayer?.SetCustomProperties(ht);
        }
        
        public bool AllPlayersReady()
        {
            var players = PhotonNetwork.PlayerList;
            if (players == null || players.Length == 0) return false;

            int present = 0;
            int ready   = 0;
            foreach (var p in players)
            {
                int slot = GetSlotIndex(p);
                if (slot < 0) continue;
                present |= (1 << slot);
                if (GetReady(p)) ready |= (1 << slot);
            }
            return present != 0 && (ready & present) == present;
        }

        public bool AllPlayersDone()
        {
            var players = PhotonNetwork.PlayerList;
            if (players == null || players.Length == 0) return false;

            int present = 0;
            int done    = 0;
            foreach (var p in players)
            {
                int slot = GetSlotIndex(p);
                if (slot < 0) continue;
                present |= (1 << slot);
                if (GetDone(p)) done |= (1 << slot);
            }
            return present != 0 && (done & present) == present;
        }
        
        
        /// <summary>
        /// UI용 편의 함수: 현재 PlayerProps 기반 Ready 비트마스크 생성
        /// (상태 판단에는 마스크를 저장하거나 사용하지 않지만, UI 업데이트에만 사용)
        /// </summary>
        public int BuildReadyMaskFromPlayers()
        {
            int mask = 0;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                int slot = GetSlotIndex(p);
                if (slot < 0) continue;
                if (GetReady(p)) mask |= (1 << slot);
            }
            return mask;
        }

        #endregion

        
    }
}