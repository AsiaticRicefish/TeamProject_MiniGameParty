using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Popup_GameEnd : UI_Popup
    {
        [SerializeField] private RectTransform textRect;
        [SerializeField] private TMP_Text text;
        [SerializeField] private int xOffset;
        [SerializeField] private float duration = 0.35f;
        
        private Vector2 originAnchorPos;

        public void SetData(string textData)
        {
            text.text = textData;
        }
        
        
        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            originAnchorPos = textRect.anchoredPosition;
            textRect.anchoredPosition = originAnchorPos + new Vector2(xOffset, 0f);
            
            cg.alpha = 1f;

            var seq = DOTween.Sequence()
                .Append(textRect.DOAnchorPos(originAnchorPos, duration))
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);

            await seq.AsyncWaitForCompletion();
        }
    }
}