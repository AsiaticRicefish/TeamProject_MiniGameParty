using System;
using System.Collections;
using System.Collections.Generic;
using LDH_Util;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using RP = LDH_Util.Define_LDH.RoomProps;

namespace LDH_MainGame
{
    public class MainGame_RoomPropController
    {
        private readonly Func<bool> _inRoom;
        private readonly Func<bool> _isJoined;
        private readonly Func<bool> _notLeaving;
        
        public MainGame_RoomPropController(Func<bool> inRoom, Func<bool> isJoined, Func<bool> notLeaving)
        {
            _inRoom = inRoom; _isJoined = isJoined; _notLeaving = notLeaving;
        }

        
        // --- 변경 가능한지 조건 체크
        private bool CanSet() => _inRoom() && _isJoined() && _notLeaving();

        
        // ---  Set Properties / Get Properties
        public T Get<T>(string key, T fallback)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room?.CustomProperties == null) return fallback;
            if (!room.CustomProperties.ContainsKey(key)) return fallback;
            try { return (T)room.CustomProperties[key]; } catch { return fallback; }
        }
        
        public void Set(Dictionary<string, object> kv)
        {
            if (!CanSet()) return;
            var ht = new Hashtable();
            foreach (var (k, v) in kv) if (v != null) ht[k] = v;
            PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
        }
        
        public void Set(string key, object value)
        {
            if (!CanSet()) return;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { key, value } });
        }


        
    }
}