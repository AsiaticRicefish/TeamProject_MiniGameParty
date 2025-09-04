using System;
using DesignPattern;
using Photon.Pun;
using UnityEngine;

namespace RhythmGame
{
    public class ScoreManager : PunSingleton<ScoreManager>
    {
        //점수
        int _score; //개인 별 점수
        int _heatScore; //과열 점수
        public int Score => _score;
        public int HeatScore => _heatScore;

        //이벤트
        public event Action<int> OnScoreChanged;
        public event Action<int> OnOverHeatScoreChanaged;
        public event Action OnHeatScoreOver;

        /// <summary>
        /// 점수 추가 로직
        /// 
        /// 점수 추가 및 이벤트 호출
        /// </summary>
        /// <param name="amount">획득 점수량</param>
        public void AddScore(int amount)
        {
            if (amount < 0)
            {
                MinusScore(amount);
                return;
            }
            _score += amount;
            Debug.Log($" 점수 획득 {amount}");
            OnScoreChanged?.Invoke(_score);
        }

        /// <summary>
        /// 점수 차감 로직
        /// 
        /// 차감될때 점수가 0보다 이하일 경우 0으로 설정
        /// </summary>
        /// <param name="amount">차감 점수량(음수)</param>
        public void MinusScore(int amount)
        {
            _score += amount;

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
                AddScore(score);
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

        // /// <summary>
        // /// 과열 시 액션
        // /// </summary>
        // [PunRPC]
        // public void RPC_IsOverHeat()
        // {
        //     Debug.Log("과열 Warning! 모든 플레이어 기절!");

        //     // OnIsOverHeat?.Invoke(); //과열 점수 초기화, 플레이어 이펙트 등등 설정
        // }

        #endregion


        #region 판정관련 로직
        // 클라 → 마스터: 히트 요청(판정 포함)
        public void RequestHit(int noteId, bool isCanInteract, NoteType type)
        {
            photonView.RPC(nameof(RPC_RequestHit), RpcTarget.MasterClient, noteId, isCanInteract, type);
        }

        [PunRPC]
        void RPC_RequestHit(int noteId, bool isCanInteract, NoteType type, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 라인 검증 (내 라인의 노트인지 판별하기)

            if (!LaneManager.Instance.LaneByNoteId.TryGetValue(noteId, out int noteLane)) return;
            if (!LaneManager.Instance.GetLane(info.Sender.ActorNumber, out int actorLane)) return;

            //내 레인이 아닐 경우에
            if (noteLane != actorLane)
            {
                //TODO 김승태 : 내 레인과 상대 레인에 노트가 동시에 도착하는 경우 과열처리가 날 수도 있음. 이걸 방지하는 코드가 필요함.
                //과열 점수가 오르도록
                GameManager.Instance.MissBlock(info.Sender);
                return;
            }

            // 득점 및 과열 처리
            if (isCanInteract)
            {
                GameManager.Instance.GoodHitScore(type, info.Sender);
            }
            else
            {
                GameManager.Instance.OverHeatCheck();

                GameManager.Instance.MissBlock(info.Sender);

            }

            // 파괴
            LaneManager.Instance.LaneByNoteId.Remove(noteId);
            NoteSpawner.Instance.DestoryNote(noteId);
        }

        public void RequestMiss()
        {
            photonView.RPC(nameof(RPC_RequestMiss), RpcTarget.MasterClient);
        }

        [PunRPC]
        void RPC_RequestMiss(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            GameManager.Instance.OverHeatCheck();
            GameManager.Instance.MissBlock(info.Sender);
        }

        #endregion

    }
}