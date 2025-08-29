using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace RhythmGame
{
    public class LaneManager : PunSingleton<LaneManager>
    {
        private Dictionary<int, int> _laneByActor = new(); // 액터넘버와 레인 번호 매핑
        private Dictionary<int, int> _laneByNoteId = new(); //noteID와 Lane 번호 매핑

        public int ActiveLaneCount => _laneByActor.Count; //현재 배정 된 Lane 수(플레이어 수)

        // 마스터가 Lane 배정 후 모든 클라에게 알려줌
        [PunRPC]
        void RPC_SetLane(int actorNumber, int lane)
        {
            GameManager.Instance.PlaceActorToLane(actorNumber, lane);
        }
        
        //lane 배정 호출
        public void SetLane()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            //초기화
            _laneByActor.Clear();

            // 현재 룸 플레이어 목록에서 액터넘버 오름차순 정렬
            var ordered = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToArray();

            //플레이어 수 만큼 등록
            for (int i = 0; i < ordered.Length; i++)
            {
                int lane = i + 1; //Lane은 1번부터 시작
                int actor = ordered[i].ActorNumber;
                _laneByActor[actor] = lane;

                // 모든 클라에 배정된 정보 브로드캐스팅
                photonView.RPC(nameof(RPC_SetLane), RpcTarget.All, actor, lane);
            }
        }

        // 스폰 시 해당 노트가 어느 lane인지 등록
        public void RegisterNote(int noteId, int lane)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _laneByNoteId[noteId] = lane;
        }

        // 클라 → 마스터: 히트 요청(판정 포함)
        public void RequestHit(int noteId, bool isGood)
        {
            photonView.RPC(nameof(RPC_RequestHit), RpcTarget.MasterClient, noteId, isGood);
        }

        [PunRPC]
        void RPC_RequestHit(int noteId, bool isGood, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 라인 검증 (내 라인의 노트인지 판별하기)
            if (!_laneByNoteId.TryGetValue(noteId, out int noteLane)) return;
            if (!_laneByActor.TryGetValue(info.Sender.ActorNumber, out int actorLane)) return;
            if (noteLane != actorLane) return;

            // 득점 및 과열 처리
            if (isGood)
                GameManager.Instance.GoodHitScore(info.Sender);
            else
                GameManager.Instance.OverHeatCheck();

            // 파괴
            _laneByNoteId.Remove(noteId);
            NoteSpawner.Instance.DestoryNote(noteId);
        }


        //룸 입장 시
        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (PhotonNetwork.IsMasterClient) SetLane();
        }

        //룸 퇴장 시
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (PhotonNetwork.IsMasterClient) SetLane();
        }

        //마스터 변경 시
        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            //레인 재배정
            if (PhotonNetwork.LocalPlayer == newMasterClient) SetLane();
        }
    }
}
