using System;
using System.Collections.Generic;
using Customization;
using Cysharp.Threading.Tasks;
using LDH_Util;
using Managers;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace LDH.LDH_Scripts.Test
{
    public class CustomizingTest : MonoBehaviour
    {
        [SerializeField] private Transform charButtonRoot;
        [SerializeField] private Transform equipButtonRoot;
        
        [SerializeField] private Button buttonPrefab;
        [SerializeField] private AvatarStruct avatarStruct;
        

        // 아이콘 스프라이트 핸들 보관(나중에 Release 위해)
        private readonly Dictionary<string, UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Sprite>> _charIconHandles = new();
        private readonly Dictionary<string, UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Sprite>> _equipIconHandles = new();
        
        
        private async void Start()
        {
            var token = this.GetCancellationTokenOnDestroy();
            // 1) 카탈로그 & 커스터마이징 서비스가 준비될 때까지 대기
            await UniTask.WaitUntil(() => CatalogProvider.IsReady, cancellationToken: token);
            await UniTask.WaitUntil(() => Manager.Custom != null && Manager.Custom.IsReady, cancellationToken: token);
            
            
            CustomizationManager.Instance.ApplyToAvatarAsync(avatarStruct, CustomizationManager.Instance.GetEquippedLocal());
            
            foreach ((string id, var def) in  CatalogProvider.Characters)
            {
                string charId = id;
                var button = Instantiate(buttonPrefab, charButtonRoot);
                button.GetComponentInChildren<TextMeshProUGUI>().text = charId;
                
                var h = def.iconRef.LoadAssetAsync<Sprite>();
                await h.Task;
                if (!this) return; // 파괴 중이면 중단
                button.image.sprite = h.Result;
                _charIconHandles[charId] = h;

                
                button.onClick.AddListener(()=>
                {
                    Debug.Log($"버튼 클릭 : {charId}");
                    ChangeCharacter(charId).Forget();
                });

                button.transform.SetAsFirstSibling();
        
            }
            
            foreach ((string id, var def) in  CatalogProvider.Equips)
            {
                string equipId = id;
                var button = Instantiate(buttonPrefab, equipButtonRoot);
                button.GetComponentInChildren<TextMeshProUGUI>().text = equipId;
                var h = def.iconRef.LoadAssetAsync<Sprite>();
                await h.Task;
                if (!this) return;
                button.image.sprite = h.Result;
                _equipIconHandles[equipId] = h;
                
                button.onClick.AddListener(()=>
                {
                    Debug.Log($"버튼 클릭 : {equipId}");
                    ChangeEquip(equipId).Forget();
                });
                
                button.transform.SetAsFirstSibling();
            }
        }


        private async UniTask ChangeCharacter(string id)
        {
            Debug.Log("버튼 클릭으로 Change Character 호출됨");
            if (await Manager.Custom.UpdateCharacterAsync(id))
                await Manager.Custom.ApplyCharacterToAvatarAsync(avatarStruct, characterId: id);

        }
        
        
        private async UniTask ChangeEquip(string id)
        {
            Debug.Log("버튼 클릭으로 Change Equip 호출됨");
            if (await Manager.Custom.UpdateEquipAsync(id))
                await Manager.Custom.ApplyEquipToAvatarAsync(avatarStruct, equipId: id);

        }
        
        private void OnDestroy()
        {
            // 아이콘 스프라이트 해제(이 UI가 사라질 때가 안전한 시점)
            foreach (var h in _charIconHandles.Values)
                if (h.IsValid()) Addressables.Release(h);
            foreach (var h in _equipIconHandles.Values)
                if (h.IsValid()) Addressables.Release(h);

            _charIconHandles.Clear();
            _equipIconHandles.Clear();
        }
    }
}