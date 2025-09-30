using UnityEngine;
using UnityEngine.EventSystems;

namespace KYG
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class ScreenTapCatcher : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private GuestLoginUI guestLoginUI;
        [SerializeField] private string tapSfxName = "Click";
        [SerializeField] private bool disableGameObjectAfterTap = true;

        private bool opened = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (opened) return;

            // (선택) 탭 효과음
            if (SoundManager.Instance && !string.IsNullOrEmpty(tapSfxName))
                SoundManager.Instance.PlaySFX(tapSfxName);

            // ✅ 첫 사용자 탭을 알리며 팝업 열기
            if (guestLoginUI)
                guestLoginUI.NotifyFirstTapAndOpen();
            else
                Debug.LogError("[ScreenTapCatcher] GuestLoginUI가 비어있습니다.");

            opened = true;

            // 이후엔 자신을 비활성화(또는 Raycast만 해제)
            if (disableGameObjectAfterTap) gameObject.SetActive(false);
            else
            {
                var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false; cg.interactable = false; cg.alpha = 0f;
            }
        }

        // 필요 시 팝업에서 돌아올 때 다시 켜주는 용도
        public void ResetForNextTap()
        {
            opened = false;
            var cg = GetComponent<CanvasGroup>();
            if (cg) { cg.blocksRaycasts = true; cg.interactable = true; cg.alpha = 1f; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }
    }
}