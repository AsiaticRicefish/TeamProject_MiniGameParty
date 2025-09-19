using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타이틀 화면 전체를 터치 감지.
/// - 첫 터치 시 GuestLoginUI를 불러 로그인 선택 팝업을 띄운다.
/// - 이후에는 스스로 비활성화(또는 레이캐스트 차단 해제)하여 버튼 클릭을 방해하지 않는다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ScreenTapCatcher : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("로그인 팝업 UI를 제어하는 GuestLoginUI 참조")]
    [SerializeField] private GuestLoginUI guestLoginUI;

    [Tooltip("첫 터치 후 자신을 완전히 끌지, 레이캐스트만 차단할지 선택")]
    [SerializeField] private bool disableGameObjectAfterTap = true;

    private bool opened = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (opened) return;

        if (guestLoginUI == null)
        {
            Debug.LogError("[ScreenTapCatcher] GuestLoginUI가 할당되지 않았습니다.");
            return;
        }

        // 로그인 팝업 UI 표시
        guestLoginUI.ShowLoginChoice();
        opened = true;
        Debug.Log("[ScreenTapCatcher] 로그인 선택 팝업 표시 (타이틀 위).");

        // 자신 비활성화 or Raycast 해제
        if (disableGameObjectAfterTap)
        {
            gameObject.SetActive(false);
        }
        else
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0; // 완전 투명(선택)
        }
    }
}