using System;
using System.Collections.Generic;
using Data;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LDH.LDH_Scripts.Test
{
    public class AddressableTest : MonoBehaviour
    {
        private readonly List<AsyncOperationHandle<GameObject>> _spawnOps = new();

        
        public async void SpawnCharacterPrefab(string id)
        {
            if (!CatalogProvider.TryGetCharacter(id, out var def) || def?.prefabRef == null)
                return;
            
            // trackHandle 기본값 true (건드리지 말기)
            var handle = def.prefabRef.InstantiateAsync(); 
            _spawnOps.Add(handle);

            var go = await handle.Task;
            go.name = "새로운 오브젝트";
        }
        
        private void OnDestroy()
        {
            // 인스턴스 해제(핸들 버전)
            foreach (var h in _spawnOps)
                if (h.IsValid()) Addressables.ReleaseInstance(h);
            _spawnOps.Clear();
        }
        
    }
}