using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Data;
using LDH_UI;
using LDH_Util;
using Managers;
using Store;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using static LDH_Util.Define_LDH;


namespace Customization
{
    public class ClosetController : MonoBehaviour
    {
        [Header("Prefabs & Parents")]  //--------------------------------------//
        [SerializeField] private UI_ClosetItemButton charTogglePrefab; // 버튼 프리팹 (비어있는 슬롯용)
        [SerializeField] private UI_ClosetItemButton equipItemTogglePrefab; // 버튼 프리팹 (비어있는 슬롯용)
        [SerializeField] private Transform characterContent; // 캐릭터 ScrollView Content
        [SerializeField] private Transform equipmentContent; // 탈 것 ScrollView Content
        
        [Header("Canvas Group")]  //--------------------------------------//
        [SerializeField] private CanvasGroup characterCanvasGroup; // 미리 빌드 중 숨김/보임 제어
        [SerializeField] private CanvasGroup equipmentCanvasGroup;

        [Header("Toggle Group")]  //--------------------------------------//
        [SerializeField] private ToggleGroup characterToggleGroup;
        [SerializeField] private ToggleGroup equipToggleGroup;

        [Header("Control Buttons")]  //--------------------------------------//
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button purchaseButton;
        
        [Header("Avatar")]  //--------------------------------------//
        [SerializeField] private AvatarStruct avatarStruct;

        [Header("Setting")] [SerializeField] private bool includeOwnedEvenIfDisabled = true;

        
        
        // DataManager --------------------------------------//
        private DataManager Data => Manager.Data;
        
        
        // 생성된 버튼 --------------------------------------//
        private readonly List<UI_ClosetItemButton> _charToggles = new();
        private readonly List<UI_ClosetItemButton> _equipToggles = new();
        private readonly Dictionary<string, UI_ClosetItemButton> _charBtnById = new();
        private readonly Dictionary<string, UI_ClosetItemButton> _equipBtnById = new();
        
        //  로드 핸들 추적 --------------------------------------//
        private readonly List<AsyncOperationHandle<Sprite>> _loadedSpriteHandles = new();

        // flag --------------------------------------//
        private bool _built;
        private bool _initializing;
        private bool _applying;

        // staged 임시 선택 --------------------------------------//
        private UnimoCombo _stagedCombo;

        private void Awake()
        {
            SetActiveGroup(characterCanvasGroup, false);
            SetActiveGroup(equipmentCanvasGroup, false);

            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(ApplyClicked);

            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ResetClicked);
            
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(PurchaseClicked);
        }

        private async void Start()
        {
//            Debug.Log("[ClosetController] Wait until managers are initialized");
            var token = this.GetCancellationTokenOnDestroy();
            await UniTask.WaitUntil(() => CatalogProvider.IsReady, cancellationToken: token);
            await UniTask.WaitUntil(() => Manager.Custom != null && Manager.Custom.IsReady, cancellationToken: token);

            await PrebuildAllAsync();

//            Debug.Log("[ClosetController] Apply Initial Selection");
            _stagedCombo = Manager.Custom.GetEquippedLocal();
            ApplyInitialSelection(_stagedCombo);
        }

        private void OnDestroy()
        {
            // 아이콘처럼 LoadAssetAsync로 로드한 핸들 해제
            foreach (var h in _loadedSpriteHandles)
                if (h.IsValid())
                    Addressables.Release(h);
            _loadedSpriteHandles.Clear();
        }

        /// <summary>
        /// 씬 진입 시 한 번만 미리 생성 + 아이콘 선로딩.
        /// </summary>
        public async UniTask PrebuildAllAsync()
        {
            if (_built) return;
            _built = true;

//            Debug.Log("[ClosetController] start prebuild");

            // 1) 정의(Definition) 목록
            var allCharacters = CatalogProvider.CharactersSorted;
            var allEquips     = CatalogProvider.EquipsSorted;
            if (allCharacters == null || allEquips == null)
            {
                Debug.LogError("[ClosetController] CatalogProvider.Characters or Equips is null");
                return;
            }
            
            // 2) Firestore ItemData의 Enabled 기준으로 허용 id 집합 만들기
            //    - 없으면(미등록) 비활성으로 간주
            //    - 필요시 '보유중이면 비활성이어도 보이게' 옵션에 따라 설정
            var enabledCharIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in Data.CharacterItemDict)
            {
                if (kv.Value?.Enabled == true) enabledCharIds.Add(kv.Key);
            }
            if (includeOwnedEvenIfDisabled && Data.Custom.ownedCharacters !=null)
                foreach (var id in  Data.Custom.ownedCharacters) enabledCharIds.Add(id);
            
