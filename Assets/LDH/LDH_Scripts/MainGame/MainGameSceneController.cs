using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace LDH_MainGame
{
    public class MainGameSceneController : BaseGameSceneController
    {
        public static MainGameSceneController Instance { get; private set; }
        protected override string GameType => "Main";
        

        [Header("초기화 대상 (IGameComponent, ICouroutineGameComponent)")] [SerializeField]
        private string mainGameManagerPrefabPath;
        // [SerializeField] private string photonViewSyncPrefabPath;
        [SerializeField] private GameObject[] initializeObjects;
        
        private readonly List<IGameComponent> _sequential = new();
        private readonly List<ICoroutineGameComponent> _parallel = new();
        private readonly Dictionary<IGameComponent, Type> _seqTypeMap = new(); // 선택
        private readonly Dictionary<ICoroutineGameComponent, Type> _parTypeMap = new(); // 선택


        private UI_Loading _uiLoading;
        [SerializeField] private int[] spawnedViewIds; // 마스터가 뿌린 ViewID 목록을 받는 버퍼


        #region 초기화 구현(BasSceneController Implement)

        protected override void Awake()
        {
            base.Awake();
            
            if (Instance == null)
                Instance = this;

            _sequential.Clear();
            _parallel.Clear();
            
            _uiLoading = _uiLoading ? _uiLoading : Manager.UI.PeekPopupUI<UI_Loading>();
            
        }

        /// <summary>
        /// - 메인 게임 씬 UI 활성화 or 배치
        /// - 메인 게임 매니저 초기화
        /// - 맵 초기화
        /// </summary>
        /// <returns></returns>
        protected override IEnumerator WaitForManagersAwake()
        {
            //플레이어 인원수 확인 및 모든 플레이어 ui 확인
            yield return WaitForAllPlayerUids(5f);
            _uiLoading?.SetProgress(0.2f);
            Debug.Log("2");
            //룸 오브젝트 - 메인 게임 매니저 생성
            yield return StartCoroutine(EnsureRoomObjects(new[] { mainGameManagerPrefabPath }));
            _uiLoading?.SetProgress(0.4f);
            Debug.Log("3");
            //타입 체크 및 type list 초기화
            yield return StartCoroutine(SetInitializeList());
            _uiLoading?.SetProgress(0.6f);
            Debug.Log("4");

            // 초기화가 필요한 대상(매니저 등 initializeTargets에 있는 요소들)이 생성될 때까지 대기  
            foreach (var seqType in _seqTypeMap.Values)
            {
                //초반에 배열에 있는 타입들을 찾아서 initializeTypes에 추가해두었으므로 이 타입을 넘긴다.
                yield return WaitForSingletonReady(seqType);
            }
            Debug.Log("5");

            _uiLoading?.SetProgress(0.7f);

            foreach (var parType in _parTypeMap.Values)
            {
                yield return WaitForSingletonReady(parType);
            }

            // Util_LDH.ConsoleLog(this, "메인 게임에 필요한 Manager들 생성 완료");
        }

        protected override IEnumerator InitializeSequentialManagers()
        {
            // Util_LDH.ConsoleLog(this, "SequenctialManager 초기화를 시작합니다.");
            yield return StartCoroutine(InitializeComponentsSafely(_sequential));
        }

        protected override IEnumerator InitializeParallelManagers()
        {
            // Util_LDH.ConsoleLog(this, "ParallelManager 초기화를 시작합니다.");
            yield return StartCoroutine(InitializeCoroutineComponentsSafely(_parallel));
        }

        protected override async void NotifyGameStart()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(1f));
            _uiLoading?.SetProgress(0.9f);
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
            _uiLoading?.SetProgress(1f);
            
            // 모든 초기화가 완료되고 게임 시작을 알림
            // Util_LDH.ConsoleLog(this, "모든 초기화가 완료되었습니다. 게임을 시작합니다.");

            // 포톤뷰 싱크 플래그 끄기
            PhotonViewSync.Instance.Clear();

            // 로딩 패널을 꺼주기
            if(Manager.UI.PeekPopupUI<UI_Loading>() !=null)
                await Manager.UI.CloseTopPopupUI();

            //메인 게임 매니저가 게임을 시작
            Debug.Log("메인 게임 매니저 초기화? " + MainGameManager.Instance.Initalized);
            MainGameManager.Instance.StartGame();
        }

        #endregion

        #region Uid

        private IEnumerator WaitForAllPlayerUids(float timeoutSec = 5f)
        {
            float t = 0f;
            while (t < timeoutSec)
            {
                // 방이 없거나 연결 안되면 다음 프레임
                if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                {
                    yield return null;
                    t += Time.deltaTime;
                    continue;
                }

                var list = PhotonNetwork.PlayerList;
                if (list != null && list.Length > 0)
                {
                    if (list.Length != PhotonNetwork.CurrentRoom.MaxPlayers)
                    {
                        // 중간에 누군가가 탈주
                        AbortInit("[MainGameSceneController] 플레이 인원 수 != 최대 인원 수. 게임을 중지합니다.");

                    }
                    
                    // 현재 인원 기준으로 판정
                    bool allHaveUid = list.All(p =>
                        p.CustomProperties != null &&
                        p.CustomProperties.TryGetValue("uid", out var v) &&
                        v is string s && !string.IsNullOrEmpty(s));

                    if (allHaveUid) yield break;
                }

                t += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            AbortInit("[MainGameManager] Init - WaitForAllPlayerUids timeout.");
        }

        #endregion

        #region Editor / Type Setting / Room Object 생성

        private void OnValidate()
        {
            // 에디터에서 미리 검증(실수 방지)
            foreach (var go in initializeObjects)
            {
                if (!go) continue;
                var hasAny = go.GetComponents<MonoBehaviour>().Any(mb =>
                    mb is IGameComponent || mb is ICoroutineGameComponent);
                if (!hasAny)
                    Debug.LogWarning($"{go.name} : IGameComponent/ICoroutineGameComponent 구현체가 없음", go);
            }
        }

        private IEnumerator SetInitializeList()
        {
            _seqTypeMap.Clear();
            _parTypeMap.Clear();

            // Debug.Log($"initialize objects 개수 : {initializeObjects.Length}");
            foreach (var go in initializeObjects)
            {
                Register(go);
            }

            // Util_LDH.ConsoleLog(this, "초기화 대상 리스트, 맵 세팅 완료");
            yield return null;
        }

        private IEnumerator EnsureRoomObjects(string[] roomObjectPaths)
        {
            Debug.Log("2-2");
            // 0) 이미 누군가가 스폰해둔 경우(마스터 교체 등): 프로퍼티만 기다리면 됨
            if (TryGetRoomObjectIds(out var idsFromProp) && idsFromProp.Length == roomObjectPaths.Length)
            {
                yield return WaitForLocalViews(idsFromProp);
                yield break;
            }

            // 마스터면 책임지고 보충
            if (PhotonNetwork.IsMasterClient)
            {
                // 아직 세팅 안되어 있으면 내가 생성해서 세팅
                if (!TryGetRoomObjectIds(out var cur) || cur == null || cur.Length == 0)
                {
                    yield return StartCoroutine(SpawnAndSetRoomObjectIds(roomObjectPaths));
                }
            }
            
            // 세팅이 되었는지 체크
            if (TryGetRoomObjectIds(out var finalIds) && finalIds.Length == roomObjectPaths.Length)
            {
                yield return WaitForLocalViews(finalIds);
            }
        }

        private IEnumerator SpawnAndSetRoomObjectIds(string[] roomObjectPaths)
        {
            var ids = new List<int>();

            foreach (var path in roomObjectPaths)
            {
                Debug.Log($"[RoomSpawn] Try spawn path='{path}' (master={PhotonNetwork.IsMasterClient})");

                var ro = PhotonNetwork.InstantiateRoomObject(path, Vector3.zero, Quaternion.identity);
                if (ro == null)
                {
                    AbortInit($"RoomObject spawn failed: {path} (null)");
                    yield break;
                }
                
                if (!ro.TryGetComponent(out PhotonView pv))
                {
                    AbortInit($"RoomObject missing PhotonView: {path}");
                    yield break;
                }
                Debug.Log($"[RoomSpawn] Spawned '{ro.name}' ViewID={pv.ViewID}");
                ids.Add(pv.ViewID);
            }

            // 룸 프로퍼티에 기록 (idempotent)
            var props = new ExitGames.Client.Photon.Hashtable
            {
                { Define_LDH.RoomProps.RoomObjectsViewIds, ids.ToArray() }
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            Debug.Log($"[RoomSpawn] Saved IDs -> [{string.Join(",", ids)}]");
            yield return null; // 한 프레임 보장
        }

        private bool TryGetRoomObjectIds(out int[] ids)
        {
            ids = null;
            var room = PhotonNetwork.CurrentRoom;
            if (room?.CustomProperties == null) return false;

            if (!room.CustomProperties.TryGetValue(Define_LDH.RoomProps.RoomObjectsViewIds, out var v))
                return false;
            
            try
            {
                if (v is int[] arr) ids = arr;
                else if (v is object[] oarr) ids = Array.ConvertAll(oarr, o => Convert.ToInt32(o));
                else
                {
                    Debug.LogError($"[RoomProps] Unexpected type: {v.GetType()}");
                    return false;
                }
                Debug.Log($"[RoomProps] IDs from room -> [{string.Join(",", ids)}]");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RoomProps] Parse error: {e.Message}");
                return false;
            }
            
        }

        private IEnumerator WaitForLocalViews(int[] viewIds)
        {
            float t = 0f;
            while (true)
            {
                var missing = viewIds.Where(id => PhotonView.Find(id) == null).ToArray();
                if (missing.Length == 0) break;
                //
                // bool allReady = true;
                //
                // for (int i = 0; i < viewIds.Length; i++)
                // {
                //     if (PhotonView.Find(viewIds[i]) == null)
                //     {
                //         allReady = false;
                //         break;
                //     }
                // }
                //
                // if (allReady)
                //     break;

                t += Time.deltaTime;
                if (t > initTimeout)
                {Debug.LogError($"[WaitForLocalViews] timeout. Missing IDs: [{string.Join(",", missing)}] " +
                                $"(scene='{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}', " +
                                $"playerIsMaster={PhotonNetwork.IsMasterClient})");
                    AbortInit("WaitForLocalViews timeout.");
                    yield break;
                }

                yield return null;
            }

            yield return null;
        }
        
        // [PunRPC]
        // private void RPC_AnnounceRoomObjects(int[] viewIds)
        // {
        //     spawnedViewIds = viewIds;
        // }

        public void Register(GameObject go)
        {
            // Debug.Log("등록 시작");
            var seqHashSet = new HashSet<object>();
            var parHashSet = new HashSet<object>();


            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb is IGameComponent seq && seqHashSet.Add(seq))
                {
                    // Debug.Log("순차 대상 대상");
                    _sequential.Add(seq);
                    _seqTypeMap[seq] = seq.GetType();
                }

                if (mb is ICoroutineGameComponent par && parHashSet.Add(par))
                {
                    // Debug.Log($"병렬 대상 : {go.name}");
                    _parallel.Add(par);
                    _parTypeMap[par] = par.GetType();
                }
            }
        }

        #endregion

        #region pun call backs / 타임아웃 대응 로직 override

        // 타임아웃이 된 경우 처리
        protected override void AbortInit(string reason)
        {
            base.AbortInit(reason);
            //모든 코루틴 중지
            StopAllCoroutines();
            
            // main game manager 가 존재한다면? 게임 강제 중지 로직 실행
            if (MainGameManager.Instance != null && MainGameManager.Instance.Initalized)
                MainGameManager.Instance.ForceStopGame();

            // 없는 경우 직접 게임 강제 중지 시키기
            else
            {
                Debug.LogWarning("[MainGameSceneController] main game manager가 아직 초기화되지 않아서 scene controller에서 강제 중지 시킵니다.");
                var quitPopup = Manager.UI.CreatePopupUI<UI_Popup_QuitGame>();
                Manager.UI.ShowPopupUI(quitPopup).Forget();
            }
        }

        #endregion
    }
}