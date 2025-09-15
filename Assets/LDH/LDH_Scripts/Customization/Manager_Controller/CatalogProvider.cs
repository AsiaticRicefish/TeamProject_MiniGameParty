using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Customization
{
    /// <summary>
    /// Addressables 라벨을 스캔해 Character/Equip Definition(SO)를 모두 로드하고
    /// id 기반으로 조회할 수 있게 제공하는 카탈로그 제공자.
    /// </summary>
    public static class CatalogProvider
    {
        // 라벨 상수(프로젝트에서 바꾸고 싶으면 여기만 수정)
        public const string CharacterLabel = "catalog:character";
        public const string EquipLabel     = "catalog:equip";
        
        public static IReadOnlyDictionary<string, CharacterDefinition> Characters { get; private set; }
        public static IReadOnlyDictionary<string, EquipmentDefinition> Equips { get; private set; }
        
        // 정렬된 열람용
        public static IReadOnlyList<CharacterDefinition> CharactersSorted { get; private set; }
        public static IReadOnlyList<EquipmentDefinition>  EquipsSorted    { get; private set; }
        
        // 나중에 Release 하기 위한 용도
        private static AsyncOperationHandle<IList<CharacterDefinition>> _charHandle;
        private static AsyncOperationHandle<IList<EquipmentDefinition>>  _equipHandle;

        public static bool IsReady => Characters != null && Equips != null;


        private static bool _busy;
        
        /// <summary>
        /// Addressables.InitializeAsync() 이후 호출해야 함.
        /// SO를 모두 로드하여 id→정의 인덱스를 만든다.
        /// </summary>
        public static async UniTask InitAsync(Action<float> progressReport = null)
        {
            progressReport?.Invoke(0f);
            // 여러 자산: LoadAssetsAsync(label, callback)
            _charHandle = Addressables.LoadAssetsAsync<CharacterDefinition>(CharacterLabel, null);
            _equipHandle = Addressables.LoadAssetsAsync<EquipmentDefinition>(EquipLabel, null);
            
            
            // 모든 so 가져오기
            IList<CharacterDefinition> charList  = await _charHandle.Task;
            IList<EquipmentDefinition> equipList = await _equipHandle.Task;
            progressReport?.Invoke(0.3f);
            
            Characters = charList?
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.id)) //id가 null인지 검증
                .GroupBy(x => x.id) // id로 그룹화하여 중복 so 정리
                .ToDictionary(g => g.Key, g => g.First()); // 각 그룹(id로 묶은 그룹)을 딕셔너리의 요소로 전환(key = 그룹의 key = id, value = 그룹의 첫번째 항목 = 첫 번째 character definition

            progressReport?.Invoke(0.5f);
            Equips = equipList?
                .Where(x => x != null && !string.IsNullOrEmpty(x.id))
                .GroupBy(x => x.id)
                .ToDictionary(g => g.Key, g => g.First());
            progressReport?.Invoke(0.7f);
            
            // 정렬 리스트
            CharactersSorted = Characters?.Values
                .OrderBy(def => def.number).ThenBy(def => def.id, StringComparer.Ordinal)
                .ToArray();
            
            EquipsSorted = Equips?.Values
                .OrderBy(def => def.number)
                .ThenBy(def => def.id, StringComparer.Ordinal)
                .ToArray();
            
            progressReport?.Invoke(1f);
            Debug.Log($"[CatalogProvider] Init 완료 : Characters {Characters.Values.Count()} 개, Equips : {Equips.Values.Count()} 개 등록 완료");

        }

        
        public static bool TryGetCharacter(string id, out CharacterDefinition def) => Characters.TryGetValue(id, out def);
        public static bool TryGetEquip(string id, out EquipmentDefinition def) => Equips.TryGetValue(id, out def);
        
        
        public static async UniTask RefreshAsync(string characterLabel = CharacterLabel, string equipLabel = EquipLabel)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var updates = await Addressables.CheckForCatalogUpdates().Task;
                if (updates != null && updates.Count > 0)
                    await Addressables.UpdateCatalogs(updates).Task;
                
                // 이전 핸들 해제 후 재로딩
                if (_charHandle.IsValid())  Addressables.Release(_charHandle);
                if (_equipHandle.IsValid()) Addressables.Release(_equipHandle);
                
                Characters = null;
                Equips     = null;
                
                await InitAsync();
            }
            finally { _busy = false; }
            
        }

        public static void Clear()
        {
            if (_charHandle.IsValid())  Addressables.Release(_charHandle);
            if (_equipHandle.IsValid()) Addressables.Release(_equipHandle);

            Characters = null;
            Equips     = null;
            
            Resources.UnloadUnusedAssets();
        }
        
    }
}