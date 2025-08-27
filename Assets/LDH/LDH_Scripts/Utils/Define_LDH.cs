using System.Collections.Generic;
using UnityEngine;

namespace LDH_Util
{
    public class Define_LDH
    {
        #region UI

        public enum UIEvent
        {
            Click,
            PointEnter,
            PointExit,
            Drag,
        }
        
        public enum UILayer
        {
            Screen,
            Popup,
            Toast
        }

        
        //screen ui 프리팹 이름(프리팹 이름 = 스크립트 이름이랑 동일해야 함)
        public enum ScreenUI
        {
            
        }
        
        public enum UIAreaType
        {
            Top,
            Center,
            Bottom,
            Default,
        }

        #endregion


        #region Match Making

        public static partial class RoomProps
        {
            public const string MatchType = "matchType";   // "Quick", "Private"
            public const string MatchState = "matchState";  // "Matching", "Complete"
            public const string RoomCode  = "code"; // "1234" (private일 때만)
        }

        public enum MatchType { Quick, Private, None }
        public enum MatchState { Matching, Complete, None}

        public const int MAX_PLAYERS = 2;
        public const int PRIVATE_MAX_RETRY = 5;
        
        
        public static class PlayerProps
        {
            public const string ReadyState = "readyState"; //bool 타입으로 true, false
            public const string SlotIndex = "slotIndex"; // 0~4까지의 숫자
        }

        #endregion


        #region Main Game
        
        public enum MainState {Init, Picking, Ready, LoadingMiniGame, PlayingMiniGame, ApplyingResult, End }

        public static partial class RoomProps
        {
            public const string State = "state";
            public const string Round = "round";
            public const string MiniGameId = "miniGameId";
            public const string ReadyMask = "readyMask";
            public const string DoneMask = "doneMask";
            public const string MiniGameResult = "miniGameResult";
        }
        #endregion
    }

}