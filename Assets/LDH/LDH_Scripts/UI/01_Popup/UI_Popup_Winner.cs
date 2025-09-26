using System;
using System.Threading;
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
        [SerializeField] private GameObject unimoPrefab;
        [SerializeField] private Vector3 unimoSpawnPos = new Vector3(-1000, -1000, -1000);
        [SerializeField] private AvatarStruct avatarStruct;
        
        [Header("UI Component")] 
        [SerializeField] private TMP_Text userNickName;
        
        [Header("Anim")]   
        [SerializeField] private AnimationClip unimoClip;
        [SerializeField] private AnimationClip equipClip;
        
        [Header("Sound")] [SerializeField]
        private Define_LDH.SfxKey winnerSfxKey = Define_LDH.SfxKey.Main_Winner;
        [SerializeField]
        private Define_LDH.SfxKey loserSfxKey = Define_LDH.SfxKey.Main_Loser;
        private Define_LDH.BgmKey winnerBgmKey = Define_LDH.BgmKey.Main_Win_Bgm;
        
        
        private Animator UnimoAnimator => avatarStruct.CurrentCharacter?.GetComponent<Animator>();
        private Animator EquipAnimator => avatarStruct.CurrentEquip?.GetComponent<Animator>();
        
        
        // 애니메이션 재생 관련 변수
        PlayableGraph _unimoGraph;
        AnimationClipPlayable _unimoClipPlayable;
        PlayableGraph _equipGraph;
        AnimationClipPlayable _equipClipPlayable;

        private bool _isWinner;
        
        protected override void Clear()
        {
            base.Clear();
            if(_unimoGraph.IsValid()) _unimoGraph.Destroy();
            if(_equipGraph.IsValid()) _equipGraph.Destroy();
        }

        public async UniTask SetData(string nickname, UnimoCombo combo, bool isWinner)
        {
            userNickName.text = nickname;
            
            // 유니모 프리팹으로 인스턴스 생성
            GameObject unimo = Instantiate(unimoPrefab, unimoSpawnPos, Quaternion.identity);
            avatarStruct = unimo.GetComponentInChildren<AvatarStruct>(true);
            
            Debug.Log($"<color=blue>winner combo : {combo.characterId}, {combo.equipId}</color>");
            await CustomizationManager.Instance.ApplyToAvatarAsync(avatarStruct, combo);
            
            //아바타 애니메이션 설정
            (_unimoGraph, _unimoClipPlayable) = AnimationClipPlayer.Play(unimoClip, UnimoAnimator);
            (_equipGraph, _equipClipPlayable) = AnimationClipPlayer.Play(equipClip, EquipAnimator);

            _isWinner = isWinner;
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
        
        
        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            if (_isWinner)
            {
                Debug.Log("<color=green> winner 효과음 재생합니다.</color>");
                SoundManager.Instance.PlaySFX( winnerSfxKey.ToString());
            }
            else
            {
                Debug.Log("<color=green> loser 효과음 재생합니다.</color>");
                SoundManager.Instance.PlaySFX( loserSfxKey.ToString());
            }
         
            await base.OnShowAsync(ct);
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
            SoundManager.Instance.PlayBGM(winnerBgmKey.ToString());
        }
    }
}