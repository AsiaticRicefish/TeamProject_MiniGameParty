using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Customization
{
    public class AvatarStruct : MonoBehaviour
    {
        [Header("Parent Transforms for Character & Engine Prefabs")]
        public Transform characterRoot;
        public Transform equipRoot;
        
        // 현재 장착한 캐릭터/엔진 정보
        public GameObject CurrentCharacter { get; private set; }
        public string     CurrentCharacterId { get; private set; }
        public GameObject CurrentEquip { get; private set; }
        public string     CurrentEquipId { get; private set; }
        
        
        [Header ("엔진 animation hash")]
        private  readonly int Equip_BaseLayer_IdleState = Animator.StringToHash("Base Layer.anim_EQ000_Idle");
        private  readonly int Equip_Altitude_IdleState = Animator.StringToHash("Altitude.anim_EQ000_Idle_Altitude");

        public async UniTask BindCharacter(GameObject go, string id, bool inheritLayer = true)
        {
            if (inheritLayer)
                SetLayerRecursively(go, characterRoot.gameObject.layer);
            
            CurrentCharacter = go;
            CurrentCharacterId = id;

            await UniTask.Yield();

        }
        
        public async UniTask BindEquip(GameObject go, string id, bool inheritLayer = true)
        {
            if (inheritLayer)
                SetLayerRecursively(go, characterRoot.gameObject.layer);
            CurrentEquip = go; 
            CurrentEquipId = id;

            Animator animator = CurrentEquip.GetComponent<Animator>();
            await ForceAnimStateAsync(animator,
                new[]
                {
                    (0, Equip_BaseLayer_IdleState),
                    (1, Equip_Attitude_IdleState: Equip_Altitude_IdleState),
                });
            await UniTask.Yield();
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

        private async UniTask  ForceAnimStateAsync(
            Animator anim,
            (int layer, int hash)[] targets, 
            float normalizedTime = 0f)
        {
            if (!anim || !anim.runtimeAnimatorController) return;
            if (targets == null || targets.Length == 0) return;
            
            bool prevEnabled = anim.enabled;
            float prevSpeed  = anim.speed;
            
            anim.enabled = false;
            // 초기화 보장
            anim.Rebind();
            anim.Update(0f);
       
            // 각 레이어에 상태 강제 스냅
            anim.speed = 0f; // 트랜지션/시간 흐름 차단
            foreach ((int layer, int hash) in targets)
            {
                if (layer < 0 || layer >= anim.layerCount)
                {
                    Debug.LogWarning($"[AnimatorUtil] 잘못된 레이어 인덱스: {layer}");
                    continue;
                }
                if (!anim.HasState(layer, hash))
                {
                    Debug.LogWarning($"[AnimatorUtil] State 없음. layer={layer}, hash={hash}");
                    continue;
                }
                anim.Play(hash, layer, normalizedTime);

            }
            anim.Update(0f);            // 바로 포즈 적용
            anim.speed = prevSpeed;
            anim.enabled = prevEnabled;
            
        }
        

    }
}