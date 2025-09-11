using System;
using Customization;
using Cysharp.Threading.Tasks;
using Managers;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace LDH_Game
{
    public class GameBootstrap : MonoBehaviour
    {
        private async void Start()
        {
            //작업이 완료되지 않았는데 씬이 전환되는 경우 파괴되지 않도록 하기 위해 dont destroy 처리
            DontDestroyOnLoad(gameObject);


            var token = this.GetCancellationTokenOnDestroy();
            // 모든 매니저 준비 완료까지 안전 대기
            await UniTask.WaitUntil(() => Manager.Custom != null);

            Debug.Log("대기완료");

            await Addressables.InitializeAsync().Task;

            // 초기화 작업들
            try
            {
                await CatalogProvider.InitAsync();
                await Manager.Custom.InitAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                // 모든 초기화 작업이 완료되면 스스로를 파괴한다.
                if (this != null) Destroy(gameObject);
            }
        }
    }
}