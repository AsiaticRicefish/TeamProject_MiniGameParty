using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Data;
using DesignPattern;
using LDH_Util;
using Network;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Customization
{
    public class CustomizationManager : CombinedSingleton<CustomizationManager>, ICustomizationService
    {
        private CustomizationData customData => DataManager.Instance.Custom;

        [SerializeField] private Transform charPoolRegistry_Transform;
        [SerializeField] private Transform equipPoolRegistry_Transform;

        // 풀
        PrefabPoolRegistry _charPools;
        PrefabPoolRegistry _equipPools;

        // 아이콘 이미지 전용 캐시 / 핸들
        private readonly Dictionary<string, Sprite> _iconCache = new();
        private readonly Dictionary<string, AsyncOperationHandle<Sprite>> _iconHandles = new();


        public bool IsReady { get; private set; }

        protected override void OnAwake()
        {
            isPersistent = true;
            _charPools = new(charPoolRegistry_Transform);
            _equipPools = new(equipPoolRegistry_Transform);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += (_, _) => ReleaseAllIcons();
        }

        private void OnDisable()
        {
            ReleaseAllIcons();
            DisposePools();
        }

        public async UniTask InitAsync(string uid = null)
        {
            DataManager.Instance.OnCustomizationChanged += OnCustomizationChanged;

            IsReady = true;
            await UniTask.Yield();

            //Debug.Log("[CustomizationManager] Init 완료");
        }

        #region Interface 구현 - 커스터마이징 기능

        public UnimoCombo GetEquippedLocal() => customData.CurrentCombo;
        public bool HasCharacter(string id) => customData.ownedCharacters.Contains(id);
        public bool HasEquip(string id) => customData.ownedEquips.Contains(id);


        #region UpdateCombo

        /// <summary>
        ///장착 상태를 바꾸는 메서드 (데이터를 업데이트)
        ///소유여부 검증
        ///장착 상태 업데이트 -> 파이어베이스 데이터 업데이트
        ///</summary>
        /// <param name="characterId"></param>
        /// <param name="equipId"></param>
        /// <returns></returns>
        public async UniTask<bool> UpdateComboAsync(string characterId, string equipId)
        {
            if (!IsModified(characterId, equipId)) return false;
#if TEST_WITHOUT_LOGIN
            var ok = await Data.DataManager.Instance.UpdateCustomizationLocalAsync(characterId, equipId);
#else
            var ok = await Data.DataManager.Instance.UpdateCustomizationAsync(characterId, equipId);
#endif

            return ok;
        }

        public async UniTask<bool> UpdateComboAsync(UnimoCombo newCombo)
        {
            return await UpdateComboAsync(newCombo.characterId, newCombo.equipId);
        }

        public UniTask<bool> UpdateCharacterAsync(string characterId) =>
            UpdateComboAsync(characterId, customData.equipId);

        public UniTask<bool> UpdateEquipAsync(string equipId) => UpdateComboAsync(customData.characterId, equipId);

        #endregion

        #region Apply Character

        /// <summary>
        /// 장착 상태를 아바타(실제 오브젝트)에 적용하여 가시화하는 메서드. 프리팹을 이전꺼와 새로운걸 스왑
        /// 상점에서 미리보기 할 때, 룸에서 상대 플레이어의 아바타를 바꿀 때 등
        /// </summary>
        /// <param name="avatarStruct"></param>
        /// <param name="combo"></param>
        public async UniTask ApplyToAvatarAsync(AvatarStruct avatarStruct, UnimoCombo combo)
        {
            await ApplyToAvatarAsync(avatarStruct, combo.characterId, combo.equipId);
        }

        public async UniTask ApplyToAvatarAsync(AvatarStruct avatarStruct, string characterId = null,
            string equipId = null)
        {
//            Debug.Log("[CustomizationManager] 캐릭터+엔진 동시 적용을 시작합니다.");
            if (characterId != null)
                await ApplyCharacterToAvatarAsync(avatarStruct, characterId);
            if (equipId != null)
                await ApplyEquipToAvatarAsync(avatarStruct, equipId);
            //    Debug.Log("[CustomizationManager] 캐릭터+엔진 동시 적용 완료");
        }

        public async UniTask ApplyCharacterToAvatarAsync(AvatarStruct avatarStruct, string characterId)
        {
            var targetChar = characterId ?? customData.characterId;

            // 캐릭터만 바뀐 경우에만 스왑
            //기존 캐릭터를 풀에 반납
//            Debug.Log("[CustomizationManager] 유니모가 변경되었는지를 확인합니다.");

            if (!string.IsNullOrEmpty(characterId) && avatarStruct.CurrentCharacterId != targetChar &&
                CatalogProvider.TryGetCharacter(characterId, out var cDef))
            {
//                Debug.Log("[CustomizationManager] 유니모가 변경됨");
                if (avatarStruct.CurrentCharacter && !string.IsNullOrEmpty(avatarStruct.CurrentCharacterId))
                {
                    //         Debug.Log("[CustomizationManager] 기존에 적용된 유니모를 Release 합니다.");
                    _charPools.ReleaseInstance(avatarStruct.CurrentCharacterId, avatarStruct.CurrentCharacter);
                }


                //남아있는 잔여 오브젝트 제거
//                Debug.Log($"[CustomizationManager] 남아있는 잔여 오브젝트 {avatarStruct.characterRoot.childCount}개를 제거합니다.");
                Util_LDH.RemoveAllChildren(avatarStruct.characterRoot);


                //새로운 캐릭터를 꺼내와서 적용한다.
//                Debug.Log($"[CustomizationManager] 새로운 유니모를 꺼내와서 적용합니다.");
                var charObj = await _charPools.GetInstanceAsync(cDef.id, cDef.prefabRef, avatarStruct.characterRoot);

                await avatarStruct.BindCharacter(charObj, cDef.id);
            }
            else
            {
                // Debug.Log("[CustomizationManager] 유니모가 변경되지 않음");
            }
        }

        public async UniTask ApplyEquipToAvatarAsync(AvatarStruct avatarStruct, string equipId)
        {
            var targetMount = equipId ?? customData.equipId;
//            Debug.Log("[CustomizationManager] 엔진이 변경되었는지를 확인합니다.");
            if (!string.IsNullOrEmpty(equipId) && avatarStruct.CurrentEquipId != equipId &&
                CatalogProvider.TryGetEquip(equipId, out var eDef))
            {
                if (avatarStruct.CurrentEquip && !string.IsNullOrEmpty(avatarStruct.CurrentEquipId))
                {
                    //Debug.Log("[CustomizationManager] 기존에 적용된 엔진을 Release 합니다.");
                    _equipPools.ReleaseInstance(avatarStruct.CurrentEquipId, avatarStruct.CurrentEquip);
                }

                //남아있는 잔여 오브젝트 제거
                //    Debug.Log($"[CustomizationManager] 남아있는 잔여 오브젝트 {avatarStruct.characterRoot.childCount}개를 제거합니다.");
                Util_LDH.RemoveAllChildren(avatarStruct.equipRoot);

                //새로운 탈 것 적용
                //  Debug.Log($"[CustomizationManager] 새로운 엔진을 꺼내와서 적용합니다.");
                var equipObj = await _equipPools.GetInstanceAsync(eDef.id, eDef.prefabRef, avatarStruct.equipRoot);

                await avatarStruct.BindEquip(equipObj, eDef.id);
            }
            else
            {
                //    Debug.Log("[CustomizationManager] 엔진이 변경되지 않음");
            }
        }

        #endregion

        public async UniTask<Sprite> GetIconAsync(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            if (!CatalogProvider.TryGetCharacter(characterId, out var def) || def.iconRef == null) return null;

            // 1) 이미 캐시에 있으면 바로 반환
            if (_iconCache.TryGetValue(characterId, out var cached))
                return cached;

            // 2) 이미 로드되어 있으면(같은 AssetReference) 재사용
            if (def.iconRef.Asset != null)
            {
                // 이미 로드되어서 메모리에 있는 실제 오브젝트가 존재한다면
                var sp = def.iconRef.Asset as Sprite;
                _iconCache[characterId] = sp;
                return sp;
            }

            if (def.iconRef.OperationHandle.IsValid())
            {
                var sp = def.iconRef.OperationHandle.Result as Sprite; // 메모리에 로드를 담당했던 핸들이 유효하
                _iconCache[characterId] = sp;
                return sp;
            }

            // 3) 처음 로드하는 경우: 핸들 저장
            var handle = def.iconRef.LoadAssetAsync<Sprite>();
            var sprite = await handle.Task;
            _iconHandles[characterId] = handle;
            _iconCache[characterId] = sprite;
            return sprite;
        }

        public void ReleaseAllIcons()
        {
            // 0) 카탈로그 준비 전이면 아무 것도 안 함
            if (!CatalogProvider.IsReady || CatalogProvider.Characters == null)
            {
                _iconCache?.Clear();
                return;
            }
            //      Debug.Log("[CustomizationManager] 모든 아이콘 handle을 release 합니다.");

            // 1) 캐싱된 스프라이트만 비움(여기엔 Addressables 핸들이 없으므로 단순 클리어)
            _iconCache?.Clear();

            // 2) 실제 Addressables 쪽은 "로드 이력이 있는" 것만 해제
            foreach (var def in CatalogProvider.Characters.Values)
            {
                if (def == null) continue;
                var aref = def.iconRef;
                if (aref == null) continue;

                // 이미 메모리에 로드되어 있던 경우
                // (Asset이 있거나, OperationHandle이 유효하고 완료된 경우)
                bool loadedAsset = aref.Asset != null;
                bool validHandle = aref.OperationHandle.IsValid();
                bool completedOk = validHandle &&
                                   aref.OperationHandle.IsDone &&
                                   aref.OperationHandle.Status == AsyncOperationStatus.Succeeded;

                if (loadedAsset || completedOk)
                {
                    try
                    {
                        aref.ReleaseAsset();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[ReleaseAllIcons] ReleaseAsset() 실패: {def.name} - {e.Message}");
                    }
                }
            }

            Resources.UnloadUnusedAssets();
        }

        //모든 풀 레지스트리 dispose
        public void DisposePools()
        {
            _charPools.DisposeAll();
            _equipPools.DisposeAll();
        }

        #endregion


        #region 데이터 저장 / Photon Properties 변경

        private void OnCustomizationChanged(CustomizationData c)
        {
            // Photon PlayerProperties 갱신, 필요한 뷰 업데이트 등
            UpdatePhotonPlayerProps(c.characterId, c.equipId);
        }

        private void UpdatePhotonPlayerProps(string charId, string equipId)
        {
            if (!Photon.Pun.PhotonNetwork.IsConnected) return;
            var table = new ExitGames.Client.Photon.Hashtable
            {
                {
                    Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId),
                    charId ?? string.Empty
                },
                {
                    Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.EquipId),
                    equipId ?? string.Empty
                }
            };

            PhotonNetwork.LocalPlayer.SetCustomProperties(table);

//            Debug.Log( "[CustomizationManager] PlayerProps에 custom 장착 data를 저장합니다. 장착한 character id :" + PhotonNetwork.LocalPlayer.CustomProperties[Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId)].ToString());
        }

        #endregion

        #region 현재 데이터랑 비교하는 함수

        public bool IsModified(UnimoCombo stagedUnimoCombo)
        {
            return !(customData.CurrentCombo.Equals(stagedUnimoCombo));
        }

        public bool IsModified(string stagedCharId, string stagedEquipId)
        {
            var staged = new UnimoCombo(stagedCharId.Trim(), stagedEquipId.Trim());
            return !customData.CurrentCombo.Equals(staged);
        }

        // 바뀐 항목만 알고 싶으면 flags/diff도 제공
        public (bool charChanged, bool equipChanged) Diff(string stagedCharId, string stagedEquipId)
        {
            var ch = !string.Equals(customData.characterId, stagedCharId.Trim(), StringComparison.Ordinal);
            var eq = !string.Equals(customData.equipId, stagedEquipId.Trim(), StringComparison.Ordinal);

            return (ch, eq);
        }

        #endregion
    }
}