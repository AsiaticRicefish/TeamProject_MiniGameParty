using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Threading.Tasks;
using System.Collections; // ← 코루틴 사용

/// <summary>
/// 닉네임 입력 팝업 UI
/// - "중복확인"을 통과해야만 "확인" 버튼이 활성화됩니다.
/// - 입력값이 변경되면 통과 상태가 자동으로 무효화되어 다시 중복확인을 요구합니다.
/// - onCheck 콜백은 외부(GuestLoginUI 등)에서 실제 중복 조회(Firebase/서버)를 수행하여 bool로 반환합니다.
/// </summary>
public class NicknamePopup : MonoBehaviour
{
    [Header("Refs (필수 UI 참조)")]
    [SerializeField] private TMP_InputField input;            // 닉네임 입력칸
    [SerializeField] private Button        confirmBtn;        // 확인(생성) 버튼
    [SerializeField] private Button        cancelBtn;         // 취소 버튼
    [SerializeField] private TextMeshProUGUI hint;            // 에러/안내 라벨

    [Header("Optional (있으면 사용)")]
    [SerializeField] private Button        checkBtn;          // 중복확인 버튼
    [SerializeField] private TextMeshProUGUI checkResultLabel;// 중복확인 결과 표시 라벨

    [Header("Rule")]
    [Tooltip("ON이면 중복확인을 통과해야 확인 버튼이 활성화됩니다.")]
    [SerializeField] private bool requireDuplicateCheck = true;
    
    [Header("Auto Hide (메시지 n초 뒤 자동 숨김)")]
    [SerializeField] private bool autoHideMessages = true;   // 전체 자동 숨김 ON/OFF
    [SerializeField] private bool autoHideOnError   = true;   // 에러류 자동 숨김
    [SerializeField] private bool autoHideOnInfo    = true;   // 안내/진행중 자동 숨김
    [SerializeField] private bool autoHideOnSuccess = true;   // 성공(가용) 자동 숨김
    [SerializeField] private float errorHideSec     = 2.0f;   // 에러 문구 유지 시간
    [SerializeField] private float infoHideSec      = 1.5f;   // 안내/진행중 유지 시간
    [SerializeField] private float successHideSec   = 1.5f;   // 성공 유지 시간
    [SerializeField] private bool  fadeOut          = true;   // 사라질 때 페이드아웃
    [SerializeField] private float fadeOutSec       = 0.25f;  // 페이드 시간

    // ──────────────────────────────────────────────────────────
    // 외부에서 넘겨줄 콜백들
    private Action<string> _onConfirm;              // "확인" 눌렀을 때 호출 (정제/검증은 바깥에서)
    private Action         _onCancel;               // "취소" 눌렀을 때 호출
    private Func<string, Task<bool>> _onCheck;      // "중복확인" 로직 (비동기)

    // 내부 상태: "최근 중복확인 통과 여부"와 "그때의 입력값"을 기억하여 입력 변경을 감지
    private bool   _dupCheckedOk;                   // 최근 검사 통과 여부
    private string _lastDupCheckedName;             // 최근 검사를 수행했던 입력값(변경되면 무효)
    
    private Coroutine _hintHideCo;          // hint 자동 숨김 코루틴
    private Coroutine _checkHideCo;         // checkResultLabel 자동 숨김 코루틴

