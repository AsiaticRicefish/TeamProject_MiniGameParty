using System.Collections;
using System.Collections.Generic;
using LDH_Util;
using UnityEngine;

namespace PMS_Util
{
    public enum SH_GameStateType
    {
        InitState,
        CardSelect,
        GamePlay,
        TurnCheck,
        CheckGameWinner,
        GameEnd
    }

    public static class GameStateName
    {
        public static string ToString(SH_GameStateType stateType) => stateType switch
        {
            SH_GameStateType.InitState => "InitState",
            SH_GameStateType.CardSelect => "CardSelectState",
            SH_GameStateType.GamePlay => "GamePlayState",
            SH_GameStateType.TurnCheck => "TurnCheckState",
            SH_GameStateType.CheckGameWinner => "GameWinnerCheckState",
            SH_GameStateType.GameEnd => "GameEndState",
            _ => "Unknown"
        };
    }

    public class Define_PMS
    {

        public static class SoundKeys
        {
            //BGM
            public static readonly string ShootingBGM = "ShootingBGM";

            //SFX
            public static readonly string CardFlipSFX = "CardFlipSFX";
            public static readonly string ButtonTouchSFX = "ButtonTouchSFX";
            public static readonly string MyTurnSFX = "MyTurnSFX";
            public static readonly string CountDownSFX = "CountDownSFX";
            public static readonly string UnimoShootingSFX = "UnimoShootingSFX";
            public static readonly string UninmoCollisionSFX = "UninmoCollisionSFX";
            public static readonly string GameWinnerSFX = "GameWinnerSFX";
            public static readonly string GameLoseSFX = "GameLoseSFX";
            public static readonly string GameEndSFX = "GameEndSFX";
        }

        /*
        public enum SH_GameStateType
        {
            CardSelect,
            GamePlay,
            TurnCheck,
            CheckGameWinner,
            GameEnd
        }

        public static class GameStateName
        {
            public static string ToString(SH_GameStateType stateType) => stateType switch
            {
                SH_GameStateType.CardSelect => "CardSelectState",
                SH_GameStateType.GamePlay => "GamePlayState",
                SH_GameStateType.TurnCheck => "TurnCheckState",
                SH_GameStateType.CheckGameWinner => "GameWinnerCheckState",
                SH_GameStateType.GameEnd => "GameEndState",
                _ => "Unknown"
            };
        }
        */
    }
}
