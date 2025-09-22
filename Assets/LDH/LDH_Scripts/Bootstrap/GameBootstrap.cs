using System;
using Customization;
using Cysharp.Threading.Tasks;
using Data;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions.Must;

namespace LDH_Game
{
    public class GameBootstrap : MonoBehaviour
    {
        private UI_Loading _loadingUI;
        private const string loadingThemePath = "Data/Lobby_Theme";
        
        
        /// <summary>
        /// - 어드레서블 다운로드
        /// - 커스터마이징을 위한 매니저/컨트롤러 초기화(CatalogProvider, CustomManager)
        /// - 파이어베이스 유저 데이터 가져오기
        /// </summary>
        private async void Start()
        {
            
            // ------ Phase 구성 --------
            // 1) 트리 전체 생성
            //    루트노드
            var root = new ProgressNode(p =>
            {
                _loadingUI.SetProgress(p);
                Debug.Log($"<color=red> loading total progress = {p}</color>");
            });
            //   3개의 페이즈로 분할(0단계, 1단계, 2단계)
            var phases = root.Fan(0.25f, 0.55f, 0.20f);
            var phaseInit  = phases[0];
            var phaseLoad  = phases[1];
            var phaseFinal = phases[2];
            
            //  1단계 페이즈 세부 분할
            var loadFan = phaseLoad.Fan(0.15f, 0.10f, 0.05f, 0.70f);
            var subStep1   = loadFan[0];
            var subStep2   = loadFan[1];
            var subStep3   = loadFan[2];
            var subStep4   = loadFan[3];
            
            //  1단계 페이지의 병렬 작업에 대한 세부 분할
            var par = subStep4.Fan(0.5f, 0.3f, 0.2f);
            var prCatalog = par[0];
            var prUser    = par[1];
            var prItems   = par[2];

            
            //-------  로딩창 설정 ---------
            SetupLoadingUI();
            Manager.UI.ShowPopupUI(_loadingUI).Forget();
            
            // ========== [0단계] ========== 
            Big("초기화 중");
            Small("초기화 준비…");
            // Util_LDH.ConsoleLog(this, "[0단계 - 1] 파괴되지 않도록 dont destroy처리");
            // 1) 작업이 완료되지 않았는데 씬이 전환되는 경우 파괴되지 않도록 하기 위해 dont destroy 처리
            DontDestroyOnLoad(gameObject);
            
            // Util_LDH.ConsoleLog(this, "[0단계 - 2] 데이터 베이스초기화");
            // 2) 데이터베이스 초기화 및 준비
            phaseInit.Report(0.1f);
            Small("서버 연결 준비 중…");
#if !TEST_WITHOUT_LOGIN
            await FirebaseBootstrap.InitializeAsync(Define_LDH.Urls.RTDB);
#endif
            // Util_LDH.ConsoleLog(this, "[0단계 - 3] DataManager, BackendManager Instance가 생성될때까지 대기");
            // 3) DataManager, BackendManager Instance가 생성될때까지 대기
            phaseInit.Report(0.4f);
            Small("인증 상태 확인…");

            await UniTask.WaitUntil(() => DataManager.Instance != null && BackendManager.Instance != null);
#if !TEST_WITHOUT_LOGIN
            var uid = BackendManager.Auth.CurrentUser.UserId;
            // Util_LDH.ConsoleLog(this, $"[0단계 - 4] UID 가져오기 : {uid}");
#endif
            
            //Util_LDH.ConsoleLog(this, "[0단계 - 5] DataBase Binding");
            phaseInit.Report(0.7f);
            Small("데이터 동기화 설정…");


#if TEST_WITHOUT_LOGIN
#else
            var userRepo = new RealTimeUserDataRepository(FirebaseBootstrap.Rtdb, FirebaseBootstrap.Root);
            var itemRepo = new FirestoreItemRepository(FirebaseBootstrap.Firestore);
#endif
   
            
#if TEST_WITHOUT_LOGIN                 
#else
            DataManager.Instance.BindUserDataRepository(userRepo,uid);
            DataManager.Instance.BindItemRepository(itemRepo);
#endif
            phaseInit.Complete();
            Util_LDH.ConsoleLog(this, "[0단계] 완료");
            
            
            //============= [1단계] ==================
          
            Big("리소스 및 데이터 로딩 중");

            // 1) Addressable 초기화
            Small("리소스 시스템 초기화…");
            await Addressables.InitializeAsync().Task;
            subStep1.Complete();
            
            // 2) 원격 카탈로그 최신화 (카탈로그 파일만 다운로드)
            Small("콘텐츠 업데이트 확인…");
            var updates = await Addressables.CheckForCatalogUpdates().Task;
            if (updates != null && updates.Count > 0)
            {
                Small("업데이트 적용 중…");
                Util_LDH.ConsoleLog(this, $"update 내역이 있습니다. 다운로드 시작");
                await Addressables.UpdateCatalogs(updates).Task;
            }
            else
            {
                Util_LDH.ConsoleLog(this, $"update 내역이 없습니다.");
            }
            subStep2.Complete();
            
            // 3) Addressables 선 다운로드
            Small("필요 용량 계산…");
            var keys = new object[] { CatalogProvider.CharacterLabel, CatalogProvider.EquipLabel };
            // 필요 용량 체크
            var sizeH = Addressables.GetDownloadSizeAsync(keys);
            await sizeH.Task;
            var bytes = sizeH.Result;
            Addressables.Release(sizeH);
            subStep3.Complete();
            
            // 4) 병렬 작업
            // - 카탈로그 SO 로드 및 초기화
            // - RTDB 로드 및 생성

            float catalogProgress = 0f, userProgress = 0f, itemsProgress = 0f;
            
            Small($"데이터 불러오는 중...");
            UniTask catalogTask = CatalogProvider.InitAsync((p) =>
            {
                catalogProgress = Mathf.Clamp(p, catalogProgress, 1f);
                prCatalog.Report(catalogProgress);
            });
            
            UniTask userDataTask = DataManager.Instance.LoadOrCreatedUserDataAsync(p =>
            {
                userProgress = Mathf.Clamp(p, userProgress, 1f);
                prUser.Report(userProgress);
            });
            UniTask itemDataTask = DataManager.Instance.LoadItemsDataAsync(p =>
            {
                itemsProgress= Mathf.Clamp(p, itemsProgress, 1f);
                prItems.Report(p);
            });
            
            
            await UniTask.WhenAll(catalogTask, userDataTask, itemDataTask);
            
            //============= [2단계] ==================

            Big("세션 진입 준비");
            // 5) 커스터마이징 매니저에서 DataManager에 저장된 데이터를 가져와 커스템 데이터를 셋팅해준다.
            Small("아바타 설정 적용…");
            await Manager.Custom.InitAsync();
            
            phaseFinal.Report(0.2f);
            // 6) 씬 이동 및 파괴를 위해 photon network로 연결
            Small("네트워크 연결 중…");
            PhotonNetwork.ConnectUsingSettings();
            
            phaseFinal.Report(0.7f);
            
        }


        private void SetupLoadingUI()
        {
            //로딩창 생성
            _loadingUI = Manager.UI.CreatePopupUI<UI_Loading>();
            //테마 적용
            UI_LoadingTheme theme = Resources.Load<UI_LoadingTheme>(loadingThemePath);
            _loadingUI.ApplyTheme(theme);
            
            //이벤트 설정
            _loadingUI.OnCloseRequested += DestroyGameBootstrap;
            _loadingUI.onSceneLoaded = (s) =>
            {
                Small("잠시 후 로비로 진입합니다!");
                _loadingUI.SetProgress(1f);
                _loadingUI.AutoCloseAfter(1f, this.destroyCancellationToken).Forget();
            };
        }

        private void DestroyGameBootstrap(UI_Base uiBase)
        {
            if (this != null && _loadingUI == uiBase)
            {
                _loadingUI = null;
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("Fail to destroy GameBootstrap object");
            }
               
        }
        
        void Big(string s)   => _loadingUI?.SetBigDescription(s);
        void Small(string s) => _loadingUI?.SetSmallDescription(s);

    }
}