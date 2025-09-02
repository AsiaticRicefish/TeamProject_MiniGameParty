using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_UI;
using Managers;
using UnityEngine;

namespace ShootingScene.ShootingGame
{
    public class ShootingUIManager : CombinedSingleton<ShootingUIManager>, IGameComponent
    {
       [SerializeField] private UI_Screen_Wind _windUI;
       [SerializeField] private UI_Screen_MyTurn _myTurnUI;
       [SerializeField] private UI_Screen_OtherTurn _otherTurnUI;

       protected override void OnAwake()
       {
           base.OnAwake();
           isPersistent = false;
       }

       public void Initialize()
       {
           TurnManager.Instance.OnSetCurrentTurn += _otherTurnUI.SetCurrentPlayerName;
       }
        
        
        public IEnumerator PlayMyTurnUI()
        {
            Debug.Log("[ShootingUIManager] 내 턴 ui show 시작");
            yield return Manager.UI.ShowScreenUI(_myTurnUI).ToCoroutine();
            Debug.Log("[ShootingUIManager] 일시 정지");
            yield return new WaitForSecondsRealtime(_myTurnUI.HoldTime);
            Debug.Log("[ShootingUIManager] 내 턴 ui close 시작");
            yield return Manager.UI.CloseScreenUI(_myTurnUI).ToCoroutine();
        }
    }
}