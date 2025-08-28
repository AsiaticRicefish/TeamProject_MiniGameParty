using System;
using System.Collections;
using DesignPattern;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace RhythmGame
{
    public class GameManager : CombinedSingleton<GameManager>
    {
        // 과열관리

        // 게임 시간 관리
        [SerializeField] float gameTime = 180f; //게임 플레이타임
        public event Action OnGameOver; //게임 오버 여부에 따른 이벤트

        //게임 규칙
        [SerializeField] int hitScore = 100; //적중 시 점수
        [SerializeField] int overHeatPoint = 5; // 미스 시 과열 증가
        [SerializeField] int overHeatMaxValue = 100; // 임계치


        int overHeatValue = 0;// 마스터가 유지하는 공유 과열 값

        public event Action OnIsOverHeat; // 과열 발생

        Coroutine timerCo;

        /// <summary>
        /// 마스터가 전담하여 게임 로직 실행
        /// 라인 배정 → 스폰 → 타이머 시작
        /// </summary>
        public void StartGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 초기화

            // 모든 클라의 과열 스코어를 0으로 세팅 
            overHeatValue = 0;

            //과열 값 초기값 설정
            ScoreManager.Instance.photonView.
            RPC(nameof(ScoreManager.SetOverheat), RpcTarget.All, overHeatValue);

            // 플레이어 자리 배정
            LaneManager.Instance.SetLane();

            // 스폰 시작
            NoteSpawner.Instance.StartSpawn();

            // 타이머 시작
            if (timerCo != null) StopCoroutine(timerCo);
            timerCo = StartCoroutine(IE_Timer());
        }

        IEnumerator IE_Timer()
        {
            float end = Time.time + gameTime;
            while (Time.time < end) yield return null;
            //시간 초과 시 게임 종료
            EndGame();
        }

        /// <summary>
        /// 게임 종료 시
        /// </summary>
        public void EndGame()
        {
            //타이머 코루틴 초기화
            if (timerCo != null)
            {
                StopCoroutine(timerCo);
                timerCo = null;
            }

            NoteSpawner.Instance.StopSpawn();

            //게임 종료 이벤트 호출
            OnGameOver?.Invoke();
            Debug.Log("게임 오버");
        }

        /// <summary>
        /// Good 히트 → 개인 점수 증감
        /// </summary>
        public void GoodHitScore(Player actor)
        {
            if (!PhotonNetwork.IsMasterClient || actor == null) return;
            
            ScoreManager.Instance.photonView.
            RPC(nameof(ScoreManager.AddScore), RpcTarget.All, actor.ActorNumber, hitScore);
        }

        /// <summary>
        /// 미스(Miss)시 과열 증가
        /// </summary>
        /// <returns>최대치 도달 시 true</returns>
        public bool OverHeatCheck()
        {
            //마스터 클라이언트만 판별하도록
            if (!PhotonNetwork.IsMasterClient) return false;

            // 과열 변수 값 증가
            overHeatValue = Mathf.Max(0, overHeatValue + overHeatPoint);

            //과열 값 반영
            ScoreManager.Instance.photonView.RPC(
                nameof(ScoreManager.SetOverheat), RpcTarget.All, overHeatValue
                );

            // 과열 최대치 도달했을 경우
            if (overHeatValue >= overHeatMaxValue)
            {
                ScoreManager.Instance.photonView.RPC(nameof(RPC_IsOverHeat), RpcTarget.All);
                overHeatValue = 0; //과열점수 리셋

                ScoreManager.Instance.photonView.RPC(
                    nameof(ScoreManager.SetOverheat), RpcTarget.All, overHeatValue
                    );
                return true;
            }
            return false;
        }

        /// <summary>
        /// 과열 시 액션
        /// </summary>
        [PunRPC]
        public void RPC_IsOverHeat()
        {
            Debug.Log("과열 Warning! 모든 플레이어 기절!");

            OnIsOverHeat?.Invoke(); //과열 점수 초기화, 플레이어 이펙트 등등 설정
        }
    }
}