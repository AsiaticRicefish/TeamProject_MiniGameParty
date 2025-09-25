using System;
using System.Collections.Generic;
using Photon.Realtime;
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
        
        public enum ToastType
        {
            Error,
            Help,
            Notify,
            Check,
        }

        public static readonly Color32[] ColorBlindPalette = new[]
        {
            new Color32(228, 81,  91,255), 
            new Color32(229, 118,  0,255), // vermillion
            new Color32(  0,158,115,255), // bluish green
            new Color32(  0,114,178,255), // blue
            new Color32(155, 78,  234,255), 
            new Color32(  0,  0,  0,255), // black
        };


        #endregion

        #region User Info - Player Properties

        public static partial class PlayerProps
        {
            public enum PlayerInfoKey
            {
                Uid,
                CharacterId,
                EquipId,
            }

            public static readonly Dictionary<PlayerInfoKey, string> PlayerInfoKeyDict =
                new Dictionary<PlayerInfoKey, string>
                {
                    { PlayerInfoKey.Uid, "uid" },
                    { PlayerInfoKey.CharacterId, "characterId" },
                    { PlayerInfoKey.EquipId, "equipId" },
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

        public static string JoinErrorMessage(short code, string serverMsg)
        {
            int ec = code; // int로 승격
            
            switch (ec)
            {
                case ErrorCode.GameFull: return $"방이 가득 찼습니다.({code})";
                case ErrorCode.GameClosed: return $"방이 닫혀 있어 입장할 수 없습니다.({code})";
                case ErrorCode.NoRandomMatchFound: return $"입장 가능한 방이 없습니다.({code})";
                case ErrorCode.GameIdAlreadyExists: return $"같은 이름의 방이 이미 존재합니다.({code})";

                case ErrorCode.JoinFailedPeerAlreadyJoined:            // 32750
                    return $"이미 해당 방에 참여 중입니다.({code})";
                case ErrorCode.JoinFailedFoundInactiveJoiner:          // 32749
                    return $"이전 참가 기록이 남아 있습니다(비활성). 재접속을 시도하세요.({code})";
                case ErrorCode.JoinFailedWithRejoinerNotFound:         // 32748
                    return $"재접속 시간이 만료되어 방을 찾지 못했습니다.({code})";
                case ErrorCode.JoinFailedFoundExcludedUserId:          // 32747
                    return $"이 사용자 ID는 이 방에서 제외되었습니다.({code})";
                case ErrorCode.JoinFailedFoundActiveJoiner:            // 32746  ⬅︎ 추가
                    return $"같은 사용자 ID가 이미 방에 접속 중입니다. 재접속 모드로 시도하세요.({code})";

                case ErrorCode.GameDoesNotExist:                       // 32758
                    return $"해당 방을 찾을 수 없습니다.({code})";
                default:
                    return $"입장 실패 ({code}) {serverMsg}";
            }
        }
        
        #endregion
        
        #region Main Game

        public enum MainState
        {
            None,
            Intro, 
            Picking, 
            Ready, 
            LoadingMiniGame, 
            PlayingMiniGame, 
            UnloadingMiniGame,
            ApplyingResult, 
            End
        }

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
            public const string InGameResultDone = "inGameResultDone"; // 결과 연출 완료

        }

        public static partial class DefaultData
        {
            public const CurrencyType DefaultRewardCurrency = CurrencyType.Currency1;
            public const int DefaultReward = 50;
            public const int DefaultPointReward = 10;
        }
        
        #endregion


        #region Customizing

        public enum ItemType
        {
            Character,
            Equip,
        }

        public static partial class DefaultData
        {
            public const string DefaultCharacter = "unimo_ch_001";
            public const string DefaultEquip = "unimo_equip_001";
        }

        #endregion

        #region Currency

        public static partial class DefaultData
        {
            public const int DefaultCurrency1 = 10000;
            public const int DefaultCurrency2 = 10000;
            public const int DefaultCurrency3 = 10000;
        }
        
        public enum CurrencyType
        {
            Currency1,
            Currency2,
            Currency3,
        }

        public static int CurrencyCount => Enum.GetValues(typeof(CurrencyType)).Length;
        public const long MaxCurrencyValue = 999999999;
        public const long MinCurrencyValue = 0;
        
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

            public const string RTDB = "https://unimo-56ebc-default-rtdb.asia-southeast1.firebasedatabase.app/";
        }
        
        #endregion

        #region Sound

        public enum BgmKey
        {
            Lobby_BGM,
            Main_Win_Bgm,
            Main_MiniGameResult_Bgm,
        }

        public enum SfxKey
        {
            Lobby_Matching,
            Main_Coin,
            Main_Picking,
            Main_Roullet,
            Main_MiniGamePicked,
            Main_ReadyClick,
            Main_MiniGameEnd,
            Main_MatchEnd,
            Main_Loser,
            Main_Winner,
            Main_Reward,
            Main_ShowWinner,
            Main_PlayerBanner,
        }
        #endregion
        
        
    }

}