using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RhythmGame
{
    [RequireComponent(typeof(PlayerInput))]
    public class RythmPlayerInput : MonoBehaviour
    {
        //판정바
        public VerdictNote verdictNote;

        Note _holdtarget;
        float _holdTimer;
        float _requireHoldTime;
        bool _isPress;

        void Update()
        {
            if (!_isPress || _holdtarget == null) return;

            if (IsInVerdictBar(_holdtarget) && _holdtarget.Status == NoteStatus.CanInteract)
            {
                _holdTimer += Time.deltaTime;

                if (_holdTimer >= _requireHoldTime)
                {
                    ScoreManager.Instance.RequestHit(_holdtarget.NoteId, true, NoteType.Continue);
                    Debug.Log("7");
                    InitHold();
                }
            }
        }

        /// <summary>
        /// 탭/ 터치 : Fake, Touch 노트 전용
        /// </summary>
        public void OnTap()
        {
            if (!CanInput()) return;

            //초기화
            Note t = PickNote();

            //노트가 없는데도 클릭 시도

            if (t == null)
            {
                ScoreManager.Instance.RequestMiss();
                Debug.Log("1");
                return;
            }

            //노트가  continue일 경우 tap 시도 시 미스처리
            if (t.Type == NoteType.Continue)
            {
                ScoreManager.Instance.RequestMiss();
                Debug.Log("2");

                return;
            }

            //클릭 시 동작 (정확하게 note 누르면 마스터한테 요청),아니면 미스했다는 로직 호출
            ScoreManager.Instance.RequestHit(t.NoteId, t.Status == NoteStatus.CanInteract, t.Type);
            Debug.Log("5");

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
            if (_holdtarget != null) return;

            var t = PickNote();
            //미스처리
            if (t == null || t.Type != NoteType.Continue || t.Status != NoteStatus.CanInteract)
            {
                // ScoreManager.Instance.RequestMiss();
                Debug.Log("3");

                return;
            }

            //홀드 값 지정
            _holdtarget = t;
            _holdTimer = 0f;
            _requireHoldTime = _holdtarget.GetHoldTime();
            _isPress = true;

        }
        void EndHold()
        {
            if (_holdtarget == null)
            {
                _isPress = false;
                return;
            }

            bool success = _holdTimer >= _requireHoldTime && IsInVerdictBar(_holdtarget);
            if (success)
            {
                ScoreManager.Instance.RequestHit(_holdtarget.NoteId, true, NoteType.Continue);
                Debug.Log("6");
            }
            else
            {
                ScoreManager.Instance.RequestMiss();
                Debug.Log("4");

            }

            InitHold();

        }

        private void InitHold()
        {
            //홀드 값 초기화
            _holdtarget = null;
            _holdTimer = 0f;
            _requireHoldTime = 0f;
            _isPress = false;
        }

        bool IsInVerdictBar(Note note)
        {
            return verdictNote.Notes.Contains(note);
        }

        /// <summary>
        /// 노트 선택 로직
        /// </summary>
        /// <returns></returns>
        Note PickNote()
        {
            //판정 영역 안의 노트 중 내 레인만
            var list = verdictNote.Notes;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var note = list[i];
                if (note == null) continue;

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
            if (!GameManager.Instance || !GameManager.Instance.IsGameStart) return false;

            //스턴 시 입력 방지
            if (GameManager.Instance.IsOverHeat) return false;

            //UI 입력일 경우 아랫단 무시
            if (IsOnUI()) return false;

            // 레인이 없거나, 포톤쪽 연결 안됏으면 무시
            if (!LaneManager.Instance || !PhotonNetwork.IsConnected) return false;
            return true;
        }
    }

}