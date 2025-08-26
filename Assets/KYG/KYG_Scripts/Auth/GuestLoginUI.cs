using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KYG.Auth;

public class GuestLoginUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject buttonRoot;        // "Guest Login" 버튼 컨테이너
    [SerializeField] private Button guestLoginButton;      // 클릭 시 입력창으로 전환
    [SerializeField] private GameObject inputRoot;         // TMP_InputField 컨테이너
    [SerializeField] private TMP_InputField nicknameInput; // 실제 닉네임 입력 필드
    [SerializeField] private TextMeshProUGUI hintText;     // “닉네임을 입력하세요” 같은 안내(선택)

    [Header("옵션")]
    [SerializeField] private int minLength = 2;            // 최소 글자수
    [SerializeField] private int maxLength = 16;           // 최대 글자수(GuestLoginManager와 일치)
    [SerializeField] private float idleSubmitSec = 1.0f;   // 타이핑 멈춘 뒤 자동 제출까지 대기 시간

    private float _lastTypeTime;
    private bool _submitting;

    private void Awake()
    {
        // 초기 상태: 버튼 보임 / 입력창 숨김
        ShowButton();
        guestLoginButton.onClick.AddListener(SwitchToInput);
        nicknameInput.onValueChanged.AddListener(OnTyping);
        nicknameInput.onSubmit.AddListener(OnSubmit);   // PC Enter / 모바일 Done 대응
        nicknameInput.onEndEdit.AddListener(OnSubmit);  // 포커스 이탈 시도에도 대응
    }

    private void OnDestroy()
    {
        guestLoginButton.onClick.RemoveListener(SwitchToInput);
        nicknameInput.onValueChanged.RemoveListener(OnTyping);
        nicknameInput.onSubmit.RemoveListener(OnSubmit);
        nicknameInput.onEndEdit.RemoveListener(OnSubmit);
    }

    private void Update()
    {
        if (!inputRoot.activeSelf || _submitting) return;

        // 입력이 있고 일정 시간 타이핑이 멈추면 자동 제출
        if (Time.unscaledTime - _lastTypeTime >= idleSubmitSec)
        {
            TrySubmit(nicknameInput.text);
        }
    }

    // --- UI 전환 ---
    private void SwitchToInput()
    {
        buttonRoot.SetActive(false);
        inputRoot.SetActive(true);

        nicknameInput.characterLimit = maxLength;
        nicknameInput.text = string.Empty;
        hintText?.SetText("닉네임을 입력하세요 (최소 " + minLength + "자)");
        ActivateInput();
    }

    private void ShowButton()
    {
        buttonRoot.SetActive(true);
        inputRoot.SetActive(false);
    }

    private void ActivateInput()
    {
        // 모바일 키보드 즉시 표시
        nicknameInput.Select();
        nicknameInput.ActivateInputField();
    }

    // --- 입력 처리 ---
    private void OnTyping(string _)
    {
        _lastTypeTime = Time.unscaledTime; // 마지막 타이핑 시간 갱신
        // 최소 길이 이전엔 안내만 갱신
        if (hintText)
        {
            int len = nicknameInput.text.Trim().Length;
            hintText.text = len < minLength
                ? $"닉네임을 입력하세요 (최소 {minLength}자)"
                : "완료(Enter/Done)를 누르거나 잠시 기다리면 연결됩니다";
        }
    }

    private void OnSubmit(string _)
    {
        // 사용자가 Enter/Done을 누르거나 포커스를 벗어난 경우 즉시 시도
        TrySubmit(nicknameInput.text);
    }

    private void TrySubmit(string raw)
    {
        if (_submitting) return;

        string nick = Sanitize(raw);
        if (nick.Length < minLength) return; // 아직 조건 미충족 → 대기

        _submitting = true;
        hintText?.SetText("연결 중...");

        // Firebase + Photon 전체 로그인 플로우 호출
        // (GuestLoginManager가 익명 로그인, 닉네임 저장, Photon 접속, 로비 씬 전환까지 처리)
        GuestLoginManager.Instance.LoginAsGuestWithNickname(nick); // :contentReference[oaicite:1]{index=1}
    }

    private string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        if (s.Length > maxLength) s = s.Substring(0, maxLength);
        return s;
    }
}
