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
    public class GameManager : PunSingleton<GameManager>,IGameComponent
    {
        // 게임 시간 관리
        [SerializeField] float gameTime = 180f; //게임 플레이타임
        [SerializeField] float countDown = 3f; //카운트 다운
        [SerializeField] float delayTime = 3f; //잔여 노트들이 남아있는 시간(임시)
        //TODO 김승태 게임 플레이 시간 (임시) 변경 예정
        [SerializeField] TMP_Text gameTimer;
        public bool IsGameStart = false;
        public bool IsGameOver = false;
        public event Action OnGameStart; //게임 시작 이벤트
        public event Action OnGameOver; //게임 오버 여부에 따른 이벤트
        public event Action<double, double> OnTimer;
        Coroutine _waitStartCo;
        Coroutine _waitEndCo;

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
        

        //TODO 김승태 : IGameComponent 인터페이스 구현

        public void Initialize()
        {
        }

        #region 게임 시작 종료 로직
        /// <summary>
        /// 마스터가 전담하여 게임 로직 실행
        /// 라인 배정 → 스폰 → 타이머 시작
        /// </summary>
        public void StartGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 플레이어 자리 배정
            LaneManager.Instance.SetLane();

            // 스폰 시작
            // NoteSpawner.Instance.photonView.RPC(nameof(NoteSpawner.RPC_StartSpawn), RpcTarget.All);

            double startTime = PhotonNetwork.Time + countDown;
            double endTime = startTime + gameTime;

            NoteSpawner.Instance.photonView.RPC(nameof(NoteSpawner.RPC_InitStart), RpcTarget.All, startTime);

            //게임 설정관련
            photonView.RPC(nameof(PRC_StartGameTIme), RpcTarget.All, startTime, endTime);
            // photonView.RPC(nameof(GameStartSettings), RpcTarget.All);
        }

        [PunRPC]
        void PRC_StartGameTIme(double startTime, double endTime)
        {
            OnTimer?.Invoke(startTime, endTime);

            if (_waitStartCo != null) StopCoroutine(_waitStartCo);
            if (_waitEndCo != null) StopCoroutine(_waitEndCo);

            _waitStartCo = StartCoroutine(IE_WaitStart(startTime, endTime));
        }

        IEnumerator IE_WaitStart(double startTime, double endTime)
        {
            while (PhotonNetwork.Time < startTime) yield return null;

            photonView.RPC(nameof(GameStartSettings), RpcTarget.All);

            if (PhotonNetwork.IsMasterClient)
            {
                NoteSpawner.Instance.photonView.RPC(nameof(NoteSpawner.RPC_InitStart), RpcTarget.All, startTime);

                NoteSpawner.Instance.photonView.RPC(nameof(NoteSpawner.RPC_StartSpawn), RpcTarget.All);
            }
            _waitEndCo = StartCoroutine(IE_WaitEnd(endTime));
        }

        IEnumerator IE_WaitEnd(double endTime)
        {
            while (PhotonNetwork.Time < endTime) yield return null;

            EndGame();
        }


        [PunRPC]
        public void GameStartSettings()
        {
            if (IsGameStart) return;
            //게임 시작 플래그 설정
            IsGameStart = true;

            //리듬게임 랜덤 브금 시작
            var index = SoundManager.Instance.RandomSelectBGM();
            SoundManager.Instance.PlayBGM(index);
            OnGameStart?.Invoke();
        }

        /// <summary>
        /// 게임 종료 시
        /// </summary>
        [PunRPC]
        public void RPC_EndGame()
        {
            if (!IsGameStart) return;

            IsGameStart = false;
            IsGameOver = true;

            NoteSpawner.Instance.StopSpawn();
            SoundManager.Instance.StopBGM();

            //게임 종료 이벤트 호출
            OnGameOver?.Invoke();
            Debug.Log("게임 오버");
        }

        public void EndGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!IsGameStart || IsGameOver) return;

            photonView.RPC(nameof(RPC_EndGame), RpcTarget.All);
        }
        #endregion


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
                    score = 4;
                    // OverHeatCheck();
                    break;

                case NoteType.Touch:
                    score = 1;
                    // FrozenHeat();
                    break;

                case NoteType.Continue:
                    score = 2;
                    // FrozenHeat();
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
        }
        [PunRPC]
        public void AfterOverHeat()
        {
            Debug.Log("과열 종료");
            IsOverHeat = false;
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