using System;
using System.Collections;
using DesignPattern;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace RhythmGame
{
    // public class GameManager : CombinedSingleton<GameManager>
    public class GameManager : PunSingleton<GameManager>
    {
        // 게임 시간 관리
        [SerializeField] float gameTime = 180f; //게임 플레이타임

        //TODO 김승태 게임 플레이 시간 (임시) 변경 예정
        [SerializeField] TMP_Text gameTimer;
        public bool IsGameStart = false;
        public event Action OnGameStart; //게임 시작 이벤트
        public event Action OnGameOver; //게임 오버 여부에 따른 이벤트
        Coroutine timerCo;

        //게임 규칙
        // [SerializeField] int hitScore = 100; //적중 시 점수
        [SerializeField] int missScore = -5; // 미스 시 감점 점수
        [SerializeField] int overHeatPoint = 5; // 미스 시 과열 증가
        [SerializeField] int frozenPoint = 5; // 적중 시 과열 감소
        [SerializeField] int overHeatMaxValue = 100; // 임계치

        //과열 관리
        int overHeatValue = 0;// 마스터가 유지하는 공유 과열 값
        public bool IsOverHeat = false; //과열여부
        public event Action OnIsOverHeat; // 과열 발생
        [SerializeField] float overHeatingTime = 3f; //과열 유지 시간

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

            photonView.RPC(nameof(GameStartSettings), RpcTarget.All);
        }

        [PunRPC]
        public void GameStartSettings()
        {
            //게임 시작 플래그 설정
            IsGameStart = true;
            OnGameStart?.Invoke();
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
        public void GoodHitScore(NoteType type, Player actor)
        {
            if (!PhotonNetwork.IsMasterClient || actor == null) return;

            int score = CalculateNote(type);
            Debug.Log($"판정 노트 타입 {type}");
            ScoreManager.Instance.photonView.
            RPC(nameof(ScoreManager.AddScore), RpcTarget.All, actor.ActorNumber, score);

        }

        //판정 관련 로직, NoteType에 따라 점수 반영 다르도록
        public int CalculateNote(NoteType type)
        {
            int score = 0;
            switch (type)
            {
                case NoteType.Fake:
                    score = -1;
                    OverHeatCheck();
                    break;

                case NoteType.Touch:
                    score = 2;
                    FrozenHeat();
                    break;

                case NoteType.Continue:
                    score = 10;
                    FrozenHeat();
                    break;
            }

            return score;
        }

        public void FrozenHeat()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 과열 변수 값 감소
            overHeatValue = Mathf.Max(0, overHeatValue - frozenPoint);

            //과열 값 반영
            ScoreManager.Instance.photonView.RPC(
                nameof(ScoreManager.SetOverheat), RpcTarget.All, overHeatValue
                );


        }

        /// <summary>
        /// 과열 증가 로직
        /// 미스(Miss)시 혹은 타 조건 만족 시 해당 로직 호출
        /// </summary>
        public void OverHeatCheck()
        {
            //마스터 클라이언트만 판별하도록
            if (!PhotonNetwork.IsMasterClient) return;

            // 과열 변수 값 증가
            overHeatValue = Mathf.Max(0, overHeatValue + overHeatPoint);

            //과열 값 반영
            ScoreManager.Instance.photonView.RPC(
                nameof(ScoreManager.SetOverheat), RpcTarget.All, overHeatValue
                );

            // 과열 최대치 도달했을 경우
            if (overHeatValue >= overHeatMaxValue)
            {
                // ScoreManager.Instance.photonView.RPC(nameof(ScoreManager.RPC_IsOverHeat), RpcTarget.All);

                //과열 코루틴 실행
                StartCoroutine(IE_OverHeating());
            }
        }

        /// <summary>
        /// 미스 시 개인점수 차감
        /// </summary>
        public void MissBlock(Player actor)
        {
            //개인 점수 차감
            ScoreManager.Instance.photonView.
            RPC(nameof(ScoreManager.MinusScore), RpcTarget.All, actor.ActorNumber, missScore);
        }
        [PunRPC]
        public void DuringOverHeat()
        {
            Debug.Log("과열 발생");
            IsOverHeat = true;
            //TODO 김승태 : 과열에 따른 플레이어 기절 애니메이션 실행시키기
        }
        [PunRPC]
        public void AfterOverHeat()
        {
            Debug.Log("과열 종료");
            IsOverHeat = false;
            //TODO 김승태 : 과열에 따른 플레이어 기절 애니메이션 중지시키고 원래 IDLE 애니메이션으로 변경하기.

        }

        /// <summary>
        /// 과열 시 코루틴 실행. n초 뒤 과열 초기화 
        /// </summary>
        IEnumerator IE_OverHeating()
        {
            //과열 시
            photonView.RPC(nameof(DuringOverHeat), RpcTarget.All);
            PlayerStunAnim(overHeatingTime);

            yield return new WaitForSeconds(overHeatingTime);
            //과열 시간 종료 후 로직

            photonView.RPC(nameof(AfterOverHeat), RpcTarget.All);

            overHeatValue = 0; //과열점수 리셋
            Debug.Log($"과열 점수 초기화 {overHeatValue}");

            ScoreManager.Instance.photonView.RPC(
                nameof(ScoreManager.SetOverheat), RpcTarget.All, overHeatValue
                );
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
            Vector3 pos = p.position + p.forward * noteSpawnDist;
            //회전
            Quaternion rot = Quaternion.LookRotation(p.forward, Vector3.up);

            //위치, 회전
            return new Pose(pos, rot);
        }

        //플레이어 스턴
        public void PlayerStunAnim(float time)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            foreach (var player in PhotonNetwork.PlayerList)
            {
                photonView.RPC(nameof(RPC_Stun), RpcTarget.All, player.ActorNumber, time);
            }
        }

        [PunRPC]
        void RPC_Stun(int actorNum, float time)
        {
            if (!PlayerController.AvatarByActor.TryGetValue(actorNum, out var avatar)) return;

            if (avatar.TryGetComponent<PlayerAnimController>(out var anim))
            {
                Debug.Log("anim 있음");
                anim.PlayeStunAnim(time);
            }
            else
            {
                Debug.LogWarning($"actorNum {actorNum}의 아바타에서 PlayerAnimController를 찾지 못함");
            }
        }


    }
}