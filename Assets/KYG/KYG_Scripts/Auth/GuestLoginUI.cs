using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KYG.Auth; // GuestLoginManager 참조

/// <summary>
/// 게스트 로그인 UI 컨트롤러
/// - "게스트 로그인" 버튼 → 입력창 전환
/// - 닉네임 입력 후 잠시 멈추면(옵션) 자동 제출
/// - Enter/Done/포커스 이탈 시 제출 시도(기기 편차가 있어 최종은 [확인] 버튼 권장)
/// - [확인] 버튼은 "닉네임 입력창일 때만" 보이도록 처리
/// - Firebase 준비 전에는 자동 제출/확인 버튼 비활성(로그 스팸/실패 방지)
/// - 제출 시작 시 입력창을 숨기고 로딩 UI만 표시(씬 전환 전에도 UX 보장)
/// - 씬 전환/오브젝트 파괴 시 MissingReferenceException 방지 가드
/// </summary>
public class GuestLoginUI : MonoBehaviour
{
    [Header("UI Roots")]
    [SerializeField] private GameObject buttonRoot;        // "Guest Login" 버튼 컨테이너
    [SerializeField] private Button guestLoginButton;      // 클릭 시 입력창으로 전환
    [SerializeField] private GameObject inputRoot;         // TMP_InputField 컨테이너
    [SerializeField] private GameObject loadingRoot;       // "연결 중..." 스피너/텍스트(선택)

    [Header("Input")]
    [SerializeField] private TMP_InputField nicknameInput; // 닉네임 입력 필드
    [SerializeField] private TextMeshProUGUI hintText;     // 안내 문구(선택)
    [SerializeField] private Button confirmButton;         // ✅ 명시적 제출 버튼(입력창일 때만 표시)

    [Header("옵션")]
    [SerializeField] private int minLength = 2;            // 최소 글자수
    [SerializeField] private int maxLength = 16;           // 최대 글자수(GuestLoginManager와 일치)
    [SerializeField] private float idleSubmitSec = 1.0f;   // 타이핑 멈춘 뒤 자동 제출 대기(0/음수면 미사용)

    private float _lastTypeTime;
    private bool _submitting;   // 제출 중(중복 방지)
    private bool _destroyed;    // 파괴 플래그(Update 차단)
    private bool _lastReady;    // Firebase 준비 상태 변화 감지용

