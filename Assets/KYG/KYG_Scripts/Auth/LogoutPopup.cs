using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// [로그아웃 위젯]
/// - "로그아웃" 버튼 → 확인창 → 실제 로그아웃(게스트/GPGS 공통) → 타이틀/로그인 복귀
/// - AuthAccount.SignOutAllAsync()를 호출하여:
///     * Photon 네트워크 분리
///     * Firebase SignOut
///     * (안드로이드) GPGS SignOut
///     * 로컬 저장(자동로그인 정보) 삭제
///     * (옵션) 게스트 닉네임 예약 해제
/// - 진행 중에는 입력 차단 오버레이 표시
/// </summary>
public class LogoutPopup : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("메인 '로그아웃' 버튼(필수)")]
    [SerializeField] private Button buttonLogout;

    [Tooltip("확인창 오버레이 패널(비활성 시작)")]
    [SerializeField] private GameObject panelConfirm;

    [Tooltip("진행중 오버레이(비활성 시작)")]
    [SerializeField] private GameObject panelProgress;

    [Tooltip("진행중 메시지 라벨 (UnityEngine.UI.Text) - 없으면 비워두세요")]
    [SerializeField] private Text progressText;

#if TMP_PRESENT
    // 프로젝트에 TMP가 있다면, 위 Text 대신 아래 필드를 써도 됩니다(인스펙터에서 하나만 연결).
    [SerializeField] private TMPro.TextMeshProUGUI progressTMP;
#endif

    [Header("Behavior")]
    [Tooltip("게스트 계정 로그아웃 시 닉네임 예약을 해제할지 여부(보통 TRUE 권장)")]
    [SerializeField] private bool releaseGuestNickname = true;

    [Tooltip("로그아웃 후 이동할 씬 이름(비우면 현재 씬에서 로그인 UI를 띄움)")]
    [SerializeField] private string titleSceneName = "Login Scene"; // 프로젝트에 맞게 변경!

    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool verbose = true;

    private void Reset()
    {
        // 에디터에서 컴포넌트를 붙일 때 자동으로 하위 오브젝트를 찾도록 배려 (선택사항)
        if (!buttonLogout)   buttonLogout = transform.Find("Button_Logout")?.GetComponent<Button>();
        if (!panelConfirm)   panelConfirm = transform.Find("Panel_Confirm")?.gameObject;
        if (!panelProgress)  panelProgress = transform.Find("Panel_Progress")?.gameObject;
    }

    private void Awake()
    {
        // 안전 장치: 누락된 참조가 있더라도 런타임에서 NRE가 나지 않도록 초기화
        if (panelConfirm)  panelConfirm.SetActive(false);
        if (panelProgress) panelProgress.SetActive(false);

        // 버튼 클릭 연결
        if (buttonLogout)
            buttonLogout.onClick.AddListener(OnClickOpenConfirm);
    }

    // === UI 핸들러 ===

    /// <summary>메인 로그아웃 버튼 클릭 → 확인창 표시</summary>
    public void OnClickOpenConfirm()
    {
        if (verbose) Debug.Log("[LogoutPopup] Open Confirm");
        if (panelConfirm) panelConfirm.SetActive(true);
    }

    /// <summary>확인창: 취소</summary>
    public void OnClickCancel()
    {
        if (verbose) Debug.Log("[LogoutPopup] Cancel");
        if (panelConfirm) panelConfirm.SetActive(false);
    }

    /// <summary>확인창: 로그아웃 실행</summary>
    public void OnClickConfirm()
    {
        if (verbose) Debug.Log("[LogoutPopup] Confirm → SignOut");
        _ = SignOutFlowAsync(); // fire & forget (예외는 내부에서 처리)
    }

    // === 메인 로직 ===

    /// <summary>
    /// 실제 로그아웃 플로우
    /// 1) 확인창 닫기 + 진행중 오버레이 켜기
    /// 2) AuthAccount.SignOutAllAsync(releaseGuestNickname)
    /// 3) 타이틀 씬 이동 or 로그인 UI 표시
    /// </summary>
    private async Task SignOutFlowAsync()
    {
        // 1) UI 상태 전환
        SetConfirmVisible(false);
        SetProgress(true, "로그아웃 중...");

        // 2) 버튼 막기
        SetAllButtonsInteractable(false);

        // 3) 실제 로그아웃
        try
        {
            await AuthAccount.SignOutAllAsync(releaseGuestNickname);
        }
        catch (System.SystemException e)
        {
            Debug.LogWarning($"[LogoutPopup] SignOutAllAsync 예외: {e.Message}");
        }

        AuthAutoSuppressor.MarkOnce();
        
        // 4) 후처리: 타이틀 씬으로 이동하거나, 현재 씬에서 로그인 UI를 띄움
        try
        {
            if (!string.IsNullOrEmpty(titleSceneName))
            {
                if (verbose) Debug.Log($"[LogoutPopup] LoadScene('{titleSceneName}')");
                SceneManager.LoadScene(titleSceneName);
                return; // 씬 이동 후 아래 코드는 의미 없음
            }
            else
            {
                // 타이틀 씬 이름을 지정하지 않았다면, 현재 씬에서 로그인 UI 호출 시도
                var guestUI = FindObjectOfType<KYG.GuestLoginUI>(true);
                if (guestUI != null)
                {
                    guestUI.ShowLoginChoice(); // 게스트/구글 선택창
                    if (verbose) Debug.Log("[LogoutPopup] ShowLoginChoice()");
                }
                else
                {
                    Debug.LogWarning("[LogoutPopup] GuestLoginUI를 찾지 못했습니다. 타이틀 씬 이름을 설정하세요.");
                }
            }
        }
        finally
        {
            // 5) 진행중 오버레이 끄고 버튼 되살리기(씬 이동 시에는 의미 없음)
            SetProgress(false);
            await Managers.Manager.UI.CloseAllPopupUI();
            SetAllButtonsInteractable(true);
        }
    }

    // === 보조 ===

    private void SetConfirmVisible(bool visible)
    {
        if (panelConfirm) panelConfirm.SetActive(visible);
    }

    private void SetProgress(bool visible, string message = null)
    {
        if (panelProgress) panelProgress.SetActive(visible);

        if (!string.IsNullOrEmpty(message))
        {
            if (progressText) progressText.text = message;
#if TMP_PRESENT
            if (progressTMP)  progressTMP.text = message;
#endif
        }
    }

    private void SetAllButtonsInteractable(bool interactable)
    {
        // 위젯 하위 모든 Button을 일괄 잠그거나 풀어줍니다(중복 클릭 방지)
        foreach (var btn in GetComponentsInChildren<Button>(true))
            btn.interactable = interactable;
    }
}
