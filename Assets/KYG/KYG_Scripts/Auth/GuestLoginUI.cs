using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using KYG.Auth;
using LDH.LDH_Scripts.Test;
using Managers;
using Photon.Pun; // GuestLoginManager 참조

public class GuestLoginUI : MonoBehaviour
{
    [Header("UI Roots")] [SerializeField] private GameObject buttonRoot;
    [SerializeField] private Button guestLoginButton;
    [SerializeField] private GameObject inputRoot;
    [SerializeField] private GameObject loadingRoot;

    [Header("Input")] [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Button confirmButton;

    [Header("옵션")] [SerializeField] private int minLength = 2;
    [SerializeField] private int maxLength = 16;
    [SerializeField] private float idleSubmitSec = 1.0f;

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

    private void Awake()
    {
        SafeShowButton();

        if (guestLoginButton != null) guestLoginButton.onClick.AddListener(SwitchToInput);
        else Debug.LogWarning("[GuestLoginUI] guestLoginButton 참조가 비어있습니다.");

        if (nicknameInput != null)
        {
            nicknameInput.onValueChanged.AddListener(OnTyping);
            nicknameInput.onSubmit.AddListener(OnSubmit);
            nicknameInput.onEndEdit.AddListener(OnSubmit);
        }
        else Debug.LogWarning("[GuestLoginUI] nicknameInput 참조가 비어있습니다.");

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnClickConfirm);
            confirmButton.interactable = false;
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

        bool nowReady = IsReady();
        if (nowReady != _lastReady)
        {
            _lastReady = nowReady;
            RefreshReadyUI();
        }

        if (!isActiveAndEnabled || _submitting) return;
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
    }

    private void SwitchToInput()
    {
        if (_destroyed) return;

        if (buttonRoot) buttonRoot.SetActive(false);
        if (inputRoot) inputRoot.SetActive(true);
        if (confirmButton) confirmButton.gameObject.SetActive(true);
        if (loadingRoot) loadingRoot.SetActive(false);

        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = maxLength;
            nicknameInput.text = string.Empty;
            SafeSetHint(IsReady() ? $"닉네임을 입력하세요 (최소 {minLength}자)" : "초기화 중... 잠시만 기다려주세요");
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

        int len = (nicknameInput != null ? nicknameInput.text : string.Empty).Trim().Length;
        bool lenOk = len >= minLength;
        bool ready = IsReady();

        if (hintText)
        {
            if (!lenOk) hintText.text = $"닉네임을 입력하세요 (최소 {minLength}자)";
            else hintText.text = ready ? "완료/확인 버튼을 누르거나 잠시 기다리면 연결됩니다" : "초기화 중... 잠시만 기다려주세요";
        }

        if (confirmButton) confirmButton.interactable = lenOk && ready;
    }

    private void OnSubmit(string _)
    {
        if (!_destroyed && nicknameInput != null) TrySubmit(nicknameInput.text);
    }

    private void OnClickConfirm()
    {
        if (!_destroyed && nicknameInput != null) TrySubmit(nicknameInput.text);
    }

    private void TrySubmit(string raw)
    {
        if (_destroyed || _submitting) return;

        if (!IsReady())
        {
            SafeSetHint("초기화 중입니다. 잠시 후 다시 시도하세요.");
            if (confirmButton) confirmButton.interactable = false;
            ActivateInput();
            return;
        }

        string nick = Sanitize(raw);

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
            Debug.Log("asdfasfsafaasdf");
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
        finally
        {
            // RTDBTest.StartTest();
        }
    }

    private void ShowSubmittingUI(bool on)
    {
        if (inputRoot) inputRoot.SetActive(!on);
        if (buttonRoot) buttonRoot.SetActive(false);
        if (confirmButton) confirmButton.gameObject.SetActive(!on);
        if (loadingRoot) loadingRoot.SetActive(on);
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
}