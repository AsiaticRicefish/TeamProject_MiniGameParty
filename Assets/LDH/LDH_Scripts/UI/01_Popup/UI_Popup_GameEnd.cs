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


        [Header("Sound")] [SerializeField] private SfX_Game miniGameEndSfx = SfX_Game.SFX_MiniGameEnd;
        [SerializeField] private SfX_Game matchEndSfx = SfX_Game.SFX_MatchEnd;
        
        private Vector2 originAnchorPos;
        private SfX_Game currentSfxType;

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
               
            SoundManager.Instance.PlaySFX_GAME(currentSfxType);
            
            cg.alpha = 1f;

            var seq = DOTween.Sequence()
                .Append(textRect.DOAnchorPos(originAnchorPos, duration))
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);

            await seq.AsyncWaitForCompletion();
        }
    }
}