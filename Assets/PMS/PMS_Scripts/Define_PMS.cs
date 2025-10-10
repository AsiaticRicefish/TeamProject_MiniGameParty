using System;
using System.Collections;
using System.Collections.Generic;
using LDH_Util;
using UnityEngine;

namespace PMS_Util
{
    [Flags]
    public enum InputMode
    {
        None = 0,
        Gameplay = 1 << 0,   // 터치, 발사 등 게임플레이
        Camera = 1 << 1,   // 스와이프, 줌 등 카메라 제어
        UI = 1 << 2,   // UI 버튼, 네비게이션
        All = Gameplay | Camera | UI // OR 연산
    }

    public enum SH_GameStateType
    {
        None,
        Init,
        CardSelect,
        GamePlay,
        TurnCheck,
        CheckGameWinner,
        GameEnd,
        Pause
    }

    public static class GameStateName
    {
        public static string ToString(SH_GameStateType stateType) => stateType switch
        {
            SH_GameStateType.Init => "InitState",
            SH_GameStateType.CardSelect => "CardSelectState",
            SH_GameStateType.GamePlay => "GamePlayState",
            SH_GameStateType.TurnCheck => "TurnCheckState",
            SH_GameStateType.CheckGameWinner => "GameWinnerCheckState",
            SH_GameStateType.GameEnd => "GameEndState",
            SH_GameStateType.Pause => "PauseState",
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
