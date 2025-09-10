using System;
using Customization;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace LDH_Game
{
    public class GameBootstrap : MonoBehaviour
    {
        private async void Awake()
        {
            DontDestroyOnLoad(gameObject);
            await Addressables.InitializeAsync().Task;
            try
            {
                await CatalogProvider.InitAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                if (this != null) Destroy(gameObject);
            }
        }
    }
}