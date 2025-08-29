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
        // 게임 시간 관리
        [SerializeField] float gameTime = 180f; //게임 플레이타임
        public event Action OnGameOver; //게임 오버 여부에 따른 이벤트
        Coroutine timerCo;

        //게임 규칙
        [SerializeField] int hitScore = 100; //적중 시 점수
        [SerializeField] int overHeatPoint = 5; // 미스 시 과열 증가
        [SerializeField] int overHeatMaxValue = 100; // 임계치

        //과열 관리
        int overHeatValue = 0;// 마스터가 유지하는 공유 과열 값

        public event Action OnIsOverHeat; // 과열 발생

        //플레이어 자리
        [SerializeField] Transform[] playerPoints;
        //노트 스폰 오프셋
        [SerializeField] float noteSpawnDist = 12f;

        #region 게임 시작 종료 로직
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
            NoteSpawner.Instance.photonView.RPC(nameof(NoteSpawner.RPC_StartSpawn), RpcTarget.All);

            // 타이머 시작
            if (timerCo != null) StopCoroutine(timerCo);
            timerCo = StartCoroutine(IE_Timer());
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
        #endregion

        IEnumerator IE_Timer()
        {
            float end = Time.time + gameTime;
            while (Time.time < end) yield return null;
            //시간 초과 시 게임 종료
            EndGame();
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

        public int LaneCapacity => playerPoints?.Length ?? 0;


        public void PlaceActorToLane(int actorNumber, int lane)
        {
            //플레이어 컨트롤러가 액터 넘버 기준으로 딕셔너리에 등록돼 있는지 확인
            if (!PlayerController.AvatarByActor.TryGetValue(actorNumber, out var t))
            {
                //아닐 경우 코루틴으로 지연 후 확인
                StartCoroutine(IE_DelayPlace(actorNumber, lane));
                return;
            }
            //lane 인덱스 초과 방지
            int idx = Mathf.Clamp(lane - 1, 0, playerPoints.Length - 1);

            //해당 인덱스의 플레이어 위치 가져오기
            var p = playerPoints[idx];

            //아바타 위치 해당 위치로 이동
            t.SetPositionAndRotation(p.position, p.rotation);
        }

        
        IEnumerator IE_DelayPlace(int actorNumber, int lane)
        {
            //최대 10번 시도
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(0.1f);

                //아바타가 딕셔너리에 등록돼 있다면 배치 진행
                if (PlayerController.AvatarByActor.TryGetValue(actorNumber, out var t))
                {
                    int idx = Mathf.Clamp(lane - 1, 0, playerPoints.Length - 1);
                    var p = playerPoints[idx];
                    t.SetPositionAndRotation(p.position, p.rotation);
                    yield break;
                }
            }
            Debug.LogWarning($"액터넘버 : {actorNumber} 아바타를 찾지 못했습니다.");
        }

        public Pose GetLaneSpawnPose(int lane)
        {
            int idx = Mathf.Clamp(lane - 1, 0, playerPoints.Length - 1);
            var p = playerPoints[idx];

            //위치
            Vector3 pos = p.position + p.forward * noteSpawnDist;        // ✔ 앞쪽으로 오프셋
            //회전
            Quaternion rot = Quaternion.LookRotation(p.forward, Vector3.up);

            //위치, 회전
            return new Pose(pos, rot);
        }


    }
}