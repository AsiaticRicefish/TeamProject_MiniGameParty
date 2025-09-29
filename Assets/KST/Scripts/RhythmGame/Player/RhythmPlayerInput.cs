using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RhythmGame
{
    [RequireComponent(typeof(PlayerInput))]
    public class RhythmPlayerInput : MonoBehaviour
    {
        //판정바
        public VerdictNote verdictNote;

        Note _holdTarget;
        float _holdTimer;
        float _requireHoldTime;
        bool _isPress;
        bool _isDone; //필요시간 도달로 성공 처리 시 cancel에서 중복처리 방지

        List<Note> _noteToTap = new(); // performed 시점 노트(지속 아니면 cancel에서 판정)

        void Update()
        {
            if (!_isPress || _holdTarget == null) return;

            if (IsInVerdictBar(_holdTarget) && _holdTarget.Status == NoteStatus.CanInteract)
            {
                _holdTimer += Time.deltaTime;

                if (_holdTimer >= _requireHoldTime)
                {
                    ScoreManager.Instance.VerdictHold(_holdTimer, _requireHoldTime);
                    NoteSpawner.Instance.ClientLocalHit(_holdTarget.NoteId);
                    ScoreManager.Instance.RequestHit(_holdTarget.NoteId, true, NoteType.Continue);
                    _isDone = true;
                    InitHold();
                }
            }
            else
            {
                ScoreManager.Instance.RequestMiss(NoteType.Continue);
                _isDone = true;
                InitHold();
            }
        }

        /// <summary>
        /// 홀드 : Continue 노트 전용
        /// </summary>
        /// <param name="callback"></param>
        public void OnPress(InputAction.CallbackContext callback)
        {
            //홀드 시작
            if (callback.performed)
            {
                BeginHold();
            }
            //홀드 종료
            else if (callback.canceled)
            {
                EndHold();
            }
        }

        void BeginHold()
        {
            if (!CanInput()) return;
            if (_holdTarget != null) return;

            _noteToTap.Clear();

            var t = PickNote();
            //미스처리
            if (t == null)
            {
                // _noteToTap = null;
                // ScoreManager.Instance.RequestMiss();
                return;
            }

            //지속 노트일 경우
            if (t.Type == NoteType.Continue && t.Status == NoteStatus.CanInteract)
            {
                //홀드 값 지정
                _holdTarget = t;
                _holdTimer = 0f;
                _requireHoldTime = _holdTarget.GetHoldTime();
                Debug.Log($"홀드 지속 시간 : {_requireHoldTime}");
                _isPress = true;
                _isDone = false;
                // _noteToTap = null;
            }
            else
            {
                // _noteToTap = t;
                _noteToTap.Add(t);
            }

        }
        void EndHold()
        {
            if (!CanInput()) return;
            if (_isDone)
            {
                _isDone = false;
                return;
            }
            //홀드 중일 때
            if (_holdTarget != null)
            {

                ScoreManager.Instance.VerdictHold(_holdTimer, _requireHoldTime);

                bool success = _holdTimer >= _requireHoldTime && IsInVerdictBar(_holdTarget);
                if (success)
                {
                    NoteSpawner.Instance.ClientLocalHit(_holdTarget.NoteId);
                    ScoreManager.Instance.RequestHit(_holdTarget.NoteId, true, NoteType.Continue);
                }
                else
                    ScoreManager.Instance.RequestMiss(NoteType.Continue);

                InitHold();
                _noteToTap.Clear();
                return;
            }
            //탭 처리
            // if (_noteToTap != null)
            if (_noteToTap.Count > 0)
            {
                bool anyHit = false;
                NoteType? missType = null;
                foreach (var t in _noteToTap)
                {

                    // t = _noteToTap;
                    // _noteToTap = null;
                    if (t == null) continue;

                    if (t.Type == NoteType.Continue) continue;

                    if (missType == null)
                        missType = t.Type;

                    bool isCan = t.Status == NoteStatus.CanInteract && IsInVerdictBar(t);

                    if (isCan)
                    {
                        ScoreManager.Instance.VerdictTouch(t, verdictNote.transform);
                        NoteSpawner.Instance.ClientLocalHit(t.NoteId);
                        ScoreManager.Instance.RequestHit(t.NoteId, true, t.Type);
                        anyHit = true;
                    }
                }
                if (!anyHit && missType.HasValue)
                {
                    ScoreManager.Instance.RequestMiss(missType.Value);
                    ScoreManager.Instance.VerdictMiss(missType.Value);
                }
                _noteToTap.Clear();
            }
            else
            {
                ScoreManager.Instance.RequestMiss(NoteType.Touch);
                ScoreManager.Instance.VerdictMiss(NoteType.Touch);
            }

        }

        private void InitHold()
        {
            //홀드 값 초기화
            _holdTarget = null;
            _holdTimer = 0f;
            _requireHoldTime = 0f;
            _isPress = false;
        }

        bool IsInVerdictBar(Note note)
        {
            var list = verdictNote.Notes;
            return list!=null && list.Contains(note);
        }

        bool TryGetMyLane(out int lane)
        {
            lane = -1;
            if (!LaneManager.Instance || !PhotonNetwork.IsConnected) return false;
            return LaneManager.Instance.GetLane(PhotonNetwork.LocalPlayer.ActorNumber, out lane);
        }

        /// <summary>
        /// 노트 선택 로직
        /// </summary>
        /// <returns></returns>
        Note PickNote()
        {
            //판정 영역 안의 노트 중 내 레인만
            if (!TryGetMyLane(out int myLane)) return null;

            var list = verdictNote.Notes;
            if (list == null || list.Count == 0) return null;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var note = list[i];
                if (note == null) continue;
                if (note.Lane != myLane) continue;

                return note;
            }
            return null;
        }


        //UI 입력 관련 로직
        /// <summary>
        /// 해당 좌표에 UI 여부 확인
        /// </summary>
        bool IsOnUI()
        {
            if (!EventSystem.current) return false;

            //현재 좌표 얻기
            Vector2 pos = Pointer.current != null ? Pointer.current.position.ReadValue() :
            Touchscreen.current != null ? Touchscreen.current.primaryTouch.position.ReadValue() :
            Vector2.zero;

            var eventData = new PointerEventData(EventSystem.current) { position = pos };

            //해당 좌표로 ui 레이캐스트 수행 결과
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            //UI가 한 개 이상 검출 되면 true 반환
            return results.Count > 0;
        }

        /// <summary>
        /// 입력 방지 여부
        /// </summary>
        /// <returns></returns>
        bool CanInput()
        {
            //게임 시작 전 입력 방지
            if (!GameManager.Instance || !GameManager.Instance.IsGameStart || GameManager.Instance.IsGameOver) return false;

            //스턴 시 입력 방지
            // if (GameManager.Instance.IsOverHeat) return false;

            //UI 입력일 경우 아랫단 무시
            if (IsOnUI()) return false;

            // 레인이 없거나, 포톤쪽 연결 안됏으면 무시
            if (!LaneManager.Instance || !PhotonNetwork.IsConnected) return false;
            return true;
        }
    }

}