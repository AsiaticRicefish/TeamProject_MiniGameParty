using System;
using Customization;
using Cysharp.Threading.Tasks;
using Data;
using LDH_Util;
using Managers;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace LDH_Game
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private float dataProgress;
        [SerializeField] private float catalogProgress;
        [SerializeField] private float totalProgress;
        
        
        /// <summary>
        /// - 어드레서블 다운로드
        /// - 커스터마이징을 위한 매니저/컨트롤러 초기화(CatalogProvider, CustomManager)
        /// - 파이어베이스 유저 데이터 가져오기
        /// </summary>
        private async void Start()
        {
            // [0단계]
            // 1) 작업이 완료되지 않았는데 씬이 전환되는 경우 파괴되지 않도록 하기 위해 dont destroy 처리
            DontDestroyOnLoad(gameObject);
            
            
           Util_LDH.ConsoleLog(this, "BackendManager의 RTDB 준비를 대기.");
            // 2) BackendManager RTDB 준비를 대기
            await BackendManager.WhenDatabaseReady();
               
            Util_LDH.ConsoleLog(this, "BackendManager의 RTDB 준비 완료.");
            // 3) UID 확보
            var uid = BackendManager.Auth.CurrentUser.UserId;
            Util_LDH.ConsoleLog(this, $"UID 가져오기 : {uid}");

            // [1단계]
            // 1) Addressable 초기화
            await Addressables.InitializeAsync().Task;
            
            // 2) 원격 카탈로그 최신화 (카탈로그 파일만 다운로드)
            var updates = await Addressables.CheckForCatalogUpdates().Task;
            if (updates != null && updates.Count > 0)
            {
                Util_LDH.ConsoleLog(this, $"update 내역이 있습니다. 다운로드 시작");
                await Addressables.UpdateCatalogs(updates).Task;
            }
            else
            {
                Util_LDH.ConsoleLog(this, $"update 내역이 없습니다.");
            }
            
            // 3) Addressables 선 다운로드
            var keys = new object[] { CatalogProvider.CharacterLabel, CatalogProvider.EquipLabel };
            // 필요 용량 체크
            var sizeH = Addressables.GetDownloadSizeAsync(keys);
            await sizeH.Task;
            var bytes = sizeH.Result;
            Addressables.Release(sizeH);
            
            // 4) 병렬 작업
            // - 카탈로그 SO 로드 및 초기화
            // - RTDB 로드 및 생성
            
            UniTask catalogTask = CatalogProvider.InitAsync((d) =>
            {
                Util_LDH.ConsoleLog(this, $"catalog progress : {catalogProgress*100}");
                catalogProgress = d;
            });
            UniTask dataTask = DataManager.Instance.LoadOrCreatedUserDataAsync();

            await UniTask.WhenAll(catalogTask, dataTask);
            
            // 5) 커스터마이징 매니저에서 DataManager에 저장된 데이터를 가져와 커스템 데이터를 셋팅해준다.
            await Manager.Custom.InitAsync();
            

            // 6) 씬 이동 및 파괴를 위해 photon network로 연결
            PhotonNetwork.ConnectUsingSettings();
            
            if(this!=null)
                Destroy(gameObject);
            
        }
        
    }
}