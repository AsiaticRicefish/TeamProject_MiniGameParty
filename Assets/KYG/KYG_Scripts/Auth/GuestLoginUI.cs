using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using KYG.Auth;
using Managers;
using Photon.Pun;

public class GuestLoginUI : MonoBehaviour
{
    [Header("UI Roots")] [SerializeField] private GameObject buttonRoot; // 팝업 카드(버튼 컨테이너)
    [SerializeField] private Button guestLoginButton; // "게스트로 바로 시작하기"
    [SerializeField] private Button gpgsLoginButton; // "Google Play Games 연결"
    [SerializeField] private GameObject inputRoot; // 닉네임 입력 컨테이너(별도 오브젝트 권장)
    [SerializeField] private GameObject loadingRoot; // 로딩 컨테이너(선택)

    [Header("Input")] [SerializeField] private TMP_InputField nicknameInput; // 입력 필드
    [SerializeField] private TextMeshProUGUI hintText; // 안내/오류 텍스트
    [SerializeField] private Button confirmButton; // 닉네임 확인 버튼

    [Header("옵션")] [SerializeField] private int minLength = 2;
    [SerializeField] private int maxLength = 16;
    [SerializeField] private float idleSubmitSec = 1.0f; // 입력 멈춘 뒤 자동 제출까지 딜레이(0이면 비활성)

    [Header("취소 버튼")] [SerializeField] private Button cancelInInputButton; // 입력창 내 취소
    [SerializeField] private Button cancelLoadingButton; // 로딩중 취소
    [SerializeField] private bool returnToFirstOnInputCancel = true; // 입력 취소 시 첫 화면(버튼 루트 숨김/표시 정책)에 맞춤

    [Header("Hint Style")] [SerializeField]
    private Color normalHintColor = new Color(1, 1, 1, 0.75f);

    [SerializeField] private Color errorHintColor = new Color(1, 0.25f, 0.25f, 1f);
    [SerializeField] private CanvasGroup toastGroup; // 선택: 토스트용
    [SerializeField] private float toastFade = 0.15f;
    [SerializeField] private float toastHold = 1.5f;

    [SerializeField] private GameObject nicknamePopupPrefab; // 닉네임 팝업 프리팹(위 NicknamePopup.cs 포함)
    [SerializeField] private Canvas popupCanvasOverride; // 팝업을 띄울 최상단 Canvas (비우면 자동 탐색)
    [SerializeField] private int popupOrderBoost = 100; // 최상단 보장용 정렬 가산치

    private float _lastTypeTime;
    private bool _submitting;
    private bool _destroyed;
    private bool _lastReady;
    private int _effectiveMax = 8;
    private bool _blockSubmit;
    private GameObject _nicknamePopupInstance;

