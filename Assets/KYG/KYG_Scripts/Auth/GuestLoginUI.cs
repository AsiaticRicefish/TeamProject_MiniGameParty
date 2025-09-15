using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using KYG.Auth;
using Managers;
using Photon.Pun; // GuestLoginManager 참조

public class GuestLoginUI : MonoBehaviour
{
    [Header("UI Roots")] [SerializeField] private GameObject buttonRoot;
    [SerializeField] private Button guestLoginButton;
    [SerializeField] private Button gpgsLoginButton;    // GPGS 로그인 버튼
    [SerializeField] private GameObject inputRoot;
    [SerializeField] private GameObject loadingRoot;

    [Header("Input")] [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Button confirmButton;

    [Header("옵션")] [SerializeField] private int minLength = 2;
    [SerializeField] private int maxLength = 16;
    [SerializeField] private float idleSubmitSec = 1.0f;
    
    [Header("취소 버튼")]
    [SerializeField] private Button cancelInInputButton;   // 닉네임 입력창에서의 취소
    [SerializeField] private Button cancelLoadingButton;   // 로딩(연결중) 화면에서의 취소
    [SerializeField] private bool returnToFirstOnInputCancel = true; // 입력취소 시 첫 화면으로

    [Header("Hint Style")] [SerializeField]
    private Color normalHintColor = new Color(1, 1, 1, 0.75f);

    [SerializeField] private Color errorHintColor = new Color(1, 0.25f, 0.25f, 1f);
    [SerializeField] private CanvasGroup toastGroup; // 힌트 텍스트를 감싸는 CanvasGroup (선택)
    [SerializeField] private float toastFade = 0.15f; // 페이드 시간
    [SerializeField] private float toastHold = 1.5f; // 보여주는 시간

    private float _lastTypeTime;
    private bool _submitting;
    private bool _destroyed;
    private bool _lastReady;
    private int _effectiveMax = 8;
    private bool _blockSubmit;
    
    private static readonly System.Text.RegularExpressions.Regex RxKorean =
        new System.Text.RegularExpressions.Regex("[가-힣]", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex RxUpper =
        new System.Text.RegularExpressions.Regex("[A-Z]", System.Text.RegularExpressions.RegexOptions.Compiled);
    
    private void Awake()
    {
        SafeShowButton();

        if (guestLoginButton != null) guestLoginButton.onClick.AddListener(SwitchToInput);
        else Debug.LogWarning("[GuestLoginUI] guestLoginButton 참조가 비어있습니다.");

        if (nicknameInput != null)
        {
            nicknameInput.onValueChanged.AddListener(OnTyping);
            nicknameInput.onSubmit.AddListener(OnSubmit);
            //nicknameInput.onEndEdit.AddListener(OnSubmit);
        }
        else Debug.LogWarning("[GuestLoginUI] nicknameInput 참조가 비어있습니다.");

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnClickConfirm);
            confirmButton.interactable = false;
        }
        else Debug.LogWarning("[GuestLoginUI] confirmButton 참조가 비어있습니다.");
        
        if (cancelInInputButton != null) cancelInInputButton.onClick.AddListener(OnClickCancelInput);
        if (cancelLoadingButton != null) cancelLoadingButton.onClick.AddListener(OnClickCancelLoading);
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
            //nicknameInput.onEndEdit.RemoveListener(OnSubmit);
        }

        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnClickConfirm);
        
        if (cancelInInputButton != null) cancelInInputButton.onClick.RemoveListener(OnClickCancelInput);
        if (cancelLoadingButton != null) cancelLoadingButton.onClick.RemoveListener(OnClickCancelLoading);
    }

    private void Update()
    {
        if (_destroyed) return;

        bool nowReady = IsReady();
        if (nowReady != _lastReady)
        {
            _lastReady = nowReady;
            RefreshReadyUI();
        }

        // ▼ 여기 수정: _blockSubmit일 때는 자동 제출 금지
        if (!isActiveAndEnabled || _submitting || _blockSubmit) return;
        if (inputRoot == null || nicknameInput == null) return;
        if (!inputRoot.activeInHierarchy) return;
        if (!nowReady) return;

        if (idleSubmitSec > 0f && Time.unscaledTime - _lastTypeTime >= idleSubmitSec)
        {
            TrySubmit(nicknameInput.text);
        }
    }

    // ------- 외부에서 쓰는 공개 API --------

    /// <summary>입력/버튼 인터랙션 토글 (로딩 중 잠금 등)</summary>
    public void SetInteractable(bool value) // ★ 추가
    {
        if (nicknameInput) nicknameInput.interactable = value;
        if (confirmButton)
            confirmButton.interactable =
                value && nicknameInput && nicknameInput.text.Trim().Length >= minLength && IsReady();
    }

    /// <summary>실패 후 재입력 플로우: 입력창 다시 보여주고 포커스</summary>
    public void EnableNicknameRetry() // ★ 추가
    {
        _submitting = false;
        ShowSubmittingUI(false);
        if (nicknameInput)
        {
            nicknameInput.text = "";
            ActivateInput();
        }

        if (confirmButton) confirmButton.interactable = false;
    }

    /// <summary>힌트 문구 안전하게 교체</summary>
    public void SafeSetHint(string msg) // 기존 메서드는 그대로 두고
    {
        if (hintText)
        {
            hintText.color = normalHintColor;
            hintText.text = msg ?? "";
        }
    }

    public void ShowErrorHint(string msg)
    {
        if (!hintText) return;
        hintText.color = errorHintColor;
        hintText.text = msg ?? "";

        // 선택: 토스트 페이드 인/아웃 (toastGroup 있으면)
        if (toastGroup)
            StartCoroutine(CoToast());
        else
            StartCoroutine(CoShake(hintText.transform)); // toastGroup 없으면 살짝 흔들기
    }
    
    

    private IEnumerator CoShake(Transform tr, float amp = 10f, float dur = 0.18f)
    {
        Vector3 basePos = tr.localPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Sin(t * 80f) * (1f - t / dur); // 감쇠
            tr.localPosition = basePos + Vector3.right * p * amp;
            yield return null;
        }

        tr.localPosition = basePos;
    }

    private IEnumerator CoToast()
    {
        // fade in
        toastGroup.gameObject.SetActive(true);
        float t = 0f;
        while (t < toastFade)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Lerp(0f, 1f, t / toastFade);
            yield return null;
        }

        toastGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(toastHold);

        // fade out
        t = 0f;
        while (t < toastFade)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Lerp(1f, 0f, t / toastFade);
            yield return null;
        }

        toastGroup.alpha = 0f;
        toastGroup.gameObject.SetActive(false);
    }

    // --------------- 내부 구현 ---------------

    private bool IsReady()
    {
        var mgr = GuestLoginManager.Instance;
        return mgr != null && mgr.IsFirebaseReady;
    }

    private void SafeShowButton()
    {
        if (buttonRoot) buttonRoot.SetActive(true);
        if (inputRoot) inputRoot.SetActive(false);
        if (confirmButton) confirmButton.gameObject.SetActive(false);
        if (loadingRoot) loadingRoot.SetActive(false);
        
        // 로그인 화면 전용 버튼은 여기서만 보이게
        if (guestLoginButton) guestLoginButton.gameObject.SetActive(true);
        if (gpgsLoginButton) gpgsLoginButton.gameObject.SetActive(true);
        
        if (cancelInInputButton) cancelInInputButton.gameObject.SetActive(false);
        if (cancelLoadingButton) cancelLoadingButton.gameObject.SetActive(false);
    }

    private void SwitchToInput()
    {
        if (_destroyed) return;

        if (buttonRoot) buttonRoot.SetActive(false);
        if (inputRoot) inputRoot.SetActive(true);
        if (confirmButton) confirmButton.gameObject.SetActive(true);
        if (loadingRoot) loadingRoot.SetActive(false);

        // 입력 취소 버튼 표시
        if (cancelInInputButton) cancelInInputButton.gameObject.SetActive(true);
        if (cancelLoadingButton) cancelLoadingButton.gameObject.SetActive(false);
        
        // 입력 화면에서는 숨김
        if (guestLoginButton) guestLoginButton.gameObject.SetActive(false);
        if (gpgsLoginButton) gpgsLoginButton.gameObject.SetActive(false);

        if (nicknameInput != null)
        {
            nicknameInput.text = string.Empty;

            // ★ 처음 들어올 때도 규칙 기반 최대치 세팅
            _effectiveMax = CalcEffectiveMax(nicknameInput.text);
            nicknameInput.characterLimit = _effectiveMax;

            SafeSetHint(IsReady() ? $"닉네임을 입력하세요 (최소 {minLength}자, 최대 {_effectiveMax}자)" : "초기화 중... 잠시만 기다려주세요");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
        }
        else Debug.LogWarning("[GuestLoginUI] nicknameInput이 없어 입력창을 활성화할 수 없습니다.");
    }

    private void ActivateInput()
    {
        if (nicknameInput == null) return;
        nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        nicknameInput.contentType = TMP_InputField.ContentType.Standard;
        nicknameInput.keyboardType = TouchScreenKeyboardType.Default;
        nicknameInput.onFocusSelectAll = true;

        nicknameInput.Select();
        nicknameInput.ActivateInputField();
    }

    private void OnTyping(string _)
    {
        if (_destroyed) return;

        _lastTypeTime = Time.unscaledTime;

        // ★ 현재 텍스트 기준 최대치 계산 & characterLimit 갱신
        _effectiveMax = CalcEffectiveMax(nicknameInput != null ? nicknameInput.text : string.Empty);
        if (nicknameInput) nicknameInput.characterLimit = _effectiveMax;

        int len = (nicknameInput != null ? nicknameInput.text : string.Empty).Trim().Length;
        bool lenOk = len >= minLength && len <= _effectiveMax;
        bool ready = IsReady();

        if (hintText)
        {
            if (!lenOk) hintText.text = $"닉네임을 입력하세요 (최소 {minLength}자, 최대 {_effectiveMax}자)";
            else hintText.text = ready ? "완료/확인 버튼을 누르거나 잠시 기다리면 연결됩니다" : "초기화 중... 잠시만 기다려주세요";
        }

        if (confirmButton) confirmButton.interactable = lenOk && ready;

        // 입력 취소 버튼 가시성 갱신
        if (cancelInInputButton) cancelInInputButton.gameObject.SetActive(inputRoot && inputRoot.activeSelf);
    }

    private void OnSubmit(string _)
    {
        // 차단 중이거나 입력창이 비활성일 때 무시
        if (_blockSubmit || _destroyed || nicknameInput == null || inputRoot == null || !inputRoot.activeInHierarchy)
            return;

        TrySubmit(nicknameInput.text);
    }

    private void OnClickConfirm()
    {
        if (!_destroyed && nicknameInput != null) TrySubmit(nicknameInput.text);
    }

    private void TrySubmit(string raw)
    {
        // 차단 중이면 무시
        if (_blockSubmit || _destroyed || _submitting) return;

        if (!IsReady())
        {
            SafeSetHint("초기화 중입니다. 잠시 후 다시 시도하세요.");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        string nick = Sanitize(raw);

        // ★ 제출 시에도 동적 최대 길이 재평가
        _effectiveMax = CalcEffectiveMax(nick);
        if (nicknameInput) nicknameInput.characterLimit = _effectiveMax;

        if (nick.Length < minLength || nick.Length > _effectiveMax)
        {
            SafeSetHint($"닉네임을 {minLength}~{_effectiveMax}자 범위로 입력하세요");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        var mgr = KYG.Auth.GuestLoginManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[GuestLoginUI] GuestLoginManager.Instance 를 찾지 못했습니다.");
            SafeSetHint("초기화 중입니다. 잠시 후 다시 시도하세요.");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        _submitting = true;
        SafeSetHint("연결 중...");
        if (confirmButton) confirmButton.interactable = false;
        ShowSubmittingUI(true);

        try
        {
#if TEST_WITHOUT_LOGIN
            Managers.Manager.Network.SetTestNicknameAndID(nick);
            Photon.Pun.PhotonNetwork.ConnectUsingSettings();
#else
        mgr.LoginAsGuestWithNickname(nick);
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GuestLoginUI] 로그인 요청 중 예외: {e.Message}");
            _submitting = false;
            SafeSetHint("연결 실패. 다시 시도하세요.");
            if (confirmButton) confirmButton.interactable = nick.Length >= minLength && IsReady();
            ShowSubmittingUI(false);
            ActivateInput();
        }
    }

    public void ShowSubmittingUI(bool on)
    {
        if (inputRoot) inputRoot.SetActive(!on);
        if (buttonRoot) buttonRoot.SetActive(false);
        if (confirmButton) confirmButton.gameObject.SetActive(!on);
        if (loadingRoot) loadingRoot.SetActive(on);

        // 로딩 취소 버튼은 로딩 화면에서만 보이게
        if (cancelLoadingButton) cancelLoadingButton.gameObject.SetActive(on);
        // 입력 취소 버튼은 입력 화면에서만 보이게
        if (cancelInInputButton) cancelInInputButton.gameObject.SetActive(!on && inputRoot && inputRoot.activeSelf);
        
        // 로딩 중에도 숨김 유지
        if (guestLoginButton) guestLoginButton.gameObject.SetActive(false);
        if (gpgsLoginButton) gpgsLoginButton.gameObject.SetActive(false);
    }
    
    // (옵션) 처음 화면으로 복귀하고 싶을 때 호출
    public void ReturnToFirstIfWanted()
    {
        if (!returnToFirstOnInputCancel) return;
        SafeShowButton();
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

    private string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        if (s.Length > maxLength) s = s.Substring(0, maxLength);
        return s;
    }
    
    // 닉네임 입력창의 취소 버튼
    private void OnClickCancelInput()
    {
        _blockSubmit = true;          // ★ 제출 차단
        _submitting = false;          // 혹시나 진행 중 플래그 해제
        StopAllCoroutines();          // 토스트/셰이크 등 코루틴 정리

        // 입력 단계 취소 → 첫 화면 또는 입력창 닫기
        if (returnToFirstOnInputCancel) SafeShowButton();
        else
        {
            if (inputRoot) inputRoot.SetActive(false);
            if (confirmButton) confirmButton.gameObject.SetActive(false);
            if (buttonRoot) buttonRoot.SetActive(true);
            if (loadingRoot) loadingRoot.SetActive(false);
        }

        // UI 정리
        if (nicknameInput) nicknameInput.text = string.Empty;
        if (confirmButton) confirmButton.interactable = false;
        SafeSetHint("");

        // 약간의 프레임 지연 뒤 제출 차단 해제 (포커스 전환/EndEdit 이벤트가 모두 끝난 후)
        StartCoroutine(CoUnblockSubmitNextFrame());
    }
    
    private System.Collections.IEnumerator CoUnblockSubmitNextFrame()
    {
        // 다음 프레임까지 대기해서 onEndEdit로 인한 OnSubmit 꼬임 방지
        yield return null;
        _blockSubmit = false;
    }
    
    private void OnClickCancelLoading()
    {
        // 로딩(연결 중) 취소: 매니저에 취소 요청
        var mgr = KYG.Auth.GuestLoginManager.Instance;
        if (mgr != null) mgr.CancelPendingLogin();
        else
        {
            // 매니저가 없다면 로딩 UI만 닫고 입력으로 복귀
            ShowSubmittingUI(false);
            EnableNicknameRetry();
            SafeSetHint("취소했습니다. 다시 시도하세요.");
        }
    }
    
    private int CalcEffectiveMax(string s)
    {
        if (string.IsNullOrEmpty(s)) return 8;
        // 한글 또는 대문자 한 글자라도 포함되어 있으면 6
        if (RxKorean.IsMatch(s) || RxUpper.IsMatch(s)) return 6;
        // 그 외(전부 소문자 등) 8
        return 8;
    }
}