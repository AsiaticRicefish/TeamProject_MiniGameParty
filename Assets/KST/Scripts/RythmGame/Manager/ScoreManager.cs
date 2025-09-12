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

        void Start()
        {
            _combo = 0; _bestCombo = 0; _verdictScore = 0;
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
            bool isHit = false;

            // 라인 검증 (내 라인의 노트인지 판별하기)

            if (!LaneManager.Instance.LaneByNoteId.TryGetValue(noteId, out int noteLane)) return;
            if (!LaneManager.Instance.GetLane(info.Sender.ActorNumber, out int actorLane)) return;

            //내 레인이 아닐 경우에
            if (noteLane != actorLane)
            {
                //TODO 김승태 : 내 레인과 상대 레인에 노트가 동시에 도착하는 경우 과열처리가 날 수도 있음. 이걸 방지하는 코드가 필요함.
                //-> 과열 시스템이 현재 기획 상에서는 없어졌기에, 미스처리를 주석 처리하면 사실 상 문제 발생 x
                // //과열 점수가 오르도록
                // GameManager.Instance.MissBlock(info.Sender);
                return;
            }

            // 득점 및 과열 처리
            if (isCanInteract)
            {
                isHit = true;
                GameManager.Instance.GoodHitScore(type, info.Sender);

                // SoundManager.Instance.PlaySFX_GAME(SfX_Game.SFX_Rhythm_NoteDestory);
                SoundManager.Instance.PlaySFX_GAME(0);

            }
            else
            {
                isHit = false;
                // GameManager.Instance.OverHeatCheck();

                GameManager.Instance.MissBlock(info.Sender);
                // SoundManager.Instance.PlaySFX_GAME(SfX_Game.SFX_Rhythm_Miss);
            }

            // 파괴
            LaneManager.Instance.LaneByNoteId.Remove(noteId);
            NoteSpawner.Instance.DestoryNote(noteId, isHit);
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

            if (note.Type == NoteType.Fake) return ApplyVerdict(Verdict.Miss);

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
            switch (verdict)
            {
                case Verdict.Perfect:
                case Verdict.Good:
                    _combo++;
                    _bestCombo = Mathf.Max(_bestCombo, _combo);
                    break;
                case Verdict.Miss:
                    _combo = 0;
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

            _verdictScore = Mathf.Clamp
            (_verdictScore, verdictConfig.verdictScoreMin, verdictConfig.verdictScoreMax);

            Debug.Log($"판정 점수 : {_verdictScore}");
            Debug.Log($"콤보  : {_combo}");

            //이벤트 발행 -> _verdictScore는 추후 마지막 점수 집계시 합산되어야함.
            OnVerdict?.Invoke(verdict, _combo, _verdictScore);

            return verdict;
        }



        #endregion


    }
}