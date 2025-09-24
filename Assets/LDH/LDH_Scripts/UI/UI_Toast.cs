using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Toast : UI_Base
    {
        [Header("Animation")] [SerializeField] protected float fadeTime = 0.5f;

        [Header("UI Components")] [SerializeField]
        private Image icon;

        [SerializeField] private Image background;
        [SerializeField] TextMeshProUGUI label;
        [SerializeField] private RectTransform targetRect;


        [Header("Layout")] [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform content;
        [SerializeField] private int offsetX;
        [SerializeField] private float iconWidth = 45f;
        [SerializeField] private float iconHeight = 45f;
        private float maxWidth;


        private float spacing = 35f;

        public RectTransform TargetRect => targetRect;

        protected override void Init()
        {
            interactable = false;
            blocksRaycasts = false;

            maxWidth = root.parent.GetComponent<RectTransform>().rect.size.x - offsetX * 2;

            base.Init();
        }


        public void SetType(Define_LDH.ToastType type)
        {
            ToastStyleTable.ToastStyle style = UIManager.Instance.ToastStyle.GetStyle(type);
            icon.sprite = style.icon;
            background.color = style.backgroundColor;
            label.color = style.textColor;
        }

        public void SetMessage(string msg)
        {
            if (label) label.text = msg;
            Rebuild();
        }


        public void Rebuild()
        {
            // 1) 충분히 넓힌 상태에서 preferred 계산
            float wide = 2000f;
            SetWidth(content, wide);
            SetWidth(label.rectTransform, wide);

            // 레이아웃 강제 갱신
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);


            // 2) 라벨이 필요로 하는 선호 폭
            float labelPref = label.preferredWidth;
            // 아이콘 포함 전체 선호 폭
            float totalPref = iconWidth + spacing + labelPref;
            // 3) 최대폭으로 캡핑
            float finalW = Mathf.Min(totalPref, maxWidth);

            // 4) Label에게 할당 가능한 폭 = 최종폭 - (아이콘+스페이싱)
            float labelAvailWidth = Mathf.Max(0f, finalW - (iconWidth + spacing));
            SetWidth(label.rectTransform, labelAvailWidth);

            // 5) 다시 갱신해서 줄바꿈 반영된 최종 높이 계산
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            // 6) Badge 폭을 최종폭으로 맞춤
            //    (Badge pivot.x=0.5 이므로 좌우로 대칭 증가)
            SetWidth(root, finalW);
        }

        private void SetWidth(RectTransform rt, float w)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        }


        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            if (!cg) return;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(t / fadeTime);
                await UniTask.Yield(ct);
            }

            cg.alpha = 1f;
        }

        protected override async UniTask OnCloseAsync(CancellationToken ct)
        {
            if (!cg) return;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                cg.alpha = 1f - Mathf.Clamp01(t / fadeTime);
                await UniTask.Yield(ct);
            }

            cg.alpha = 0f;
        }
    }
}