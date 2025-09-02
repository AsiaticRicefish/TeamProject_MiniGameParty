using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_UI;
using Managers;
using Photon.Pun;
using UnityEngine;

namespace ShootingScene.ShootingGame
{
    public class ShootingUIManager : CombinedSingleton<ShootingUIManager>, IGameComponent
    {
       [SerializeField] private UI_Screen_Wind _windUI;
       [SerializeField] private UI_Screen_MyTurn _myTurnUI;
       [SerializeField] private UI_Screen_OtherTurn _otherTurnUI;
       [SerializeField] private UI_Screen_Timer _timerUI;

       private Coroutine _timerCoroutine;
       
       
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
            Debug.Log("[ShootingUIManager] 내 턴 ui position reset");
            _myTurnUI.ResetPosition();
            Debug.Log("[ShootingUIManager] 내 턴 ui show 시작");
            yield return Manager.UI.ShowScreenUI(_myTurnUI).ToCoroutine();
            Debug.Log("[ShootingUIManager] 일시 정지");
            yield return new WaitForSecondsRealtime(_myTurnUI.HoldTime);
            Debug.Log("[ShootingUIManager] 내 턴 ui close 시작");
            yield return Manager.UI.CloseScreenUI(_myTurnUI).ToCoroutine();
        }

        public void StartCountDown(double startAt, double endAt)
        {
            StopCountDown();
            
            StartCoroutine(Co_CountDown(startAt, endAt));
        }

        public void StopCountDown()
        {
            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }
            
            if(_timerUI.gameObject.activeSelf)
                Manager.UI.CloseScreenUI(_timerUI).Forget();
        }
        
        private IEnumerator Co_CountDown(double startAt, double endAt)
        {
            while (PhotonNetwork.Time<startAt)
            {
                yield return null;
            }
            
            Debug.Log("카운트 다운 시작 : 타이머 UI Show");
            //카운트 다운 시작
            yield return Manager.UI.ShowScreenUI(_timerUI).ToCoroutine();
            
            int lastSec = -1;

            while (true)
            {
                double now = PhotonNetwork.Time;
                double remain = endAt - now;
                if(remain<= 0.0) break;
                
                int sec = Mathf.CeilToInt((float)remain);
                if (sec != lastSec)
                {
                    lastSec = sec;
                    _timerUI.SetTimerText(sec.ToString());
                }

                yield return null;

            }
            
            Manager.UI.CloseScreenUI(_timerUI).Forget();

            _timerCoroutine = null;
        }
        
    }
}