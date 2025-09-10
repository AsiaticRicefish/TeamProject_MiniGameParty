using Unity.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Customization
{
    [CreateAssetMenu(menuName = "Customization/Equip", order = 0)]
    public class EquipmentDefinition : ScriptableObject
    {
        [Min(0)] public int number;
        [SerializeField, ReadOnly]  public string id;
        [SerializeField, ReadOnly] public string setKey;
        public string Id => id;// unique id
        public string SetKey => setKey;          // 캐릭터-탈것 세트 매칭용

        [Header("Prefab")] public AssetReferenceGameObject prefabRef; // characterRoot 아래에 붙일 프리팹

        [Header("Visuals")] public AssetReferenceSprite iconRef; // 상점 카드용 아이콘

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(number.ToString()))
            {
                id = $"unimo_equip_{number:D3}";
                setKey = $"unimo_{number:D3}";
            }
            else
            {
                id = "";
                setKey = "";
            }
        }
#endif
    }
}