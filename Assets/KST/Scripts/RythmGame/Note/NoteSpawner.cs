using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using DesignPattern;
using System.Collections;

namespace RhythmGame
{

    public class NoteSpawner : MonoBehaviourPunCallbacks
    {
        //������Ʈ Ǯ ����
        [SerializeField] private PooledObject[] _notePrefabs;
        private Dictionary<string, ObjectPool> _notePools = new();
        //���� ����
        [SerializeField] private float _spawnTiming = 2f;

        private bool _isSpawning = false;
        private Coroutine _spawnCoroutine;

        /// <summary>
        /// Note ���� �����ϴ� ����
        /// ������Ŭ���̾�Ʈ�� ����Ͽ� Note ����
        /// ���� ���� ��� return �ȴ�.
        /// </summary>
        public void StartSpawn()
        {
            //���� ���� ��� ����
            if (_isSpawning) return;

            _isSpawning = true;

            //Pool ����
            InitPools();

            //������ Ŭ���̾�Ʈ�� ������ �� �ֵ��� ����
            if (PhotonNetwork.IsMasterClient)
                _spawnCoroutine = StartCoroutine(IE_Spawn());
        }

        /// <summary>
        /// �� Note �����鿡 �ش��ϴ� Ǯ ����
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
        /// ������ Ŭ���̾�Ʈ�� Note ������ ��ġ ���� ��,
        /// ��� Ŭ���̾�Ʈ���� ����ȭ ��Ű�� ����
        /// </summary>
        /// <returns> spawnTiming ���� ���� </returns>
        private IEnumerator IE_Spawn()
        {
            while (true)
            {
                yield return new WaitForSeconds(_spawnTiming);

                // ������ Ŭ���̾�Ʈ�� Note ������ ��ġ ����

                //�������� Note ������ �� �ϳ� ����
                int index = Random.Range(0, _notePrefabs.Length);
                string noteName = _notePrefabs[index].name;

                //TODO ����� : ���� ��ġ ���� �� �ʿ� ����
                // -> ���� ���� �� �ʿ� ���� ��(�÷��̾� �ο� ��)�� �޾ƿ� ��, ���� ���� �ʱ�ȭ �� ��,
                // ���⼭ �ش� ���� ���� �°� ��ġ ����(spawnPos)�ϸ� �� ��.
                
                //���� �̵� �ӵ� ����
                float randomSpeed = Random.Range(1f, 4f);

                // ��� Ŭ���̾�Ʈ���� ����ȭ
                // photonView.RPC(nameof(NoteSpawn), RpcTarget.All, noteName, spawnPos, randomSpeed);
            }
        }

        /// <summary>
        /// Note �̸�, ���� ��ġ, �̵��ӵ��� �Ű������� ���� ��,
        /// Ǯ���� ���� �� �ش� �ɼ� �����ϴ� ����
        /// </summary>
        /// <param name="noteName">Note�� �̸�</param>
        /// <param name="spawnPos">Note�� ������ ��ġ</param>
        /// <param name="speed">Note�� ���� �ӵ�</param>
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
        /// ������ ���ߴ� ����
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