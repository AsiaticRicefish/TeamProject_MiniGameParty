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
        

        public void BindCharacter(GameObject go, string id, bool inheritLayer = true)
        {
            if (inheritLayer)
                SetLayerRecursively(go, characterRoot.gameObject.layer);
            
            CurrentCharacter = go;
            CurrentCharacterId = id;
            // var t = go.transform; 
            // t.localPosition = Vector3.zero; 
            // t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        }
        
        public void BindEquip(GameObject go, string id, bool inheritLayer = true)
        {
            if (inheritLayer)
                SetLayerRecursively(go, characterRoot.gameObject.layer);
            
            CurrentEquip = go; CurrentEquipId = id;
            
            // var t = go.transform; t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        }
        
        
        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (!go) return;
            go.layer = layer;

            // Renderer/SkinnedMeshRenderer 등 자식 포함 전부 동일 레이어로
            var trs = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                if (trs[i]) trs[i].gameObject.layer = layer;
            }
        }

    }
}