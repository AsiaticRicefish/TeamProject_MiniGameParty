using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타이틀 화면 전체를 터치 감지.
/// - 첫 터치 시 GuestLoginUI를 불러 로그인 선택 팝업을 띄운다.
/// - 이후에는 스스로 비활성화(또는 레이캐스트 차단 해제)하여 버튼 클릭을 방해하지 않는다.
/// </summary>

namespace KYG
{
    
[RequireComponent(typeof(CanvasRenderer))]
public class ScreenTapCatcher : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("로그인 팝업 UI를 제어하는 GuestLoginUI 참조")]
    [SerializeField] private GuestLoginUI guestLoginUI;
    
    [Header("SFX Settings")]
    [Tooltip("탭할 때 재생할 SFX의 SoundData.soundName (SoundCollection에 등록된 이름)")]
    [SerializeField] private string tapSfxName = "Click"; // ← SoundCollection의 SFX 'Sound Name'과 동일해야 함

    [Tooltip("첫 터치 후 자신을 완전히 끌지, 레이캐스트만 차단할지 선택")]
    [SerializeField] private bool disableGameObjectAfterTap = true;

    private bool opened = false; // 이미 처리했는지 중복 클릭 방지 플래그
    
    
    public void ResetForNextTap()
    {
        // 다음 탭을 받을 수 있게 플래그 초기화
        opened = false;

        // 혹시 disableGameObjectAfterTap=false 를 쓰는 경우
        // OnPointerDown에서 blocksRaycasts=false/alpha=0 으로 바꿨을 수 있으니 되돌려줌
        var cg = GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.blocksRaycasts = true;
            cg.interactable = true;
            // 투명으로 썼었다면 다시 보이게 (필요 없으면 주석)
            cg.alpha = 1f;
        }

        // 안전하게 자기 자신도 켠다 (비활성로 껐다면)
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }
    
    /// <summary>
    /// 전체 화면을 탭하면 EventSystem이 호출해주는 콜백
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (opened) return;

        // 1) 탭 SFX 재생
        //    - SoundManager 싱글톤이 살아있고, tapSfxName이 SoundCollection에 등록되어 있어야 소리가 납니다.
        if (SoundManager.Instance != null && !string.IsNullOrEmpty(tapSfxName))
        {
            SoundManager.Instance.PlaySFX(tapSfxName);
        }
        else
        {
            Debug.LogWarning("[ScreenTapCatcher] SoundManager 또는 SFX 이름이 비어있습니다.");
        }

        // 2) 로그인 팝업 UI 열기
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
            // Raycast 차단만 해제 (아래 UI 클릭 가능)
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0; // 완전 투명(선택)
        }
    }
}
}
