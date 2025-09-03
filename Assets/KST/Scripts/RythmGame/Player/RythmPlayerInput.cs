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

        public void OnTap()
        {
            //게임 시작 전 입력 방지
            //스턴 시 입력 방지

            //UI 입력일 경우 아랫단 무시
            if (IsOnUI()) return;

            // 레인이 없거나, 포톤쪽 연결 안됏으면 무시
            if (!LaneManager.Instance || !PhotonNetwork.IsConnected) return;

            //초기화
            Note t = null;
            //판정 영역 안의 노트 중 내 레인만
            var list = verdictNote.Notes;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var note = list[i];
                if (note == null) continue;

                t = note;

                break;
            }

            //클릭 시 동작 (정확하게 note 누르면 마스터한테 요청),아니면 미스했다는 로직 호출
            if (t != null)
            {
                // LaneManager.Instance.RequestHit(t.NoteId, t.Status == NoteStatus.CanInteract, t.Type);
                ScoreManager.Instance.RequestHit(t.NoteId, t.Status == NoteStatus.CanInteract, t.Type);
            }
            //노트가 없는데도 클릭 시도
            else
            {
                //TODO 김승태: 개인점수도 깎이도록 추가 코드 필요
                // LaneManager.Instance.RequestMiss();
                ScoreManager.Instance.RequestMiss();
            }
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
    }

}