            var enabledEquipIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in Data.EquipItemDict)
            {
                if (kv.Value?.Enabled == true) enabledEquipIds.Add(kv.Key);
            }
            if (includeOwnedEvenIfDisabled && Data.Custom.ownedEquips !=null)
                foreach (var id in Data.Custom.ownedEquips ) enabledEquipIds.Add(id);
            
            // 3) 실제 생성에 쓸 리스트는 Enabled만 통과
            var characters = allCharacters.Where(def => enabledCharIds.Contains(def.id)).ToList();
            var equips     = allEquips.Where(def => enabledEquipIds.Contains(def.id)).ToList();

            

            // 4) 버튼 프리팹들 미리 생성 (동기 Instantiate → 빠르게 끝남)
            BuildToggles(characterContent, _charToggles, characters.Count, charTogglePrefab, characterToggleGroup);
            BuildToggles(equipmentContent, _equipToggles, equips.Count, equipItemTogglePrefab, equipToggleGroup);

            // 5) 아이콘 등 Addressables 리소스를 선로딩
            // 비동기 task를 리스트에 넣어 아래 task가 끝날 때까지 대기
            var charIconTasks = new List<UniTask<Sprite>>();
            var equipIconTasks = new List<UniTask<Sprite>>();


            foreach (var def in characters)
                charIconTasks.Add(LoadIconAsync(def.iconRef));
            foreach (var def in equips)
                equipIconTasks.Add(LoadIconAsync(def.iconRef));

            var charIcons = await UniTask.WhenAll(charIconTasks);
            var equipIcons = await UniTask.WhenAll(equipIconTasks);


            // 6) 로드한 리소스를 적용
            for (int i = 0; i < _charToggles.Count; i++)
            {
                var toggle = _charToggles[i];
                var def = characters[i];
                var icon = charIcons[i];

                //딕셔너리에 등록
                _charBtnById.TryAdd(def.id, toggle);
                
                toggle.SetOwned(Data.HasCharacter(def.id));

                toggle.Bind(
                    id: def.id,
                    displayName: def.id,
                    icon: icon,
                    onToggle: (id, isOn) =>
                    {
                        if (!isOn) return;
                        _stagedCombo.characterId = id;
                        ChangeCharacter(id).Forget();
                        UpdateApplyButton();
                        UpdatePurchaseButton();
                    });
            }

            for (int i = 0; i < _equipToggles.Count; i++)
            {
                var toggle = _equipToggles[i];
                var def = equips[i];
                var icon = equipIcons[i];
               
                //딕셔너리에 등록
                _equipBtnById.TryAdd(def.id, toggle);
                toggle.SetOwned(Data.HasEquip(def.id));

                toggle.Bind(
                    id: def.id,
                    displayName: def.id,
                    icon: icon,
                    onToggle: (id, isOn) =>
                    {
                        if (!isOn) return;
                        _stagedCombo.equipId = id;
                        ChangeEquip(id).Forget();
                        UpdateApplyButton();
                        UpdatePurchaseButton();
                    });
            }

            // 7) 레이아웃 리빌드 후 가시화
            ForceRebuild(characterContent as RectTransform);
            ForceRebuild(equipmentContent as RectTransform);

            SetActiveGroup(characterCanvasGroup, true);
            SetActiveGroup(equipmentCanvasGroup, true);