    /// <summary>
    /// 팝업 초기화 (외부에서 호출)
    /// </summary>
    public void Init(Action<string> onConfirm, Action onCancel, string placeholder = "",
                     Func<string, Task<bool>> onCheck = null)
    {
        _onConfirm = onConfirm;
        _onCancel  = onCancel;
        _onCheck   = onCheck;

        // === 입력칸 세팅 ===
        if (input)
        {
            input.text = "";
            input.onSubmit.RemoveAllListeners();
            input.onSubmit.AddListener(_ => ClickConfirm()); // 엔터로도 확인 시도

            // ✅ 핵심: 입력값이 바뀌면 "중복확인 통과" 상태를 무효화
            input.onValueChanged.RemoveAllListeners();
            input.onValueChanged.AddListener(_ => InvalidateDupCheck());

            input.ActivateInputField();
            input.Select();

            if (!string.IsNullOrEmpty(placeholder))
            {
                var ph = input.placeholder as TMP_Text;
                if (ph) ph.text = placeholder;
            }
        }

        // === 확인 버튼 ===
        if (confirmBtn)
        {
            confirmBtn.onClick.RemoveAllListeners();
            confirmBtn.onClick.AddListener(ClickConfirm);

            // 시작 시: "중복확인 필요"라면 비활성화, 아니면 활성화
            confirmBtn.interactable = !requireDuplicateCheck;
        }

        // === 취소 버튼 ===
        if (cancelBtn)
        {
            cancelBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.AddListener(() =>
            {
                _onCancel?.Invoke();
                Close();
            });
        }

        // === 중복확인 버튼 ===
        if (checkBtn)
        {
            checkBtn.onClick.RemoveAllListeners();
            checkBtn.onClick.AddListener(async () =>
            {
                if (_onCheck == null)
                {
                    ShowCheck(false, "중복확인 기능이 준비되지 않았습니다.");
                    return;
                }

                string raw = input ? (input.text ?? "").Trim() : "";
                if (string.IsNullOrEmpty(raw))
                {
                    ShowCheck(false, "닉네임을 먼저 입력하세요.");
                    if (confirmBtn) confirmBtn.interactable = false;
                    return;
                }

                // 진행중 UI
                ShowCheck(null, "중복 검사중...");
                if (confirmBtn) confirmBtn.interactable = false;
                checkBtn.interactable = false;

                bool available = false;
                bool handled = false; // ← 이 플래그가 true면 아래 "중복" 문구를 표시하지 않음
                try
                {
                    available = await _onCheck(raw); // true=사용가능 / false=중복
                }
                catch (KYG.NicknameInvalidException nie)
                {
                    ShowError(nie.Message);          // 규칙 위반 → 에러 라벨만 표시
                    available = false;
                    handled = true;
                }
                catch (KYG.NicknameTimeoutException te)
                {
                    ShowError(te.Message);           // 타임아웃 → 에러 라벨만 표시
                    available = false;
                    handled = true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    ShowError("검사 중 오류가 발생했습니다.");
                    available = false;
                    handled = true;
                }
                finally
                {
                    checkBtn.interactable = true;
                }

                // 내부 상태 기록
                _dupCheckedOk = available;
                _lastDupCheckedName = available ? raw : null;

                // ✅ 결과 표시: "중복" 문구는 진짜 중복일 때만!
                if (available)
                {
                    ShowCheck(true, "사용 가능한 닉네임입니다.\n 확인을 눌러 진행하세요.");
                    if (requireDuplicateCheck && confirmBtn) confirmBtn.interactable = true;
                }
                else
                {
                    if (!handled)
                    {
                        // false지만 규칙 위반/타임아웃이 아닌 "진짜 중복"만 여기로 옴
                        ShowCheck(false, "중복된 닉네임 입니다. \n 다른 이름을 입력하세요.");
                    }
                    if (confirmBtn) confirmBtn.interactable = false;
                }
            });
        }

        // 초기 메시지 정리
        if (hint) hint.text = "";
        if (checkResultLabel) checkResultLabel.text = "";

        if (requireDuplicateCheck)
        {
            ShowCheck(null, "중복확인을 눌러 \n 닉네임을 확인하세요.");
            if (confirmBtn) confirmBtn.interactable = false;
        }
    }
    
    // 라벨 알파를 즉시 1로 복구 (새 문구 보일 때 또렷하게)
    private void ResetAlpha(TextMeshProUGUI lab)
    {
        if (!lab) return;
        var c = lab.color; c.a = 1f; lab.color = c;
    }

// 코루틴 재시작 헬퍼
    private void RestartHide(ref Coroutine handle, TextMeshProUGUI lab, float delay)
    {
        if (!autoHideMessages || !lab) return;
        if (handle != null) StopCoroutine(handle);
        handle = StartCoroutine(CoHideLabel(lab, delay));
    }

// 실제 숨김(지연→페이드→지우기)
    private IEnumerator CoHideLabel(TextMeshProUGUI lab, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (fadeOut && fadeOutSec > 0f && lab)
        {
            float t = 0f;
            var baseCol = lab.color;
            while (t < fadeOutSec)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(1f, 0f, t / fadeOutSec);
                lab.color = new Color(baseCol.r, baseCol.g, baseCol.b, a);
                yield return null;
            }
        }

