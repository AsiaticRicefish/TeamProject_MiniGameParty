using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;

//다른 곳에서도 턴매니저가 있을 수 있으니깐 namespace처리 
namespace ShootingScene
{
    //원형 연결 리스트 사용  끝 -> 시작의 이동
    public class TurnManager : CombinedSingleton<TurnManager>, IGameComponent
    {
        private List<GameObject> playerList = new List<GameObject>();
        // TODO - 턴 매니저
        // 슈팅게임은 턴이 있어야한다.
        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - 슈팅 게임 TurnManager 초기화");
        }
    }
}
