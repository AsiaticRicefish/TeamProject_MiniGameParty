using DesignPattern;
using LDH_Util;
using UnityEngine;

namespace LDH_MainGame
{
    public class MainGameManager : CombinedSingleton<MainGameManager>, IGameComponent
    {
        private GamePlayer[] _gamePlayers;


        protected override void OnAwake()
        {
            isPersistent = false;
            base.OnAwake();
        }

        public void Initialize()
        {
            Util_LDH.ConsoleLog(this, "MainGameManager 초기화 로직 실행");
        }

        public void StartGame()
        {
            Util_LDH.ConsoleLog(this, "게임 시작 호출");
        }
    }
}