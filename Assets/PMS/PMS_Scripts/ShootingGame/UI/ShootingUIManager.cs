using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_UI;
using Managers;
using Photon.Pun;
using PMS_Util;
using UnityEngine;
using UnityEngine.UI;

namespace ShootingScene.ShootingGame
{
    public class ShootingUIManager : CombinedSingleton<ShootingUIManager>, IGameComponent
    {
       [SerializeField] private UI_Screen_Wind windUI;
       [SerializeField] private UI_Screen_MyTurn myTurnUI;
       [SerializeField] private UI_Screen_OtherTurn otherTurnUI;
       [SerializeField] private UI_Screen_Timer timerUI;
       [SerializeField] private UI_Screen_PlayerRank playerRank;
       [SerializeField] private UI_Screen_Charging chargingUI;

        private Coroutine _timerCoroutine;
       
       
       protected override void OnAwake()
       {
           base.OnAwake();
           isPersistent = false;
       }

       public void Initialize()
       {
           TurnManager.Instance.OnSetCurrentTurn += otherTurnUI.SetCurrentPlayerName;
           RegisterTimer();
       }


        #region my turn ui

       public IEnumerator PlayMyTurnUI()
       {
           Debug.Log("[ShootingUIManager] 내 턴 ui show 시작");
           yield return Manager.UI.ShowScreenUI(myTurnUI).ToCoroutine();
           Debug.Log("[ShootingUIManager] 일시 정지");
           yield return new WaitForSecondsRealtime(myTurnUI.HoldTime);
           Debug.Log("[ShootingUIManager] 내 턴 ui close 시작");
           yield return Manager.UI.CloseScreenUI(myTurnUI).ToCoroutine();
       }

        #endregion

        #region timer ui
        public void RegisterTimer()
        {
            ShootingNetworkManager.Instance.networkTimer.OnTick += OnTimerTick;
            ShootingNetworkManager.Instance.networkTimer.OnTimerCancel += OnTimerEnd;
            ShootingNetworkManager.Instance.networkTimer.OnTimerEnd += OnTimerEnd;
        }

        public void UnRegisterTimer()
        {
            ShootingNetworkManager.Instance.networkTimer.OnTick -= OnTimerTick;
            ShootingNetworkManager.Instance.networkTimer.OnTimerCancel -= OnTimerEnd;
            ShootingNetworkManager.Instance.networkTimer.OnTimerEnd -= OnTimerEnd;
        }

        private async void OnTimerTick(int remaining)
        {
            if (!timerUI.gameObject.activeSelf)
            {
                await Manager.UI.ShowScreenUI(timerUI);
            }
            // 시간 업데이트
            timerUI.SetTimerText(remaining.ToString());

            SoundManager.Instance.PlaySFX(Define_PMS.SoundKeys.CountDownSFX);
        }

        private void OnTimerEnd()
        {
            if (timerUI == null) return; // null이면 종료
            // UI 텍스트 클리어
            //timerUI.SetTimerText("");
            // UI 닫기
            if (timerUI.gameObject.activeSelf)
            {
                Manager.UI.CloseScreenUI(timerUI).Forget();
            }
        }

        public void StartCountDown(double startAt, double endAt)
         {
            StopCountDown();
                   
            _timerCoroutine =  StartCoroutine(Co_CountDown(startAt, endAt));
         }
       
         public void StopCountDown(bool close = false)
         {
            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }
                   
            if(close && timerUI.gameObject.activeSelf)
                Manager.UI.CloseScreenUI(timerUI).Forget();
         }
               
        private IEnumerator Co_CountDown(double startAt, double endAt)
        {
            while (PhotonNetwork.Time<startAt)
            {
                yield return null;
            }
                   
            Debug.Log("카운트 다운 시작 : 타이머 UI Show");
            //카운트 다운 시작
            yield return Manager.UI.ShowScreenUI(timerUI).ToCoroutine();
                   
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
                    timerUI.SetTimerText(sec.ToString());
                }
       
                yield return null;
       
            }
                   
            Manager.UI.CloseScreenUI(timerUI).Forget();
       
            _timerCoroutine = null;
        }
        #endregion

        #region ranking
        //맨 처음 랭킹 UI를 넣어주기 UID - Color 매핑시켜줄 목적으로
        //턴 인덱스로 순위 정함
        public void StartRanking()
        {
            Dictionary<string, int> ranks = new Dictionary<string, int>();

            if (!playerRank.gameObject.activeSelf)
            {
                playerRank.gameObject.SetActive(true);
            }

            foreach(var player in PlayerManager.Instance.Players.Values)
            {
                ranks.Add(player.PlayerId, player.ShootingData.myTurnIndex);
            }

            playerRank.UpdateLiveRanks(ranks);
        }


        public void UpdateRanking(Dictionary<string,int> ranks)
        {
            playerRank.UpdateLiveRanks(ranks);
        }

        //마커에 입힐 랭킹 Color 가져오기
        public Color GetPlayerColor(string playerUID)
        {
            playerRank.GetColor(playerUID, out Color color);
            return color;
        }

        #endregion

        #region Wind UI
        public void ShowWindUI()
        {
            if (windUI.gameObject.activeInHierarchy) return;

            Manager.UI.ShowScreenUI(windUI).Forget();
        }
        #endregion

        public UI_Screen_Charging GetChargingUI()
        {
            return chargingUI;
        }

        #region charging UI
        public void ShowChargingUI()
        {
            Debug.Log("유니모 차징 UI 호출");

            if (chargingUI.IsVisible)
            {
                Manager.UI.CloseScreenUI(chargingUI).Forget();
                Debug.Log("유니모 차징 UI 끄기");
            }
            else
            {
                Manager.UI.ShowScreenUI(chargingUI).Forget();
                Debug.Log("유니모 차징 UI 켜기");
            }
        }

        public Slider GetSlider()
        {
            return chargingUI.GetComponent<Slider>();
        }
        #endregion
    }
}