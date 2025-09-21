using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Threading.Tasks;

public class NicknamePopup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_InputField input;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private TextMeshProUGUI hint;

    [Header("Optional")]
    [SerializeField] private Button checkBtn;                    // 중복확인 버튼
    [SerializeField] private TextMeshProUGUI checkResultLabel;   // 결과 표시 (선택)
    
    private System.Action<string> _onConfirm;
    private System.Action _onCancel;
    private Func<string, Task<bool>> _onCheck;                   // 가용성 체크 콜백
    
    public void Init(System.Action<string> onConfirm, System.Action onCancel, string placeholder = "",
        Func<string, Task<bool>> onCheck = null)
    {
        _onConfirm = onConfirm;
        _onCancel  = onCancel;
        _onCheck   = onCheck;
        
        if (input)
        {
            input.text = "";
            input.onSubmit.RemoveAllListeners();
            input.onSubmit.AddListener(_ => ClickConfirm());
            input.ActivateInputField();
            input.Select();
            if (!string.IsNullOrEmpty(placeholder))
            {
                var ph = input.placeholder as TMP_Text;
                if (ph) ph.text = placeholder;
            }
        }

        if (confirmBtn)
        {
            confirmBtn.onClick.RemoveAllListeners();
            confirmBtn.onClick.AddListener(ClickConfirm);
        }

        if (cancelBtn)
        {
            cancelBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.AddListener(() =>
            {
                _onCancel?.Invoke();
                Close();
            });
        }

        if (checkBtn)
        {
            checkBtn.onClick.RemoveAllListeners();
            checkBtn.onClick.AddListener(async () =>
            {
                if (_onCheck == null) return;
                var raw = input ? input.text.Trim() : "";
                if (string.IsNullOrEmpty(raw))
                {
                    ShowCheck(false, "닉네임을 입력하세요");
                    return;
                }

                // 비동기 가용성 조회
                try
                {
                    checkBtn.interactable = false;
                    ShowCheck(null, "확인 중...");
                    bool available = await _onCheck(raw);
                    ShowCheck(available, available ? "사용 가능한 닉네임입니다" : "중복된 닉네임입니다");
                }
                finally
                {
                    checkBtn.interactable = true;
                }
            });
        }

        if (hint) hint.text = "";
        if (checkResultLabel) checkResultLabel.text = "";
    }

    private void ClickConfirm()
    {
        var raw = input ? input.text : "";
        _onConfirm?.Invoke(raw);
        // 성공/실패는 호출자(GuestLoginUI)가 처리 → 성공 시 Close 호출, 실패면 유지
    }

    public void ShowError(string msg)
    {
        if (hint)
        {
            hint.color = new Color(1f, .25f, .25f, 1f);
            hint.text = msg;
        }
    }
    
    // available: true/false/unknown(null)
    private void ShowCheck(bool? available, string msg)
    {
        if (checkResultLabel == null) { ShowError(msg); return; }

        checkResultLabel.text  = msg ?? "";
        if (available == null)                 checkResultLabel.color = new Color(1,1,1,0.8f);   // 진행중/기본
        else if (available.Value == true)      checkResultLabel.color = new Color(0.20f,0.85f,0.40f,1f); // 초록
        else                                   checkResultLabel.color = new Color(1.0f,0.25f,0.25f,1f);   // 빨강
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}