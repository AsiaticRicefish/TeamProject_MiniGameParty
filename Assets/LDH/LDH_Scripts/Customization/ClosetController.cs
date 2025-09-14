using System;
using System.Collections.Generic;
using System.Linq;
using Customization;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Customization
{
    public class ClosetController : MonoBehaviour
    {
        [Header("Prefabs & Parents")] [SerializeField]
        private UI_ClosetItemButton charTogglePrefab; // 버튼 프리팹 (비어있는 슬롯용)

        [SerializeField] private UI_ClosetItemButton equipItemTogglePrefab; // 버튼 프리팹 (비어있는 슬롯용)
        [SerializeField] private Transform characterContent; // 캐릭터 ScrollView Content
        [SerializeField] private Transform equipmentContent; // 탈 것 ScrollView Content

        [Header("Canvas Group")] [SerializeField]
        private CanvasGroup characterCanvasGroup; // 미리 빌드 중 숨김/보임 제어

        [SerializeField] private CanvasGroup equipmentCanvasGroup;

        [Header("Toggle Group")] [SerializeField]
        private ToggleGroup characterToggleGroup;

        [SerializeField] private ToggleGroup equipToggleGroup;

        [Header("Control Buttons")] [SerializeField]
        private Button applyButton;

        [SerializeField] private Button resetButton;


        [Header("Avatar")] [SerializeField] private AvatarStruct avatarStruct;


        // 생성된 버튼
        private readonly List<UI_ClosetItemButton> _charToggles = new();
        private readonly List<UI_ClosetItemButton> _equipToggles = new();

        //  로드 핸들 추적
        private readonly List<AsyncOperationHandle<Sprite>> _loadedSpriteHandles = new();

        // flag
        private bool _built;
        private bool _initializing;
        private bool _applying;

        // staged 임시 선택
        private UnimoCombo _stagedCombo;

        private void Awake()
        {
            SetActiveGroup(characterCanvasGroup, false);
            SetActiveGroup(equipmentCanvasGroup, false);

            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(() => ApplyClicked().Forget());

            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ResetClicked);
        }

        private async void Start()
        {
            Debug.Log("[ClosetPrebuilder] Wait until managers are initialized");
            var token = this.GetCancellationTokenOnDestroy();
            await UniTask.WaitUntil(() => CatalogProvider.IsReady, cancellationToken: token);
            await UniTask.WaitUntil(() => Manager.Custom != null && Manager.Custom.IsReady, cancellationToken: token);

            await PrebuildAllAsync();

            Debug.Log("[ClosetPrebuilder] Apply Initial Selection");
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

            Debug.Log("[ClosetPrebuilder] start prebuild");

            // 1) 데이터 가져오기 (CatalogProvider에서 정의 제공)
            var characters = CatalogProvider.CharactersSorted;
            var equips = CatalogProvider.EquipsSorted;
            if (characters == null || equips == null)
            {
                Debug.LogError("[ClosetPrebuilder] CatalogProvider.Characters or CatalogProvider.Equips is null");
                return;
            }


            // 2) 버튼 프리팹들 미리 생성 (동기 Instantiate → 빠르게 끝남)
            BuildToggles(characterContent, _charToggles, characters.Count, charTogglePrefab, characterToggleGroup);
            BuildToggles(equipmentContent, _equipToggles, equips.Count, equipItemTogglePrefab, equipToggleGroup);

            // 3) 아이콘 등 Addressables 리소스를 선로딩
            // 비동기 task를 리스트에 넣어 아래 task가 끝날 때까지 대기
            var charIconTasks = new List<UniTask<Sprite>>();
            var equipIconTasks = new List<UniTask<Sprite>>();


            foreach (var def in characters)
                charIconTasks.Add(LoadIconAsync(def.iconRef));
            foreach (var def in equips)
                equipIconTasks.Add(LoadIconAsync(def.iconRef));

            var charIcons = await UniTask.WhenAll(charIconTasks);
            var equipIcons = await UniTask.WhenAll(equipIconTasks);


            // 4) 로드한 리소스를 적용
            for (int i = 0; i < _charToggles.Count; i++)
            {
                var toggle = _charToggles[i];
                var def = characters[i];
                var icon = charIcons[i];

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
                    }
                );
            }

            for (int i = 0; i < _equipToggles.Count; i++)
            {
                var toggle = _equipToggles[i];
                var def = equips[i];
                var icon = equipIcons[i];

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
                    }
                );
            }

            // 6) 레이아웃 리빌드 후 가시화
            ForceRebuild(characterContent as RectTransform);
            ForceRebuild(equipmentContent as RectTransform);

            SetActiveGroup(characterCanvasGroup, true);
            SetActiveGroup(equipmentCanvasGroup, true);


            Debug.Log("[ClosetPrebuilder] prebuild complete");
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
            Debug.Log($"======= ChangeCharacter 시작 =======");

            Debug.Log($"[Closet] Change Character → {id}");

            //입력 막기
 
            await Manager.Custom.ApplyToAvatarAsync(avatarStruct, characterId: id);
            Debug.Log("Apply가 완료되었습니다.");

            Debug.Log($"======= ChangeCharacter 끝 =======");
        }

        private async UniTask ChangeEquip(string id)
        {
            Debug.Log($"======= ChangeEquip 시작 =======");

            Debug.Log($"[Closet] Change Equip → {id}");

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
                Debug.LogError("[ClosetPreBuilder] apply initial selection error");
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

            bool modified = Manager.Custom.IsModified(_stagedCombo);
            bool valid = !string.IsNullOrEmpty(_stagedCombo.characterId) && !string.IsNullOrEmpty(_stagedCombo.equipId);

            applyButton.interactable = !_applying && modified && valid;
        }

        private async UniTaskVoid ApplyClicked()
        {
            if (!_stagedCombo.characterId?.Any() ?? true) return;
            if (!_stagedCombo.equipId?.Any() ?? true) return;
            if (_applying) return;

            Debug.Log("ApplyClicked && 현재 장착 상태 적용가능");
            _applying = true;
            UpdateApplyButton();

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
        }

        public void ResetClicked()
        {
            _stagedCombo = Manager.Custom.GetEquippedLocal();
            ApplyInitialSelection(_stagedCombo);
            UpdateApplyButton();
        }

        #endregion
    }
}