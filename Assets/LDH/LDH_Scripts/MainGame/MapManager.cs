using DesignPattern;
using LDH_Util;
using UnityEngine;

namespace LDH_MainGame
{
    public class MapManager : CombinedSingleton<MapManager>,IGameComponent
    {
        protected override void OnAwake()
        {
            isPersistent = false;
            base.OnAwake();
        }
        public void Initialize()
        {
            Util_LDH.ConsoleLog(this, "MapManager 초기화 로직 실행");
        }
    }
}