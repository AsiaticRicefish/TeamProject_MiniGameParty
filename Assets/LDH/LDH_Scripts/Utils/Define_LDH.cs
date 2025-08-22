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

        public static class RoomProps
        {
            public const string MatchType = "matchType";   // "Quick", "Private"
            public const string MatchState = "matchState";  // "Matching", "Complete"
            public const string RoomCode  = "code"; // "1234" (private일 때만)
        }

        public enum MatchType { Quick, Private }
        public enum MatchState { Matching, Complete}

        public const int MAX_PLAYERS = 3;
        
        
        public static class PlayerProps
        {
            public const string ReadyState = "readyState"; //"Ready", "NotReady"
            
            // public 
        }
        

        #endregion








    }
}