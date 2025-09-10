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

        #region User Info

        public static partial class PlayerProps
        {
            public enum PlayerInfoKey
            {
                Uid,
                CharacterId,
                MountId,
                ProfileId,
            }

            public static readonly Dictionary<PlayerInfoKey, string> PlayerInfoKeyDict =
                new Dictionary<PlayerInfoKey, string>
                {
                    { PlayerInfoKey.Uid, "uid" },
                    { PlayerInfoKey.CharacterId, "characterId" },
                    { PlayerInfoKey.MountId, "mountId" },
                    { PlayerInfoKey.ProfileId, "profileId" }
                };

            public static string GetPlayerInfoKey(PlayerInfoKey k) => PlayerInfoKeyDict[k];
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

        public static int MaxPlayers { get; set; } = 4;
        public const int PRIVATE_MAX_RETRY = 5;
        public const int QUICK_MAX_RETRY = 3;
        public const float QUICK_DELAY_MIN = 0.15f; // 초
        public const float QUICK_DELAY_MAX = 0.60f;
        public const int QUICK_INTERVAL = 5000;
        
        public static partial class PlayerProps
        {
            public const string ReadyState = "readyState"; //bool 타입으로 true, false
            public const string SlotIndex = "slotIndex"; // 0~4까지의 숫자
        }

        #endregion
        
        #region Main Game
        
        public enum MainState {Init, Picking, Ready, LoadingMiniGame, PlayingMiniGame, ApplyingResult, End }

        public static partial class RoomProps
        {
            public const string State = "mainState";
            public const string Round = "mainRound";
            public const string MiniGameId = "miniGameId";
            public const string ReadyMask = "readyMask";
            public const string DoneMask = "doneMask";
            public const string MiniGameResult = "miniGameResult";
        }
        
        public static partial class PlayerProps
        {
            public const string InGameReady = "inGameReady";
            public const string InGameDone = "inGameDone";
        }
        
        #endregion
        
        #region Shooting Game

        public enum WindDirection
        {
            North,
            South,
            West,
            East,
        }

        #endregion

        #region Setting

        /// <summary>
        /// UrlConfig(ScriptableObject)에 없는 경우 폴백으로 전달되는 url 주소
        /// </summary>
        public static class Urls
        {
            public const string Terms   = "https://hwiggames38434.imweb.me/termofuse";
            public const string Privacy = "https://hwig.games/?mode=privacy";
            public const string Support = "https://www.notion.so/2697de437a0c8007b7eeceb6a707547a?source=copy_link";
        }

        #endregion
    }

}