using System;
using Customization;
using Cysharp.Threading.Tasks;
using LDH_Util;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LDH_UI
{
    public class UI_Popup_Winner : UI_Popup
    {
        [Header("Unimo Avatar")]
        [SerializeField] private AvatarStruct avatarStruct;
        
        [Header("UI Component")] 
        [SerializeField] private TMP_Text userNickName;
        
        [Header("Anim")]   
        [SerializeField] private AnimationClip unimoClip;
        [SerializeField] private AnimationClip equipClip;
        
        
        private Animator UnimoAnimator => avatarStruct.CurrentCharacter?.GetComponent<Animator>();
        private Animator EquipAnimator => avatarStruct.CurrentEquip?.GetComponent<Animator>();
        
        
        // 애니메이션 재생 관련 변수
        PlayableGraph _unimoGraph;
        AnimationClipPlayable _unimoClipPlayable;
        PlayableGraph _equipGraph;
        AnimationClipPlayable _equipClipPlayable;
        
        protected override void Clear()
        {
            base.Clear();
            if(_unimoGraph.IsValid()) _unimoGraph.Destroy();
            if(_equipGraph.IsValid()) _equipGraph.Destroy();
        }

        public async UniTask SetData(string nickname, UnimoCombo combo)
        {
            userNickName.text = nickname;
            await CustomizationManager.Instance.ApplyToAvatarAsync(avatarStruct, combo);
            
            //아바타 애니메이션 설정
            (_unimoGraph, _unimoClipPlayable) = AnimationClipPlayer.Play(unimoClip, UnimoAnimator);
            (_equipGraph, _equipClipPlayable) = AnimationClipPlayer.Play(equipClip, EquipAnimator);
        }

        private void Update()
        {
            if (!_unimoGraph.IsValid() || !unimoClip || !_equipGraph.IsValid() || !equipClip) return;
            
            _unimoClipPlayable.SetSpeed(1f);
            _equipClipPlayable.SetSpeed(1f);
            
            // 루프: 시간을 직접 모듈러
            double u_t = _unimoClipPlayable.GetTime();
            double e_t = _equipClipPlayable.GetTime();
            double u_len = Mathf.Max(unimoClip.length, 0.0001f); // 0 보호
            double e_len = Mathf.Max(equipClip.length, 0.0001f);
            
            //GameTime을 기준으로 playable time은 자동 증가
            //length 보다 커진 경우 되감아서 플레이 시킨다.
            if (u_t > u_len)
            {
                double u_wrapped = u_t % u_len;
                _unimoClipPlayable.SetTime(u_wrapped);
            }

            if (e_t > e_len)
            {
                double e_wrapped = e_t % e_len;
                _equipClipPlayable.SetTime(e_wrapped);
            }
        }
    }
}