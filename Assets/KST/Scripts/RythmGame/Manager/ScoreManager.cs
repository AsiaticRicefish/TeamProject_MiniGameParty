using System;
using DesignPattern;
using Photon.Pun;
using UnityEngine;

namespace RhythmGame
{
    public class ScoreManager : PunSingleton<ScoreManager>
    {
        int _score; //개인 별 점수
        int _heatScore; //과열 점수
        public event Action<int> OnScoreChanged;
        public event Action<int> OnOverHeatScoreChanaged;
        public event Action OnHeatScoreOver;

        /// <summary>
        /// 점수 추가 로직
        /// 
        /// 점수 추가 및 이벤트 호출
        /// </summary>
        /// <param name="amount">획득 점수량</param>
        public void AddScroe(int amount)
        {
            _score += amount;
            Debug.Log($" 점수 획득 {amount}");
            OnScoreChanged?.Invoke(_score);
        }

        /// <summary>
        /// 점수 차감 로직
        /// 
        /// 차감될때 점수가 0보다 이하일 경우 0으로 설정
        /// </summary>
        /// <param name="amount">차감 점수량</param>
        public void MinusScore(int amount)
        {
            _score -= amount;
            Debug.Log($" 점수 차감 {amount}");
            if (_score < 0) _score = 0;
            OnScoreChanged?.Invoke(_score);
        }

        #region RPC

        /// <summary>
        /// 해당 플레이어에게 일정 점수 부여
        /// </summary>
        /// <param name="actorNumber">해당 플레이어 액터넘버</param>
        /// <param name="score">획득 점수</param>
        [PunRPC]
        public void AddScore(int actorNumber, int score)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber == actorNumber)
                AddScroe(score);
        }

        /// <summary>
        /// 해당 플레이어에게 일정 점수만큼 차감
        /// </summary>
        /// <param name="actorNumber">해당 플레이어 액터넘버</param>
        /// <param name="score">차감 점수</param>
        [PunRPC]
        public void MinusScore(int actorNumber, int score)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber == actorNumber)
                MinusScore(score);
        }

        /// <summary>
        /// 과열값 최신화
        /// </summary>
        [PunRPC]
        public void SetOverheat(int value)
        {
            _heatScore = Mathf.Max(0, value);
            OnOverHeatScoreChanaged?.Invoke(_heatScore);
            Debug.Log($"과열 점수 : {_heatScore}");
        }
               
        #endregion
    }
}