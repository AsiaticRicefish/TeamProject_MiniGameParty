using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LDH_Util;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Popup_GameEnd : UI_Popup
    {
        [Header("UI")]
        [SerializeField] private RectTransform textRect;
        [SerializeField] private TMP_Text text;
        [SerializeField] private int xOffset;
        [SerializeField] private float duration = 0.35f;

        [Header("Text")] [SerializeField] private string miniGameEnd = "게임 종료";
        [SerializeField] private string matchEnd = "매치 종료";
        
        
        [Header("Sound")] 
        [SerializeField] private Define_LDH.SfxKey miniGameEndSfx = Define_LDH.SfxKey.Main_MiniGameEnd;
        [SerializeField] private Define_LDH.SfxKey matchEndSfx = Define_LDH.SfxKey.Main_MatchEnd;
        
        private Vector2 originAnchorPos;
        private Define_LDH.SfxKey currentSfxType;

        public void SetMiniGameEnd()
        {
            text.text = miniGameEnd;
            currentSfxType = miniGameEndSfx;
        }
        
        public void SetMatchEnd()
        {
            text.text = matchEnd;
            currentSfxType = matchEndSfx;
        }
        
        
        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            originAnchorPos = textRect.anchoredPosition;
            textRect.anchoredPosition = originAnchorPos + new Vector2(xOffset, 0f);
               
            SoundManager.Instance.PlaySFX(currentSfxType.ToString());
            
            cg.alpha = 1f;

            var seq = DOTween.Sequence()
                .Append(textRect.DOAnchorPos(originAnchorPos, duration))
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);

            await seq.AsyncWaitForCompletion();
        }
    }
}