        if (lab)
        {
            lab.text = "";
            // 다음 표시를 위해 알파 원복
            var c = lab.color; c.a = 1f; lab.color = c;
        }
    }

    /// <summary>
    /// 확인 버튼 클릭 처리
    /// - 중복확인이 필요한 경우, 통과 상태가 아니면 막습니다.
    /// - 통과 후 입력을 수정한 경우(검사 당시와 문자열 불일치)도 막습니다.
    /// </summary>
    private void ClickConfirm()
    {
        string raw = input ? (input.text ?? "").Trim() : "";

        if (requireDuplicateCheck)
        {
            // 1) 아직 통과 못했거나,
            // 2) 통과 이후에 입력이 바뀐 경우 → 거절
            if (!_dupCheckedOk || !string.Equals(raw, _lastDupCheckedName, StringComparison.Ordinal))
            {
                ShowError("중복확인을 먼저 통과하세요.");
                if (confirmBtn) confirmBtn.interactable = false;
                return;
            }
        }

        // 여기서부터는 외부 흐름(GuestLoginUI)이 실제 가입/로그인을 진행
        _onConfirm?.Invoke(raw);
        // 성공 시 외부에서 Close() 호출, 실패 시 팝업 유지
    }

    /// <summary>
    /// 입력값이 변경되었을 때 호출되어, 이전 중복확인 결과를 무효화합니다.
    /// </summary>
    private void InvalidateDupCheck()
    {
        _dupCheckedOk = false;
        _lastDupCheckedName = null;

        if (requireDuplicateCheck && confirmBtn)
            confirmBtn.interactable = false;

        ShowCheck(null, "입력이 변경되었습니다. \n 중복확인을 다시 진행하세요.");
    }

    // ────────────────── UI 헬퍼 ──────────────────
    public void ShowError(string msg, float? customHideSec = null)
    {
        if (!hint) return;
        hint.color = new Color(1f, .25f, .25f, 1f); // 빨강
        hint.text  = msg ?? "";
        ResetAlpha(hint);

        if (autoHideOnError)
        {
            float sec = customHideSec.HasValue ? Mathf.Max(0f, customHideSec.Value) : errorHideSec;
            RestartHide(ref _hintHideCo, hint, sec);
        }
    }

    /// <summary>
    /// 중복확인 상태 표시: available = true(가용)/false(중복)/null(진행중·안내)
    /// </summary>
    // available: true(가용)/false(중복)/null(진행중/안내)
// customHideSec: 특정 상황에서 유지 시간을 개별 지정하고 싶을 때
    private void ShowCheck(bool? available, string msg, float? customHideSec = null)
    {
        if (checkResultLabel == null) { ShowError(msg, customHideSec); return; }

        checkResultLabel.text = msg ?? "";
        ResetAlpha(checkResultLabel);

        Color col;
        float defaultSec;
        bool doHide;

        if (available == null)
        {
            col = new Color(1, 1, 1, 0.85f); // 안내/진행중
            defaultSec = infoHideSec;
            doHide = autoHideOnInfo;
        }
        else if (available.Value)
        {
            col = new Color(0.20f, 0.85f, 0.40f, 1f); // 성공(사용 가능)
            defaultSec = successHideSec;
            doHide = autoHideOnSuccess;
        }
        else
        {
            col = new Color(1.0f, 0.25f, 0.25f, 1f); // 에러(중복)
            defaultSec = errorHideSec;
            doHide = autoHideOnError;
        }

        checkResultLabel.color = col;

        if (doHide)
        {
            float sec = customHideSec.HasValue ? Mathf.Max(0f, customHideSec.Value) : defaultSec;
            RestartHide(ref _checkHideCo, checkResultLabel, sec);
        }
    }

    public void Close() => Destroy(gameObject);
}
