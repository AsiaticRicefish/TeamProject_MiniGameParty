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
    public class NoteSpawner : PunSingleton<NoteSpawner>, IGameComponent
    {
        //오브젝트 풀 관련
        [SerializeField] PooledObject[] _notePrefabs; // 노트 풀링 프리팹들(로컬용)
        [SerializeField] PooledObject[] _hitEffect; //적중 시 파티클
        // [SerializeField] PooledObject _hitEffect; //적중 시 파티클
        private ObjectPool[] _effectPool; //이펙트 풀
        Dictionary<string, ObjectPool> _notePools = new();
        Dictionary<int, PooledObject> _activeById = new();

        //프리펩 관련
        string touchName = "TouchNote";
        string continueName = "ContinueNote";
        string fakeName = "FakeNote";
        Dictionary<NoteType, string> _typeToPrefab = new();

        //스폰 관련
        bool _isSpawning;
        int _seqId = 0; // 마스터가 증가시키는 전역 노트 ID 시퀀스

        bool _isInit;
        double _gameStartTime;
        Dictionary<int, List<Coroutine>> _laneLists = new();

        //스폰 사이클 관련 상수
        [SerializeField] float speed = 4f;
        int basePoint = 30;
        int minPoint = 15;
        int maxPoint = 45;
        Dictionary<int, int> _verdictSumByActor = new();

        //BPM 관련

        [SerializeField] float _songBpm = 160f;  // 곡 BPM
        [SerializeField] double _songOffsetSec = 0.0; // 시작 보정
        [SerializeField] AudioSource _songSource;

        //TODO 김승태 : IGameComponent 인터페이스 구현
        public void Initialize()
        {
        }

        protected override void Awake()
        {
            _typeToPrefab[NoteType.Touch] = touchName;
            _typeToPrefab[NoteType.Continue] = continueName;
            _typeToPrefab[NoteType.Fake] = fakeName;
        }

        public void StartSpawn()
        {
            if (_isSpawning) return;
            if (!_isInit) return;

            _isSpawning = true;

            InitPools();

            if (!PhotonNetwork.IsMasterClient) return;

            _laneLists.Clear();
            StartCoroutine(IE_SpawnScheduler());
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

            _effectPool = new ObjectPool[_hitEffect.Length];
            for (int i = 0; i < _hitEffect.Length; i++)
            {
                _effectPool[i] = new(null, _hitEffect[i], 5);
            }
        }

        public PooledObject GetEffectPool(NoteType type)
        {
            int index = type == NoteType.Continue ? 0 : 1;

            return _effectPool[index].PopPool();
        }

        [PunRPC]
        public void RPC_InitStart(double startTime)
        {
            _gameStartTime = startTime;
            _isInit = true;

            // if (_songSource != null)
            // {
            //     // 모든 클라에서 같은 시각에 재생되도록
            //     double dspNow = AudioSettings.dspTime;
            //     double delay = Mathf.Max(0.05f, (float)(_gameStartTime - PhotonNetwork.Time)); // 50ms 이상 여유
            //     _songSource.PlayScheduled(dspNow + delay);
            // }
        }

        IEnumerator IE_SpawnScheduler()
        {
            while (PhotonNetwork.Time < _gameStartTime) yield return null;

            Preload();

            yield return StartCoroutine(IE_CycleLoop());
        }
        /// <summary>
        /// 0~30초 간 사이클
        /// </summary>
        void Preload()
        {
            double start = PhotonNetwork.Time + 0.1;
            int activeLaneCount = Mathf.Min(LaneManager.Instance.ActiveLaneCount, GameManager.Instance.LaneCapacity);

            for (int lane = 1; lane <= activeLaneCount; lane++)
            {
                //  정확히 15개(터치10 + 지속5)
                var types = new List<NoteType>(15);
                for (int i = 0; i < 10; i++) types.Add(NoteType.Touch);
                for (int i = 0; i < 5; i++) types.Add(NoteType.Continue);
                Utils.Shuffle(types); // "랜덤하게" 조건 충족

                double windowStart = _gameStartTime;
                double windowEnd = _gameStartTime + 30.0; // 30초
                Schedule(windowStart, windowEnd, lane, types); //15개만 비트에 분배
            }
        }

        IEnumerator IE_CycleLoop()
        {
            double endTime = _gameStartTime + 180.0;  // 180초까지
            double cycle = 30.0;

            int time = Mathf.FloorToInt((float)((PhotonNetwork.Time - _gameStartTime) / cycle));
            double nextCycleStart = _gameStartTime + (time + 1) * cycle;

            while (PhotonNetwork.Time < endTime)
            {
                while (PhotonNetwork.Time < nextCycleStart) yield return null;

                int lanes = Mathf.Min(LaneManager.Instance.ActiveLaneCount, GameManager.Instance.LaneCapacity);
                for (int lane = 1; lane <= lanes; lane++)
                {
                    int actorNum = -1;
                    LaneManager.Instance.GetActor(lane, out actorNum);

                    int bonus = (actorNum > 0) ? GetVerdictBonus(actorNum) : 0; // [-10, +15]
                    int budget = Mathf.Clamp(basePoint + bonus, minPoint, maxPoint); // 15~45

                    var types = TypesByBudget(budget);   // budget≥35면 Fake 1~4 포함
                    Utils.Shuffle(types);

                    double start = nextCycleStart;
                    double end = nextCycleStart + cycle;
                    Schedule(start, end, lane, types);  // 정확히 budget만큼 분배
                }

                nextCycleStart += cycle;
                yield return null;
            }
        }

        //  types.Count개를 균등 샘플링해서 스폰
        void Schedule(double start, double end, int lane, List<NoteType> types)
        {
            if (types == null || types.Count == 0) return;

            GetBeatRange(start, end, out int b0, out int b1);
            if (b1 < b0) return;

            int beatCount = b1 - b0 + 1;
            int N = Mathf.Min(types.Count, beatCount);

            var chosenBeats = new List<int>(N);
            double step = (double)beatCount / N; // 60비트에서 15개면 4비트마다 1개
            double acc = 0;
            for (int i = 0; i < N; i++)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt((float)acc), 0, beatCount - 1);
                chosenBeats.Add(b0 + idx);
                acc += step;
            }

            if (!_laneLists.ContainsKey(lane)) _laneLists[lane] = new List<Coroutine>();

            for (int i = 0; i < N; i++)
            {
                double due = BeatTimeNetwork(chosenBeats[i]);
                var co = StartCoroutine(IE_WaitAndSpawn(due, lane, types[i]));
                _laneLists[lane].Add(co);
            }
        }

        // 절대시간까지 기다렸다가 바로 스폰
        IEnumerator IE_WaitAndSpawn(double dueNetworkTime, int lane, NoteType type)
        {
            while (PhotonNetwork.Time < dueNetworkTime) yield return null;
            SpawnNote(lane, type);
        }

        public void VerdictDelta(int actorNum, int delta)
        {
            if (!_verdictSumByActor.ContainsKey(actorNum))
                _verdictSumByActor[actorNum] = 0;
            _verdictSumByActor[actorNum] += delta;
        }

        int GetVerdictBonus(int actorNum)
        {
            int summary = _verdictSumByActor.TryGetValue(actorNum, out var result) ? result : 0;
            return Mathf.Clamp(summary, -10, +15);
        }

        /// <summary>
        /// 예산에 맞는 fake 규칙 구현
        /// 리스트 (Fake 규칙 + 소진)
        /// </summary>
        /// <param name="budget"></param>
        /// <returns></returns>
        List<NoteType> TypesByBudget(int budget)
        {
            var list = new List<NoteType>(budget);

            // 예산 35 이상이면 Fake 최소1~최대4 (예산 내에서)
            if (budget >= 35)
            {
                int maxByBudget = budget / 4; // Fake=4점
                int fakeCount = Mathf.Clamp(Random.Range(1, 5), 1, Mathf.Min(4, maxByBudget));
                for (int i = 0; i < fakeCount; i++) list.Add(NoteType.Fake);
                budget -= fakeCount * 4;
            }

            // 남은 예산으로 터치 (1)/ 지속노트(2)로 랜덤 소진
            while (budget > 0)
            {
                if (budget >= 2 && Random.value < 0.5f)
                {
                    list.Add(NoteType.Continue); budget -= 2;
                }
                else
                {
                    list.Add(NoteType.Touch); budget -= 1;
                }
            }
            return list;
        }


        void SpawnNote(int lane, NoteType type)
        {
            if (!_typeToPrefab.TryGetValue(type, out var prefabName))
                prefabName = touchName;

            int noteId = ++_seqId;
            LaneManager.Instance.RegisterNote(noteId, lane);

            photonView.RPC(nameof(RPC_NoteSpawn),
                RpcTarget.All, prefabName, lane, speed, noteId);
        }

        [PunRPC]
        void RPC_NoteSpawn(string prefabName, int lane, float speed, int noteId)
        {
            if (!_notePools.TryGetValue(prefabName, out var pool)) return;

            var pose = GameManager.Instance.GetLaneSpawnPose(lane);
            var note = pool.PopPool();
            note.transform.SetPositionAndRotation(pose.position, pose.rotation);

            if (note.TryGetComponent(out Note mover))
            {
                mover.SetSpeed(speed);
                mover.SetMoveDirection(pose.rotation * Vector3.forward);
                mover.Init(noteId, lane);
            }
            _activeById[noteId] = note;
        }

        [PunRPC]
        private void RPC_DestroyNote(int noteId, bool isHit)
        {
            // noteId에 해당하는 로컬 인스턴스만 반납
            if (_activeById.TryGetValue(noteId, out var inst))
            {
                _activeById.Remove(noteId);
                if (inst.TryGetComponent(out Note mover))
                {
                    //적중 시 히트 이펙트
                    if (isHit)
                    {
                        var effect = GetEffectPool(mover.Type);
                        var particle = effect.GetComponent<PooledEffect>();
                        particle.PlayEffect(mover.transform.position, Quaternion.identity);
                        Debug.Log("적중");
                    }

                    mover.ReturnPool();
                }
            }
        }

        //스폰 정지
        public void StopSpawn()
        {
            if (!_isSpawning) return;

            _isSpawning = false;

            foreach (var list in _laneLists)
            {
                if (list.Value == null) continue;
                foreach (var co in list.Value)
                    if (co != null)
                        StopCoroutine(co);
            }
            _laneLists.Clear();
        }

        // 마스터가 검증 후 파괴 브로드캐스트할 때 씀
        public void DestroyNote(int noteId, bool isHit)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_DestroyNote), RpcTarget.All, noteId, isHit);
        }

        public bool TryGetNote(int noteId, out Note note)
        {
            note = null;
            if (_activeById.TryGetValue(noteId, out var pooled) && pooled.TryGetComponent(out Note _note))
            {
                note = _note;
                return true;
            }
            return false;
        }

        double BeatSec() => 60.0 / _songBpm;
        double SongNetworkTime() => _gameStartTime + _songOffsetSec; // 비트 0의 네트워크 시각
        double BeatTimeNetwork(int beatIndex) => SongNetworkTime() + beatIndex * BeatSec();

        //[start, end) 범위 안에 들어오는 비트 인덱스 계산
        void GetBeatRange(double start, double end, out int firstBeat, out int lastBeat)
        {
            double bSec = BeatSec();
            // 첫 비트: 창 시작 이상인 최초 비트
            firstBeat = Mathf.CeilToInt((float)((start - SongNetworkTime()) / bSec));
            // 마지막 비트: 창 끝 미만인 마지막 비트
            lastBeat = Mathf.FloorToInt((float)((end - SongNetworkTime()) / bSec));
        }
    }
}
