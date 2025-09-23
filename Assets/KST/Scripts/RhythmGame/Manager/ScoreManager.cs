using System;
using DesignPattern;
using Photon.Pun;
using UnityEngine;

namespace RhythmGame
{
    public class ScoreManager : PunSingleton<ScoreManager>, IGameComponent
    {
        //점수
        int _score; //개인 별 점수
        int _heatScore; //과열 점수
        public int Score => _score;
        public int HeatScore => _heatScore;

        //판정 및 콤보
        private int _combo;
        private int _bestCombo;
        private int _verdictScore;

        public int Combo => _combo;
        public int BestCombo => _bestCombo;
        public int VerdictScore => _verdictScore;
        public VerdictConfig verdictConfig = new();


        //이벤트
        public event Action<int> OnScoreChanged;
        public event Action<int> OnOverHeatScoreChanaged;
        public event Action OnHeatScoreOver;
        public event Action<Verdict, int, int> OnVerdict;

        //TODO 김승태 : IGameComponent 인터페이스 구현

        public void Initialize()
        {
        }

        void Start()
        {
            _combo = 0; _bestCombo = 0; _score = 0; _verdictScore = 0;
        }

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
            SendScore();
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
            SendScore();
        }

        void SendScore()
        {
            if (!PhotonNetwork.IsConnected) return;

            var uid = PhotonNetwork.LocalPlayer.CustomProperties?["uid"] as string;
            if (string.IsNullOrEmpty(uid)) return;

            GameManager.Instance.photonView.RPC(nameof(GameManager.RPC_ReceiveScore), RpcTarget.MasterClient, uid, _score, _verdictScore);
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
            if (noteLane != actorLane) return;
            if (!LaneManager.Instance.LaneByNoteId.Remove(noteId)) return;

            // 파괴
            // LaneManager.Instance.LaneByNoteId.Remove(noteId);
            NoteSpawner.Instance.DestroyNote(noteId, isCanInteract);

            // 득점 및 과열 처리
            if (isCanInteract)
            {
                GameManager.Instance.GoodHitScore(type, info.Sender);
                if (SoundManager.Instance != null)
                    SoundManager.Instance.PlaySFX
                    (type == NoteType.Continue ? "Continue" : "Touch");

                return;
                
            }
            GameManager.Instance.MissBlock(info.Sender);
            // SoundManager.Instance.PlaySFX_GAME(SfX_Game.SFX_Rhythm_Miss);
        }

        public void RequestMiss()
        {
            photonView.RPC(nameof(RPC_RequestMiss), RpcTarget.MasterClient);
        }

        [PunRPC]
        void RPC_RequestMiss(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            // GameManager.Instance.OverHeatCheck();
            GameManager.Instance.MissBlock(info.Sender);
        }

        /// <summary>
        /// 맞춰야하는 노트 못 맞췄을 때
        /// </summary>
        /// <param name="noteId">해당 noteID</param>
        /// <param name="actorNum">플레이어 액터넘버</param>
        [PunRPC]
        void RPC_RequesetLaneMiss(int noteId, int actorNum)
        {
            if (!LaneManager.Instance.LaneByNoteId.TryGetValue(noteId, out int noteLane)) return;
            //액터 확인
            if (!LaneManager.Instance.GetLane(actorNum, out int actorLane)) return;
            //해당 액터의 레인과 노트 레인 일치 확인
            if (noteLane != actorLane) return;

            MissToAll(actorNum);

            //노트 파괴()
            NoteSpawner.Instance.DestroyNote(noteId, false);
        }

        public void RequestLaneMiss(int noteId)
        {
            photonView.RPC(nameof(RPC_RequesetLaneMiss), RpcTarget.MasterClient, noteId, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        /// <summary>
        /// 모두에게 해당 노트가 miss 됐다는 것을 전파
        /// </summary>
        [PunRPC]
        void RPC_MissToAll(int actorNum)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber == actorNum)
            {
                VerdictMiss();
            }
        }

        void MissToAll(int actorNum)
        {
            photonView.RPC(nameof(RPC_MissToAll), RpcTarget.All, actorNum);
        }