    // 한글/대문자 혼합 시 6자, 소문자만 8자 룰
    private static readonly System.Text.RegularExpressions.Regex RxKorean =
        new System.Text.RegularExpressions.Regex("[가-힣]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex RxUpper =
        new System.Text.RegularExpressions.Regex("[A-Z]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private void Awake()
    {
        // 시작 시 버튼/입력/로딩 모두 숨김(팝업은 ScreenTapCatcher가 ShowLoginChoice로 띄움)
        SafeShowFirst();

        if (guestLoginButton)
        {
            guestLoginButton.onClick.RemoveAllListeners(); // ← 기존 SwitchToInput 등 전부 제거
            guestLoginButton.onClick.AddListener(OpenNicknamePopup);
        }

        if (gpgsLoginButton)
        {
            gpgsLoginButton.onClick.RemoveAllListeners();
            gpgsLoginButton.onClick.AddListener(OnClickGpgsLogin);
        }

        if (nicknameInput)
        {
            nicknameInput.onValueChanged.AddListener(OnTyping);
            nicknameInput.onSubmit.AddListener(OnSubmit);
        }

        if (confirmButton)
        {
            confirmButton.onClick.AddListener(OnClickConfirm);
            confirmButton.interactable = false;
        }

        if (cancelInInputButton) cancelInInputButton.onClick.AddListener(OnClickCancelInput);
        if (cancelLoadingButton) cancelLoadingButton.onClick.AddListener(OnClickCancelLoading);
    }

    private void OnDestroy()
    {
        _destroyed = true;
        _submitting = true;
        enabled = false;

        if (guestLoginButton) guestLoginButton.onClick.RemoveListener(SwitchToInput);
        if (gpgsLoginButton) gpgsLoginButton.onClick.RemoveListener(OnClickGpgsLogin);

        if (nicknameInput)
        {
            nicknameInput.onValueChanged.RemoveListener(OnTyping);
            nicknameInput.onSubmit.RemoveListener(OnSubmit);
        }

        if (confirmButton) confirmButton.onClick.RemoveListener(OnClickConfirm);
        if (cancelInInputButton) cancelInInputButton.onClick.RemoveListener(OnClickCancelInput);
        if (cancelLoadingButton) cancelLoadingButton.onClick.RemoveListener(OnClickCancelLoading);
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

        if (!isActiveAndEnabled || _submitting || _blockSubmit) return;
        if (inputRoot == null || nicknameInput == null) return;
        if (!inputRoot.activeInHierarchy) return;
        if (!nowReady) return;
        if (idleSubmitSec <= 0f) return;

        if (Time.unscaledTime - _lastTypeTime >= idleSubmitSec)
            TrySubmit(nicknameInput.text);
    }

    // ---------- 공개 API ----------

    /// <summary>처음(팝업 뜨기 전) 상태로 안전 초기화</summary>
    private void SafeShowFirst()
    {
        if (buttonRoot) buttonRoot.SetActive(false);
        if (guestLoginButton) guestLoginButton.gameObject.SetActive(false);
        if (gpgsLoginButton) gpgsLoginButton.gameObject.SetActive(false);
        if (inputRoot) inputRoot.SetActive(false);
        if (confirmButton) confirmButton.gameObject.SetActive(false);
        if (loadingRoot) loadingRoot.SetActive(false);
        if (cancelInInputButton) cancelInInputButton.gameObject.SetActive(false);
        if (cancelLoadingButton) cancelLoadingButton.gameObject.SetActive(false);
        SafeSetHint(string.Empty);
        LogButtonStates("SafeShowFirst");
    }

    /// <summary>
    /// ScreenTapCatcher에서 호출: 로그인 선택(버튼) 팝업을 표시.
    /// 강제 표시/그래픽/레이캐스트까지 정리하여 SetActive(false)로 가려지는 문제를 무력화.
    /// </summary>
    public void ShowLoginChoice()
    {
        if (inputRoot) inputRoot.SetActive(false);
        if (loadingRoot) loadingRoot.SetActive(false);

        ForceButtonsOn(); // 핵심: 버튼/그래픽/레이캐스트 강제 ON
        LogButtonStates("ShowLoginChoice-done");
    }

    /// <summary>UI 전체 입력 토글(로딩 중 잠금 등)</summary>
    public void SetInteractable(bool value)
    {
        if (nicknameInput) nicknameInput.interactable = value;
        if (confirmButton)
            confirmButton.interactable =
                value && nicknameInput && nicknameInput.text.Trim().Length >= minLength && IsReady();
    }

    /// <summary>실패 후 재입력 플로우</summary>
    public void EnableNicknameRetry()
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

    /// <summary>힌트 문구 안전 교체</summary>
    public void SafeSetHint(string msg)
    {
        if (!hintText) return;
        hintText.color = normalHintColor;
        hintText.text = msg ?? "";
    }

    public void ShowErrorHint(string msg)
    {
        if (!hintText) return;
        hintText.color = errorHintColor;
        hintText.text = msg ?? "";

        if (toastGroup) StartCoroutine(CoToast());
        else StartCoroutine(CoShake(hintText.transform));
    }

    // ---------- 내부 구현 ----------

    private bool IsReady()
    {
        var mgr = GuestLoginManager.Instance;
        return mgr != null && mgr.IsFirebaseReady;
    }

    private void SwitchToInput()
    {
        if (_destroyed) return;

        if (buttonRoot) buttonRoot.SetActive(false);
        if (inputRoot) inputRoot.SetActive(true);
        if (confirmButton) confirmButton.gameObject.SetActive(true);
        if (loadingRoot) loadingRoot.SetActive(false);

        if (cancelInInputButton) cancelInInputButton.gameObject.SetActive(true);
        if (cancelLoadingButton) cancelLoadingButton.gameObject.SetActive(false);

        if (guestLoginButton) guestLoginButton.gameObject.SetActive(false);
        if (gpgsLoginButton) gpgsLoginButton.gameObject.SetActive(false);

        if (nicknameInput)
        {
            nicknameInput.text = string.Empty;
            _effectiveMax = CalcEffectiveMax(nicknameInput.text);
            nicknameInput.characterLimit = _effectiveMax;
            SafeSetHint(IsReady()
                ? $"닉네임을 입력하세요 (최소 {minLength}자, 최대 {_effectiveMax}자)"
                : "초기화 중... 잠시만 기다려주세요");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
        }

        LogButtonStates("SwitchToInput");
    }

    private void OnClickGpgsLogin()
    {
        // GPGS 팝업/흐름은 별도 매니저가 처리
        var g = FindObjectOfType<KYG.Auth.GPGSLoginManager>();
        if (g == null)
        {
            Debug.LogWarning("[GuestLoginUI] GPGSLoginManager가 씬에 없습니다.");
            return;
        }

        g.LoginWithGPGS();
    }

    private void ActivateInput()
    {
        if (!nicknameInput) return;
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

        _effectiveMax = CalcEffectiveMax(nicknameInput != null ? nicknameInput.text : string.Empty);
        if (nicknameInput) nicknameInput.characterLimit = _effectiveMax;

        int len = (nicknameInput != null ? nicknameInput.text : string.Empty).Trim().Length;
        bool lenOk = len >= minLength && len <= _effectiveMax;
        bool ready = IsReady();

        if (hintText)
        {
            if (!lenOk) hintText.text = $"닉네임을 입력하세요 (최소 {minLength}자, 최대 {_effectiveMax}자)";
            else
                hintText.text = ready
                    ? "완료/확인 버튼을 누르거나 잠시 기다리면 연결됩니다"
                    : "초기화 중... 잠시만 기다려주세요";
        }

        if (confirmButton) confirmButton.interactable = lenOk && ready;
        if (cancelInInputButton) cancelInInputButton.gameObject.SetActive(inputRoot && inputRoot.activeSelf);
    }

    private void OnSubmit(string _)
    {
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
        if (_blockSubmit || _destroyed || _submitting) return;

        if (!IsReady())
        {
            SafeSetHint("초기화 중입니다. 잠시 후 다시 시도하세요.");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        string nick = Sanitize(raw);
        _effectiveMax = CalcEffectiveMax(nick);
        if (nicknameInput) nicknameInput.characterLimit = _effectiveMax;

        if (nick.Length < minLength || nick.Length > _effectiveMax)
        {
            SafeSetHint($"닉네임을 {minLength}~{_effectiveMax}자 범위로 입력하세요");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        var mgr = GuestLoginManager.Instance;
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
            PhotonNetwork.ConnectUsingSettings();
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

        if (cancelLoadingButton) cancelLoadingButton.gameObject.SetActive(on);
        if (cancelInInputButton) cancelInInputButton.gameObject.SetActive(!on && inputRoot && inputRoot.activeSelf);

        if (guestLoginButton) guestLoginButton.gameObject.SetActive(false);
        if (gpgsLoginButton) gpgsLoginButton.gameObject.SetActive(false);

        LogButtonStates(on ? "ShowSubmittingUI-ON" : "ShowSubmittingUI-OFF");
    }

    public void ReturnToFirstIfWanted()
    {
        if (!returnToFirstOnInputCancel) return;
        SafeShowFirst();
    }

    private void RefreshReadyUI()
    {
        if (!nicknameInput) return;
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

    // 입력창 취소
    private void OnClickCancelInput()
    {
        _blockSubmit = true;
        _submitting = false;
        StopAllCoroutines();

        if (returnToFirstOnInputCancel) SafeShowFirst();
        else
        {
            if (inputRoot) inputRoot.SetActive(false);
            if (confirmButton) confirmButton.gameObject.SetActive(false);
            if (buttonRoot) buttonRoot.SetActive(true);
            if (guestLoginButton) guestLoginButton.gameObject.SetActive(true);
            if (gpgsLoginButton) gpgsLoginButton.gameObject.SetActive(true);
            if (loadingRoot) loadingRoot.SetActive(false);
        }

        if (nicknameInput) nicknameInput.text = string.Empty;
        if (confirmButton) confirmButton.interactable = false;
        SafeSetHint("");

        StartCoroutine(CoUnblockSubmitNextFrame());
        LogButtonStates("OnClickCancelInput");
    }

    private IEnumerator CoUnblockSubmitNextFrame()
    {
        yield return null; // onEndEdit/Submit 꼬임 방지
        _blockSubmit = false;
    }

    // 로딩 취소
    private void OnClickCancelLoading()
    {
        var mgr = GuestLoginManager.Instance;
        if (mgr != null) mgr.CancelPendingLogin();
        else
        {
            ShowSubmittingUI(false);
            EnableNicknameRetry();
            SafeSetHint("취소했습니다. 다시 시도하세요.");
        }
    }

    private IEnumerator CoShake(Transform tr, float amp = 10f, float dur = 0.18f)
    {
        var basePos = tr.localPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Sin(t * 80f) * (1f - t / dur);
            tr.localPosition = basePos + Vector3.right * p * amp;
            yield return null;
        }

        tr.localPosition = basePos;
    }

    private IEnumerator CoToast()
    {
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

    private int CalcEffectiveMax(string s)
    {
        if (string.IsNullOrEmpty(s)) return 8;
        if (RxKorean.IsMatch(s) || RxUpper.IsMatch(s)) return 6;
        return 8;
    }

    // ---------- 디버그/강제표시 유틸 ----------

    private void LogButtonStates(string tag)
    {
        bool br = buttonRoot && buttonRoot.activeSelf;
        bool brH = buttonRoot && buttonRoot.activeInHierarchy;
        bool g1 = guestLoginButton && guestLoginButton.gameObject.activeSelf;
        bool g1H = guestLoginButton && guestLoginButton.gameObject.activeInHierarchy;
        bool g2 = gpgsLoginButton && gpgsLoginButton.gameObject.activeSelf;
        bool g2H = gpgsLoginButton && gpgsLoginButton.gameObject.activeInHierarchy;

        Debug.Log($"[GuestLoginUI][{tag}] buttonRoot act={br}/{brH}, guest act={g1}/{g1H}, gpgs act={g2}/{g2H}");
    }

    private void ForceButtonsOn()
    {
        if (buttonRoot && !buttonRoot.activeSelf) buttonRoot.SetActive(true);
        if (guestLoginButton && !guestLoginButton.gameObject.activeSelf) guestLoginButton.gameObject.SetActive(true);
        if (gpgsLoginButton && !gpgsLoginButton.gameObject.activeSelf) gpgsLoginButton.gameObject.SetActive(true);

        // CanvasGroup 차단 해제
        if (buttonRoot)
        {
            var cg = buttonRoot.GetComponent<CanvasGroup>();
            if (cg)
            {
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        // 하위 그래픽 강제 표시
        TouchGraphics(guestLoginButton ? guestLoginButton.gameObject : null);
        TouchGraphics(gpgsLoginButton ? gpgsLoginButton.gameObject : null);
    }

    private void TouchGraphics(GameObject go)
    {
        if (!go) return;
        foreach (var g in go.GetComponentsInChildren<Graphic>(true))
        {
            var c = g.color;
            c.a = 1f;
            g.color = c;
            g.raycastTarget = true;
            g.gameObject.SetActive(true);
        }
    }

    private Canvas FindTopCanvas()
    {
        Canvas top = null;
        int topOrder = int.MinValue;
        foreach (var cv in FindObjectsOfType<Canvas>(true))
        {
            if (!cv.enabled) continue;
            if (cv.sortingOrder > topOrder)
            {
                topOrder = cv.sortingOrder;
                top = cv;
            }
        }

        return top != null ? top : GetComponentInParent<Canvas>();
    }

    private bool IsSceneCanvas(Canvas c)
    {
        return c != null && c.gameObject.scene.IsValid() && c.isActiveAndEnabled;
    }

    private Canvas ResolveTargetCanvas()
    {
        // override가 프리팹이거나 비활성이면 무시하고 자동 탐색
        if (!IsSceneCanvas(popupCanvasOverride))
        {
            var top = FindTopCanvas();
            if (top != null) return top;

            // 최후: 자신의 상위에서라도 찾기
            var self = GetComponentInParent<Canvas>();
            if (IsSceneCanvas(self)) return self;
        }

        return popupCanvasOverride;
    }

    private void OpenNicknamePopup()
{
    // 1) 로그인 선택 팝업은 끄기
    if (buttonRoot) buttonRoot.SetActive(false);

    // 2) 타겟 Canvas 결정(씬 오브젝트)
    var canvas = ResolveTargetCanvas(); // 이전에 드린 메서드 그대로 사용
    if (canvas == null)
    {
        Debug.LogError("[GuestLoginUI] Canvas가 없어 닉네임 팝업을 표시할 수 없습니다.");
        return;
    }

    // 3) 프리팹 지정 확인
    if (nicknamePopupPrefab == null)
    {
        Debug.LogError("[GuestLoginUI] nicknamePopupPrefab 미지정");
        return;
    }

    // 4) 항상 새 인스턴스 생성(씬 오브젝트/프리팹 여부 상관없이)
    //    ※ 프리팹 루트가 비활성이라면 인스턴스도 비활성로 생성되므로 곧바로 SetActive(true) 처리
    var go = Instantiate(nicknamePopupPrefab, canvas.transform);
    go.SetActive(true);

    // 5) 최상단 보장
    var c = go.GetComponent<Canvas>() ?? go.AddComponent<Canvas>();
    c.overrideSorting = true;
    c.sortingOrder = canvas.sortingOrder + popupOrderBoost;
    if (!go.GetComponent<UnityEngine.UI.GraphicRaycaster>())
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

    // (디버그) 생성 상태 출력
    Debug.Log($"[GuestLoginUI] NicknamePopup instanced. activeSelf={go.activeSelf}, inHierarchy={go.activeInHierarchy}");

    // 6) 초기화(확인/취소 콜백 연결)
    var popup = go.GetComponent<NicknamePopup>();
    if (popup != null)
    {
        popup.Init(
            onConfirm: (raw) =>
            {
                string nick = Sanitize(raw);
                int effMax = CalcEffectiveMax(nick);
                if (nick.Length < 2 || nick.Length > effMax)
                {
                    popup.ShowError($"닉네임을 2~{effMax}자 범위로 입력하세요");
                    return;
                }

                popup.Close();          // 입력 성공 → 팝업 닫기
                ShowSubmittingUI(true); // 로딩 표시
                TrySubmit(nick);        // 기존 로그인 흐름(예약/Photon 연결 포함)
            },
            onCancel: () =>
            {
                if (buttonRoot) buttonRoot.SetActive(true);
                ForceButtonsOn();
            },
            placeholder: "(최대 한글 6자, 영문 8자)",
            onCheck: async (raw) =>
            {
                // ★ 단순 가용성 조회(예약 X)
                string nick = Sanitize(raw);
                int effMax = CalcEffectiveMax(nick);
                if (nick.Length < 2 || nick.Length > effMax) return false;
                return await NicknameRegistry.IsAvailableAsync(nick);
            }
        );
    }
    else
    {
        Debug.LogWarning("[GuestLoginUI] NicknamePopup 컴포넌트가 프리팹에 없습니다.");
    }

    _nicknamePopupInstance = go;
    Debug.Log("[GuestLoginUI] 로그인 팝업 OFF, 닉네임 팝업 ON(Instantiate).");
}
}