//            Debug.Log("[ClosetController] prebuild complete");
        }

        #region Toggle Build

        private void BuildToggles(Transform content, List<UI_ClosetItemButton> store, int count,
            UI_ClosetItemButton togglePrefab, ToggleGroup group)
        {
            // 기존 것 있으면 정리
            for (int i = store.Count - 1; i >= 0; i--)
            {
                if (store[i]) Destroy(store[i].gameObject);
            }

            store.Clear();

            // 버튼 생성
            for (int i = 0; i < count; i++)
            {
                var item = Instantiate(togglePrefab, content);
                item.transform.SetAsLastSibling();
                item.Init();

                var t = item.GetToggle();
                if (t && group) t.group = group;

                store.Add(item);
            }
        }

        private async UniTask<Sprite> LoadIconAsync(AssetReferenceSprite iconRef)
        {
            if (iconRef == null || !iconRef.RuntimeKeyIsValid())
                return null;

            var handle = iconRef.LoadAssetAsync<Sprite>();
            _loadedSpriteHandles.Add(handle);

            var iconSprite = await handle.Task;
            return iconSprite;
        }

        private static void SetActiveGroup(CanvasGroup cg, bool active)
        {
            if (!cg) return;
            cg.alpha = active ? 1f : 0f;
            cg.interactable = active;
            cg.blocksRaycasts = active;
        }

        private void ForceRebuild(RectTransform rt)
        {
            if (!rt) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            // 부모 체인 리빌드(그리드/컨텐츠/스크롤뷰)
            var parent = rt.parent as RectTransform;
            if (parent) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }

        #endregion

        #region 토글 변경 로직

        private async UniTask ChangeCharacter(string id)
        {
//            Debug.Log($"======= ChangeCharacter 시작 =======");

//            Debug.Log($"[Closet] Change Character → {id}");

            //입력 막기

            await Manager.Custom.ApplyToAvatarAsync(avatarStruct, characterId: id);
            Debug.Log("Apply가 완료되었습니다.");

            Debug.Log($"======= ChangeCharacter 끝 =======");
        }

        private async UniTask ChangeEquip(string id)
        {
//            Debug.Log($"======= ChangeEquip 시작 =======");

//            Debug.Log($"[Closet] Change Equip → {id}");

            await Manager.Custom.ApplyToAvatarAsync(avatarStruct, equipId: id);
            Debug.Log("Apply가 완료되었습니다.");


            Debug.Log($"======= ChangeEquip 끝 =======");
        }

        private void ApplyInitialSelection(UnimoCombo combo)
        {
            if (_initializing) return;
            _initializing = true;

            string charId = combo.characterId;
            string equipId = combo.equipId;

            var charToggle = _charToggles.FirstOrDefault(b => b.Id == charId)?.GetToggle();
            var equipToggle = _equipToggles.FirstOrDefault(b => b.Id == equipId)?.GetToggle();

            if (charToggle == null || equipToggle == null)
            {
                Debug.LogError("[ClosetController] apply initial selection error");
                return;
            }

            // 리스너는 이미 연결되어 있다고 가정: isOn = true → onValueChanged( true ) 한 번 호출
            if (charToggle) charToggle.isOn = true; // false 이벤트는 Bind쪽에서 무시(isOn 체크)
            if (equipToggle) equipToggle.isOn = true;

            _initializing = false;
        }
        

        #endregion

        #region Control

        private void UpdateApplyButton()
        {
            if (_stagedCombo.characterId == null || _stagedCombo.equipId == null) return;
            
            bool valid = Data.HasCharacter(_stagedCombo.characterId) && Data.HasEquip(_stagedCombo.equipId);
            bool modified = Manager.Custom.IsModified(_stagedCombo);
            applyButton.interactable = !_applying && modified && valid;
        }

        private void UpdatePurchaseButton()
        {
            if (_stagedCombo.characterId == null || _stagedCombo.equipId == null) return;
            
            bool owned = Data.HasCharacter(_stagedCombo.characterId) && Data.HasEquip(_stagedCombo.equipId);

            purchaseButton.interactable = !owned;

        }

        
        // ===== 구매 팝업 호출 =====
        private async void PurchaseClicked()
        {
            //보유하지 않은 아이템인 경우 가격 조회
            List<PurchaseLine> purchaseLines = new();

            if (!Data.HasCharacter(_stagedCombo.characterId))
                purchaseLines.Add(new PurchaseLine(new ItemUnit(ItemKind.Character, _stagedCombo.characterId), 1));
            if(!Data.HasEquip(_stagedCombo.equipId))
                purchaseLines.Add(new PurchaseLine(new ItemUnit(ItemKind.Equip, _stagedCombo.equipId), 1));

            try
            {
                var purchaseQuote = await Manager.Purchase.QuoteAsync(purchaseLines);
                var popup = Manager.UI.CreatePopupUI<UI_Popup_ItemPurchase>();
                popup.SetData(purchaseQuote);
                popup.OnPurchaseSuccess = async (grantedItems) =>
                {
                    var tasks = new List<UniTask>();
                    foreach (var item in grantedItems)
                    {
                        if (item.Kind == ItemKind.Character)
                            tasks.Add(ChangeCharacter(item.Id));
                            
                           
                        else if (item.Kind == ItemKind.Equip)
                            tasks.Add(ChangeEquip(item.Id));
                        else
                            continue;
                    }
                    
                    await UniTask.WhenAll(tasks);
                    MarkOwned(grantedItems);
                };
                
                Manager.UI.ShowPopupUI(popup).Forget();
            }
            catch(Exception e)
            {
                Debug.LogException(e);
                Manager.UI.EnqueueToast("가격 계산 중 오류가 발생했습니다. 나중에 시도해주세요.");
            }
        }
        private async void ApplyClicked()
        {
            if (!_stagedCombo.characterId?.Any() ?? true) return;
            if (!_stagedCombo.equipId?.Any() ?? true) return;
            if (_applying) return;

            Debug.Log("ApplyClicked && 현재 장착 상태 적용가능");
            _applying = true;

            // 실제 저장 API 호출
            bool ok = await Manager.Custom.UpdateComboAsync(_stagedCombo);
            
            if (ok)
            {
                Debug.Log($"[Closet] Success saving staged combo");
                Manager.UI.EnqueueToast("적용되었습니다.");
            }
            else
            {
                Debug.LogWarning("[Closet] Save failed. Keeping staged preview but not updating saved.");
                Manager.UI.EnqueueToast("적용에 실패했습니다.");
            }

            _applying = false;
            UpdateApplyButton();
            UpdatePurchaseButton();
        }

        public void ResetClicked()
        {
            _stagedCombo = Manager.Custom.GetEquippedLocal();
            ApplyInitialSelection(_stagedCombo);
            UpdateApplyButton();
            UpdatePurchaseButton();
        }

        #endregion
        
        
        private void MarkOwned(IEnumerable<ItemUnit> items)
        {
            foreach (var it in items)
            {
                if (it.Kind == ItemKind.Character && _charBtnById.TryGetValue(it.Id, out var cbtn))
                    cbtn.SetOwned(true);
                else if (it.Kind == ItemKind.Equip && _equipBtnById.TryGetValue(it.Id, out var ebtn))
                    ebtn.SetOwned(true);
            }

            // 버튼 상태 갱신
            UpdateApplyButton();
            UpdatePurchaseButton();
        }
        
    }
}