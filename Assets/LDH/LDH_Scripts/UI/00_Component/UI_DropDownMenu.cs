using Cysharp.Threading.Tasks;
using DG.Tweening;
using Managers;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_DropDownMenu : MonoBehaviour
    {
        [Header("drop down toggle")] 
        [SerializeField] private Toggle dropDownToggle;

        
        [Header("drop down menus")]
        [SerializeField] private CanvasGroup cg;
        [SerializeField] private Transform menuParent;
        [SerializeField] private Image menuParentImage;
        [SerializeField] private float duration = 0.35f;
        
        
        public async UniTask ShowMenu()
        {
            int n = menuParent.childCount;
            // 입력 잠금 + 배경 on
            cg.interactable = false;
            cg.blocksRaycasts = false;
            if (menuParentImage) menuParentImage.enabled = true;

            if (n <= 0)
            {
                // 항목이 없어도 배경만 켜고 바로 입력 복구
                cg.interactable = true;
                cg.blocksRaycasts = true;
                return;
            }
            // 모두 꺼진 상태에서 시작
            for (int i = 0; i < n; i++)
                menuParent.GetChild(i).gameObject.SetActive(false);

            float step = (n > 1) ? (duration / (n - 1)) : 0f;

            var seq = DOTween.Sequence();
            for (int i = 0; i < n; i++)
            {
                int idx = i; // 클로저 캡쳐 주의
                seq.InsertCallback(i * step, () =>
                {
                    menuParent.GetChild(idx).gameObject.SetActive(true);
                });
            }

            // 마지막에 입력 복구
            seq.InsertCallback(duration, () =>
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
            });

            await seq.AsyncWaitForCompletion();
        

        }

        public async UniTask HideMenu()
        {
            int n = menuParent.childCount;

            // 입력 잠금 (닫히는 동안 클릭 차단)
            cg.interactable = false;
            cg.blocksRaycasts = false;

            if (n <= 0)
            {
                if (menuParentImage) menuParentImage.enabled = false;
                return;
            }

            float step = (n > 1) ? (duration / (n - 1)) : 0f;

            var seq = DOTween.Sequence();
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                seq.InsertCallback(i * step, () =>
                {
                    menuParent.GetChild(idx).gameObject.SetActive(false);
                });
            }

            // 끝에서 배경 off
            seq.InsertCallback(duration, () =>
            {
                if (menuParentImage) menuParentImage.enabled = false;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            });

            await seq.AsyncWaitForCompletion();
        }
        
        public void ShowPopupUI(UI_Popup uiPopup)
        {
            //드롭다운 토글 닫기
            dropDownToggle.isOn = !dropDownToggle.isOn;
            
            var popup = Manager.UI.CreatePopupUI<UI_Popup>(uiPopup.name);
            Manager.UI.ShowPopupUI(popup).Forget();
        }
    }
}