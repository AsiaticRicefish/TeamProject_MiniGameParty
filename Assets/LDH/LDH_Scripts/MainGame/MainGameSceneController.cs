using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace LDH_MainGame
{
    public class MainGameSceneController : BaseGameSceneController
    {
        public static MainGameSceneController Instance { get; private set; }
        protected override string GameType => "Main";

        [Header("초기화 대상 (IGameComponent, ICouroutineGameComponent)")] [SerializeField]
        private string[] roomObjectPaths;

        [SerializeField] private GameObject[] initializeObjects;

        private readonly List<IGameComponent> _sequential = new();
        private readonly List<ICoroutineGameComponent> _parallel = new();
        private readonly Dictionary<IGameComponent, Type> _seqTypeMap = new(); // 선택
        private readonly Dictionary<ICoroutineGameComponent, Type> _parTypeMap = new(); // 선택


        private UI_Loading _uiLoading;


        #region 초기화 구현(BasSceneController Implement)

        private void Awake()
        {
            //todo: 로딩 패널 켜는 시점 옮기기(로비 씬에서 켜기)
            _uiLoading = Manager.UI.CreatePopupUI<UI_Loading>();
            Manager.UI.ShowPopupUI(_uiLoading).Forget();

            if (Instance == null)
                Instance = this;
        }

        /// <summary>
        /// - 메인 게임 씬 UI 활성화 or 배치
        /// - 메인 게임 매니저 초기화
        /// - 맵 초기화
        /// </summary>
        /// <returns></returns>
        protected override IEnumerator WaitForManagersAwake()
        {
            //플레이어 UID가 있는지 확인 (임시 메서드)
            yield return WaitForAllPlayerUids(5f);

            //룸 오브젝트 생성
            yield return StartCoroutine(CreateRoomObjects());

            //타입 체크 및 type list 초기화
            yield return StartCoroutine(SetInitializeList());

            // 초기화가 필요한 대상(매니저 등 initializeTargets에 있는 요소들)이 생성될 때까지 대기  
            foreach (var seqType in _seqTypeMap.Values)
            {
                //초반에 배열에 있는 타입들을 찾아서 initializeTypes에 추가해두었으므로 이 타입을 넘긴다.
                yield return WaitForSingletonReady(seqType);
            }

            foreach (var parType in _parTypeMap.Values)
            {
                yield return WaitForSingletonReady(parType);
            }

            Util_LDH.ConsoleLog(this, "메인 게임에 필요한 Manager들 생성 완료");
        }

        protected override IEnumerator InitializeSequentialManagers()
        {
            Util_LDH.ConsoleLog(this, "SequenctialManager 초기화를 시작합니다.");
            yield return StartCoroutine(InitializeComponentsSafely(_sequential));
        }

        protected override IEnumerator InitializeParallelManagers()
        {
            Util_LDH.ConsoleLog(this, "ParallelManager 초기화를 시작합니다.");
            yield return StartCoroutine(InitializeCoroutineComponentsSafely(_parallel));
        }

        protected override void NotifyGameStart()
        {
            // 모든 초기화가 완료되고 게임 시작을 알림
            Util_LDH.ConsoleLog(this, "모든 초기화가 완료되었습니다. 게임을 시작합니다.");

            // 로딩 패널을 꺼주기
            Manager.UI.CloseTopPopupUI();

            //메인 게임 매니저가 게임을 시작
            MainGameManager.Instance.StartGame();
        }

        #endregion


        #region Temp

        private IEnumerator WaitForAllPlayerUids(float timeoutSec = 5f)
        {
            float end = Time.time + timeoutSec;
            while (Time.time < end)
            {
                var list = PhotonNetwork.PlayerList;
                bool allHaveUid = list != null && list.Length > 0 && list.All(p =>
                    p.CustomProperties != null &&
                    p.CustomProperties.TryGetValue("uid", out var v) &&
                    v is string s && !string.IsNullOrEmpty(s));

                if (allHaveUid) yield break;
                yield return new WaitForSeconds(0.1f);
            }

            Debug.LogWarning("[Init] Not all players have UID. Continue anyway.");
        }

        #endregion


        #region Editor / Type Setting / Type Util

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
            _sequential.Clear();
            _parallel.Clear();
            _seqTypeMap.Clear();
            _parTypeMap.Clear();
            
            foreach (var go in initializeObjects)
            {
                Register(go);
            }
            
            Util_LDH.ConsoleLog(this, "초기화 대상 리스트, 맵 세팅 완료");
            yield return null;
        }

        private IEnumerator CreateRoomObjects()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                foreach (var path in roomObjectPaths)
                {
                    var roomObject = PhotonNetwork.InstantiateRoomObject(path, Vector3.zero, Quaternion.identity);
                }
            }

            yield return null;
        }


        public void Register(GameObject go)
        {
            var seqHashSet = new HashSet<object>();
            var parHashSet = new HashSet<object>();


            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb is IGameComponent seq && seqHashSet.Add(seq))
                {
                    _sequential.Add(seq);
                    _seqTypeMap[seq] = seq.GetType();
                }

                if (mb is ICoroutineGameComponent par && parHashSet.Add(par))
                {
                    _parallel.Add(par);
                    _parTypeMap[par] = par.GetType();
                }
            }
        }
    }

    #endregion
}
