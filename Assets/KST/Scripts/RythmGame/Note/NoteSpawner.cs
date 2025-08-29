using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using DesignPattern;

namespace RhythmGame
{
    /// <summary>
    /// Note를 스폰하는 클래스로, 
    /// 마스터 클라이언트가 담당하여 실행하며, Note 생성 시 속도, 종류 등을 설정함.
    /// 
    /// 해당 클래스는 독립성이 보장되어야 하며, 추후 게임매니저 및 네트워크 매니저에서도 이용할 가능성이 있기에, 싱글톤으로 구현
    /// </summary>
    public class NoteSpawner : PunSingleton<NoteSpawner>
    {
        //오브젝트 풀 관련
        [SerializeField] PooledObject[] _notePrefabs; // 풀링 프리팹들(로컬용)
        Dictionary<string, ObjectPool> _notePools = new();
        Dictionary<int, PooledObject> _activeById = new();

        //스폰 관련
        [SerializeField] float _spawnTiming = 2f; // 노트 생성 간격 -> 추후 bpm에 맞춰서 변경하기
        bool _isSpawning;
        Coroutine _spawnCo;
        int _seqId = 0; // 마스터가 증가시키는 전역 노트 ID 시퀀스

        public void StartSpawn()
        {
            if (_isSpawning) return;

            _isSpawning = true;

            InitPools();

            if (PhotonNetwork.IsMasterClient)
                _spawnCo = StartCoroutine(IE_Spawn());
        }

        [PunRPC]
        public void RPC_StartSpawn() => StartSpawn(); // 스폰 시작


        /// <summary>
        /// 풀 초기화
        /// 
        /// 각 노트 별 최소 프리펩 수 지정
        /// </summary>
        private void InitPools()
        {
            foreach (var prefab in _notePrefabs)
                if (!_notePools.ContainsKey(prefab.name))
                    _notePools.Add(prefab.name, new ObjectPool(transform, prefab, 5));
        }

        private IEnumerator IE_Spawn()
        {
            while (true)
            {
                yield return new WaitForSeconds(_spawnTiming);

                //레인 활성화 수(실제 씬에 활성화 한 레인 수 ~ 실제 접속중인 플레이어 수 중 최소 값)
                int activeLaneCount = Mathf.Min(LaneManager.Instance.ActiveLaneCount, GameManager.Instance.LaneCapacity
            );
                if (activeLaneCount <= 0)
                {
                    Debug.Log("액티브 라인 카운트가 0이하임");
                    continue; //플레이어 없으면 whilte 루프 다시 돌기
                }

                //노트 랜덤 선택
                string noteName = _notePrefabs[Random.Range(0, _notePrefabs.Length)].name;
                // 레인 중 1개 랜덤 선택
                int lane = Random.Range(1, activeLaneCount + 1);
                //속도 랜덤 선택
                float speed = Random.Range(1.5f, 4.0f);

                // 마스터가 각 noteId 생성 & 등록
                int noteId = ++_seqId;
                LaneManager.Instance.RegisterNote(noteId, lane);

                // 모든 클라에 로컬 스폰 명령
                photonView.RPC(nameof(RPC_NoteSpawn), RpcTarget.All, noteName, lane, speed, noteId);
            }
        }

        [PunRPC]
        private void RPC_NoteSpawn(string noteName, int lane, float speed, int noteId)
        {
            if (!_notePools.TryGetValue(noteName, out var pool)) return;

            var pose = GameManager.Instance.GetLaneSpawnPose(lane);

            //노트 풀에서 꺼내고 위치, 회전 설정
            var note = pool.PopPool();
            note.transform.SetPositionAndRotation(pose.position, pose.rotation);

            //  Note 속도, Id, 레인 설정
            if (note.TryGetComponent(out Note mover))
            {
                mover.SetSpeed(speed);
                mover.SetMoveDirection(pose.rotation * Vector3.forward);
                mover.Init(noteId, lane);
            }

            _activeById[noteId] = note; // 로컬에서 활성화된 노트 추적하기 위해 등록
        }

        [PunRPC]
        private void RPC_DestroyNote(int noteId)
        {
            // noteId에 해당하는 로컬 인스턴스만 반납
            if (_activeById.TryGetValue(noteId, out var inst))
            {
                _activeById.Remove(noteId);
                inst.ReturnPool();
            }
        }

        //스폰 정지
        public void StopSpawn()
        {
            if (!_isSpawning) return;

            _isSpawning = false;
            if (_spawnCo != null)
            {
                StopCoroutine(_spawnCo);
                _spawnCo = null;
            }
        }

        // 마스터가 검증 후 파괴 브로드캐스트할 때 씀
        public void DestoryNote(int noteId)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_DestroyNote), RpcTarget.All, noteId);
        }
    }
}
