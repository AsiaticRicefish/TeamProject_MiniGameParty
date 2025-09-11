using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Customization
{
    public class AvatarStruct : MonoBehaviour
    {
        public Transform characterRoot;
        public Transform equipRoot;

        public GameObject CurrentCharacter { get; private set; }
        public string     CurrentCharacterId { get; private set; }
        public GameObject CurrentEquip { get; private set; }
        public string     CurrentEquipId { get; private set; }


        private void Start()
        {
            Init();
        }

        public void Init()
        {
            CustomizationManager.Instance.ApplyToAvatarAsync(this, CustomizationManager.Instance.GetEquippedLocal())
                .Forget();
        }


        public void BindCharacter(GameObject go, string id)
        {
            CurrentCharacter = go;
            CurrentCharacterId = id;
            var t = go.transform; t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
            
            
        }
        
        public void BindEquip(GameObject go, string id)
        {
            CurrentEquip = go; CurrentEquipId = id;
            // 캐릭터 루트를 시트에 장착(탈것 있을 때)
            
            var t = go.transform; t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        }
        

    }
}