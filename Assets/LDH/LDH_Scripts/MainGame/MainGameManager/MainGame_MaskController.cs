using LDH_Util;
using Photon.Pun;
using UnityEngine;

namespace LDH_MainGame
{
    public class MainGame_MaskController
    {
        public enum MaskType
        {
            Ready,
            Done,
        }
        private readonly MainGame_PropertiesController _rpc;

        // 생성자
        public MainGame_MaskController(MainGame_PropertiesController rpc)
        {
            _rpc = rpc;
        }

        
        // Util  ------ 
        public int GetPresentMask()
        {
            int m = 0;
            foreach (var p in PhotonNetwork.PlayerList)
                if (p.CustomProperties.TryGetValue(Define_LDH.PlayerProps.SlotIndex, out var v) && v is int slot)
                    m |= (1 << slot);
            return m;
        }
        
        
        public bool AreAllPresentOn(int mask)
        {
            int present = GetPresentMask();
            return present != 0 && (mask & present) == present;
        }
        
        
        // Ready
        public int ToggleReady(int slot, bool on) => ToggleType(MaskType.Ready, slot, on);
        public bool IsAllReady(int mask) => AreAllPresentOn(mask);
        
        // Done
        public int ToggleDone(int slot, bool on) => ToggleType(MaskType.Done, slot, on);
        public bool IsAllDone(int mask) => AreAllPresentOn(mask);


        private int ToggleType(MaskType maskType, int slot, bool on)
        {
            int mask = maskType switch
            {
                MaskType.Ready =>_rpc.GetRoomProps(Define_LDH.RoomProps.ReadyMask, 0),
                MaskType.Done => _rpc.GetRoomProps(Define_LDH.RoomProps.DoneMask, 0),
                _ => 0,
            };
              
            return on ? (mask | (1 << slot)) : (mask & ~(1 << slot));
        }
        
    }
}