        /// <summary>
        /// Fake 노트일 때는 노트 파괴 요청
        /// </summary>
        /// <param name="noteId"></param>
        [PunRPC]
        void RPC_RequestMissFake(int noteId)
        {
            if (!NoteSpawner.Instance.TryGetNote(noteId, out var note)) return;

            //해당 노트가 fake가 아닐경우 미스처리로 넘기기
            if (note.Type != NoteType.Fake)
                RequestLaneMiss(note.NoteId);

            NoteSpawner.Instance.DestroyNote(noteId, false);
        }

        public void RequestMissFake(int noteId)
        {
            photonView.RPC(nameof(RPC_RequestMissFake), RpcTarget.MasterClient, noteId);
        }

        #endregion

        #region Perfect 판정 및 콤보 시스템

        /// <summary>
        /// 터치 판정시스템
        /// </summary>
        /// <param name="note"></param>
        /// <param name="verdictPos"></param>
        /// <returns></returns>
        public Verdict VerdictTouch(Note note, Transform verdictPos)
        {
            if (!note || !verdictPos) return ApplyVerdict(Verdict.Miss);

            if (note.Type == NoteType.Fake) return ApplyVerdict(Verdict.Bad);

            Vector3 dist = note.transform.position - verdictPos.position;
            float z = Mathf.Abs(Vector3.Dot(dist, Vector3.forward));

            if (z <= verdictConfig.touchPerfect) return ApplyVerdict(Verdict.Perfect);
            else return ApplyVerdict(Verdict.Good);
        }

        /// <summary>
        /// 홀드 판정 시스템
        /// </summary>
        /// <param name="hold"></param>
        /// <param name="perfectTime"></param>
        /// <returns></returns>
        public Verdict VerdictHold(float hold, float perfectTime)
        {
            if (perfectTime <= 0f) return ApplyVerdict(Verdict.Miss);
            float requieTime = perfectTime * verdictConfig.holdGood;

            if (hold >= perfectTime) return ApplyVerdict(Verdict.Perfect);
            if (hold >= requieTime) return ApplyVerdict(Verdict.Good);
            else return ApplyVerdict(Verdict.Miss);
        }

        /// <summary>
        /// 미스 처리
        /// </summary>
        /// <returns></returns>
        public Verdict VerdictMiss()
        {
            return ApplyVerdict(Verdict.Miss);
        }


        /// <summary>
        /// 판정 적용 로직
        /// </summary>
        /// <param name="verdict"></param>
        /// <returns></returns>
        Verdict ApplyVerdict(Verdict verdict)
        {
            int delta = 0;
            switch (verdict)
            {
                case Verdict.Perfect:
                case Verdict.Good:
                    _combo++;
                    _bestCombo = Mathf.Max(_bestCombo, _combo);
                    delta = +1;
                    //TODO 김승태:콤보 유지시 플레이어 캐릭터에 이펙트 유지
                    break;
                case Verdict.Bad:
                case Verdict.Miss:
                    _combo = 0;
                    delta = -1;
                    //TODO 김승태: 콤보 실패시 플레이어 캐릭터 이펙트 해지

                    break;
            }

            if (verdict == Verdict.Perfect)
            {
                _verdictScore++;
                Debug.Log("퍼펙트");
            }
            else if (verdict == Verdict.Good)
            {
                _verdictScore++;
                Debug.Log("굿");
            }
            else if (verdict == Verdict.Miss)
            {
                _verdictScore--;
                Debug.Log("미스");
            }
            else //Bad
            {
                _verdictScore--;
                Debug.Log("베드");
            }

            _verdictScore = Mathf.Clamp
            (_verdictScore, verdictConfig.verdictScoreMin, verdictConfig.verdictScoreMax);

            Debug.Log($"판정 점수 : {_verdictScore}");
            Debug.Log($"콤보  : {_combo}");

            //이벤트 발행 -> _verdictScore는 추후 마지막 점수 집계시 합산되어야함.
            OnVerdict?.Invoke(verdict, _combo, _verdictScore);

            SendScore();

            photonView.RPC(nameof(RPC_VerdictDelta), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber, delta);

            return verdict;
        }

        [PunRPC]
        void RPC_VerdictDelta(int actorNumber, int delta)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (NoteSpawner.Instance)
                NoteSpawner.Instance.VerdictDelta(actorNumber, delta);
        }

        #endregion
    }
}