    private void Awake()
    {
        // 초기 화면: "게스트 로그인" 버튼만 보이고, 입력창/확인/로딩 숨김
        SafeShowButton();

        if (guestLoginButton != null) guestLoginButton.onClick.AddListener(SwitchToInput);
        else Debug.LogWarning("[GuestLoginUI] guestLoginButton 참조가 비어있습니다.");

        if (nicknameInput != null)
        {
            nicknameInput.onValueChanged.AddListener(OnTyping);
            nicknameInput.onSubmit.AddListener(OnSubmit);   // PC Enter / 일부 안드로이드 Done
            nicknameInput.onEndEdit.AddListener(OnSubmit);  // 포커스 이탈
        }
        else Debug.LogWarning("[GuestLoginUI] nicknameInput 참조가 비어있습니다.");

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnClickConfirm);
            confirmButton.interactable = false; // 글자수/준비 조건 전까지 비활성
        }
        else Debug.LogWarning("[GuestLoginUI] confirmButton 참조가 비어있습니다.");
    }

    private void OnDestroy()
    {
        _destroyed = true;
        _submitting = true;
        enabled = false;

        if (guestLoginButton != null) guestLoginButton.onClick.RemoveListener(SwitchToInput);

        if (nicknameInput != null)
        {
            nicknameInput.onValueChanged.RemoveListener(OnTyping);
            nicknameInput.onSubmit.RemoveListener(OnSubmit);
            nicknameInput.onEndEdit.RemoveListener(OnSubmit);
        }

        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnClickConfirm);
    }

    private void Update()
    {
        if (_destroyed) return;

        // ✅ Firebase 준비 상태가 바뀌면 힌트/버튼 즉시 갱신
        bool nowReady = IsReady();
        if (nowReady != _lastReady)
        {
            _lastReady = nowReady;
            RefreshReadyUI();
        }

        if (!isActiveAndEnabled || _submitting) return;
        if (inputRoot == null || nicknameInput == null) return;
        if (!inputRoot.activeInHierarchy) return; // 입력창 아닐 때 자동 제출 금지
        if (!nowReady) return;                    // Firebase 준비 전 자동 제출 금지

        // 자동 제출(원치 않으면 idleSubmitSec을 0/음수로 두거나 크게 설정)
        if (idleSubmitSec > 0f && Time.unscaledTime - _lastTypeTime >= idleSubmitSec)
        {
            TrySubmit(nicknameInput.text);
        }
    }

    // --- 준비 상태 확인 ---

    private bool IsReady()
    {
        var mgr = GuestLoginManager.Instance;
        return mgr != null && mgr.IsFirebaseReady;
    }

    // --- UI 전환 ---

    /// <summary>초기 화면: 버튼만 보이게</summary>
    private void SafeShowButton()
    {
        if (buttonRoot) buttonRoot.SetActive(true);
        if (inputRoot) inputRoot.SetActive(false);
        if (confirmButton) confirmButton.gameObject.SetActive(false); // ✅ 요구사항
        if (loadingRoot) loadingRoot.SetActive(false);
    }

    /// <summary>닉네임 입력창으로 전환</summary>
    private void SwitchToInput()
    {
        if (_destroyed) return;

        if (buttonRoot) buttonRoot.SetActive(false);
        if (inputRoot) inputRoot.SetActive(true);
        if (confirmButton) confirmButton.gameObject.SetActive(true);  // ✅ 입력창일 때만 표시
        if (loadingRoot) loadingRoot.SetActive(false);

        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = maxLength;
            nicknameInput.text = string.Empty;
            SafeSetHint(IsReady() ? $"닉네임을 입력하세요 (최소 {minLength}자)" : "초기화 중... 잠시만 기다려주세요");
            if (confirmButton) confirmButton.interactable = false; // 글자 입력되면 OnTyping에서 결정
            ActivateInput();
        }
        else Debug.LogWarning("[GuestLoginUI] nicknameInput이 없어 입력창을 활성화할 수 없습니다.");
    }

    /// <summary>입력 필드 활성화(모바일 키보드 유도)</summary>
    private void ActivateInput()
    {
        if (nicknameInput == null) return;
        nicknameInput.lineType = TMP_InputField.LineType.SingleLine; // IME '완료' 유도
        nicknameInput.contentType = TMP_InputField.ContentType.Standard;
        nicknameInput.keyboardType = TouchScreenKeyboardType.Default;
        nicknameInput.onFocusSelectAll = true;

        nicknameInput.Select();
        nicknameInput.ActivateInputField();
    }

    // --- 입력 처리 ---

    private void OnTyping(string _)
    {
        if (_destroyed) return;

        _lastTypeTime = Time.unscaledTime;

        int len = (nicknameInput != null ? nicknameInput.text : string.Empty).Trim().Length;
        bool lenOk = len >= minLength;
        bool ready = IsReady();

        if (hintText)
        {
            if (!lenOk) hintText.text = $"닉네임을 입력하세요 (최소 {minLength}자)";
            else hintText.text = ready ? "완료/확인 버튼을 누르거나 잠시 기다리면 연결됩니다" : "초기화 중... 잠시만 기다려주세요";
        }

        if (confirmButton) confirmButton.interactable = lenOk && ready; // 준비/길이 둘 다 만족해야 활성
    }

    private void OnSubmit(string _)
    {
        if (_destroyed || nicknameInput == null) return;
        // 일부 기기에서 타이밍 이슈가 있어 시도만 하고, 최종 보장은 [확인] 버튼이 담당.
        TrySubmit(nicknameInput.text);
    }

    private void OnClickConfirm()
    {
        if (_destroyed || nicknameInput == null) return;
        TrySubmit(nicknameInput.text);
    }

    /// <summary>유효성/준비 상태를 모두 만족할 때만 제출 시도</summary>
    private void TrySubmit(string raw)
    {
        if (_destroyed || _submitting) return;

        // Firebase 준비 전에는 제출 금지(로그 스팸/실패 방지)
        if (!IsReady())
        {
            SafeSetHint("초기화 중입니다. 잠시 후 다시 시도하세요.");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        string nick = Sanitize(raw);

        // 길이/유효성 검사
        if (nick.Length < minLength)
        {
            SafeSetHint($"닉네임을 {minLength}자 이상 입력하세요");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        var mgr = GuestLoginManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[GuestLoginUI] GuestLoginManager.Instance 를 찾지 못했습니다. 잠시 후 다시 시도하세요.");
            SafeSetHint("초기화 중입니다. 잠시 후 다시 시도하세요.");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        _submitting = true;
        SafeSetHint("연결 중...");
        if (confirmButton) confirmButton.interactable = false;

        // ✅ 제출 UI 상태 전환: 입력창 숨기고 로딩 표시
        ShowSubmittingUI(true);

        try
        {
            // Firebase 익명 로그인 + Photon 접속 + 로비 씬 전환까지 내부 처리
            mgr.LoginAsGuestWithNickname(nick);
        }
        catch (System.SystemException e)
        {
            Debug.LogError($"[GuestLoginUI] 로그인 요청 중 예외: {e.Message}");
            _submitting = false;
            SafeSetHint("연결 실패. 다시 시도하세요.");
            if (confirmButton) confirmButton.interactable = nick.Length >= minLength && IsReady();
            ShowSubmittingUI(false); // 실패 시 원복
            ActivateInput();
        }
    }

    private void ShowSubmittingUI(bool on)
    {
        if (inputRoot) inputRoot.SetActive(!on);     // ✅ 입력창 숨김
        if (buttonRoot) buttonRoot.SetActive(false); // 게스트 로그인 버튼은 항상 숨김
        if (confirmButton) confirmButton.gameObject.SetActive(!on);
        if (loadingRoot) loadingRoot.SetActive(on);  // 로딩 오브젝트 있으면 표시
    }

    private void RefreshReadyUI()
    {
        if (nicknameInput == null) return;
        int len = nicknameInput.text.Trim().Length;
        bool lenOk = len >= minLength;

        if (hintText)
            hintText.text = lenOk
                ? (_lastReady ? "완료/확인 버튼 또는 잠시 후 자동 연결됩니다" : "초기화 중... 잠시만 기다려주세요")
                : $"닉네임을 입력하세요 (최소 {minLength}자)";

        if (confirmButton) confirmButton.interactable = lenOk && _lastReady;
    }

    // --- 유틸리티 ---

    private string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        if (s.Length > maxLength) s = s.Substring(0, maxLength);
        return s;
    }

    private void SafeSetHint(string msg)
    {
        if (hintText) hintText.text = msg;
    }
}
