using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using DesignPattern;
using System.Collections;

namespace RhythmGame
{

    public class NoteSpawner : MonoBehaviourPunCallbacks
    {
        //오브젝트 풀 관련
        [SerializeField] private PooledObject[] _notePrefabs;
        private Dictionary<string, ObjectPool> _notePools = new();
        //스폰 관련
        [SerializeField] private float _spawnTiming = 2f;

        private bool _isSpawning = false;
        private Coroutine _spawnCoroutine;

        /// <summary>
        /// Note 스폰 시작하는 로직
        /// 마스터클라이언트가 담당하여 Note 스폰
        /// 스폰 중일 경우 return 된다.
        /// </summary>
        public void StartSpawn()
        {
            //스폰 중일 경우 리턴
            if (_isSpawning) return;

            _isSpawning = true;

            //Pool 셋팅
            InitPools();

            //마스터 클라이언트만 스폰할 수 있도록 설정
            if (PhotonNetwork.IsMasterClient)
                _spawnCoroutine = StartCoroutine(IE_Spawn());
        }

        /// <summary>
        /// 각 Note 프리펩에 해당하는 풀 생성
        /// </summary>
        private void InitPools()
        {
            foreach (var prefab in _notePrefabs)
            {
                if (!_notePools.ContainsKey(prefab.name))
                {
                    var pool = new ObjectPool(transform, prefab, 4);
                    _notePools.Add(prefab.name, pool);
                }
            }
        }

        /// <summary>
        /// 마스터 클라이언트가 Note 종류와 위치 결정 후,
        /// 모든 클라이언트에게 동기화 시키는 로직
        /// </summary>
        /// <returns> spawnTiming 마다 실행 </returns>
        private IEnumerator IE_Spawn()
        {
            while (true)
            {
                yield return new WaitForSeconds(_spawnTiming);

                // 마스터 클라이언트가 Note 종류와 위치 결정

                //랜덤으로 Note 프리펩 중 하나 선택
                int index = Random.Range(0, _notePrefabs.Length);
                string noteName = _notePrefabs[index].name;

                //TODO 김승태 : 레일 위치 설정 할 필요 있음
                // -> 게임 시작 시 필요 레일 수(플레이어 인원 수)를 받아온 후, 레일 수를 초기화 한 후,
                // 여기서 해당 레일 수에 맞게 위치 조정(spawnPos)하면 될 듯.
                
                //랜덤 이동 속도 설정
                float randomSpeed = Random.Range(1f, 4f);

                // 모든 클라이언트에게 동기화
                // photonView.RPC(nameof(NoteSpawn), RpcTarget.All, noteName, spawnPos, randomSpeed);
            }
        }

        /// <summary>
        /// Note 이름, 스폰 위치, 이동속도를 매개변수로 받은 후,
        /// 풀에서 꺼낸 후 해당 옵션 설정하는 로직
        /// </summary>
        /// <param name="noteName">Note의 이름</param>
        /// <param name="spawnPos">Note가 스폰될 위치</param>
        /// <param name="speed">Note의 낙하 속도</param>
        [PunRPC]
        private void NoteSpawn(string noteName, Vector3 spawnPos, float speed)
        {
            if (_notePools.TryGetValue(noteName, out var pool))
            {
                var note = pool.PopPool();
                note.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);
                if (note.TryGetComponent(out Note mover))
                    mover.SetSpeed(speed);
            }
        }

        /// <summary>
        /// 스폰을 멈추는 로직
        /// </summary>
        public void StopSpawn()
        {
            if (!_isSpawning) return;


            _isSpawning = false;

            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }
        }
    }
}