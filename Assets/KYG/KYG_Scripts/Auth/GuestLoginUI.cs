using UnityEngine;
using UnityEngine.UI;
using KYG.Auth;
using TMPro;

public class GuestLoginUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nicknameInput; // TMP_InputField여도 동일 개념
    [SerializeField] private Button loginButton;

    private void Awake()
    {
        loginButton.onClick.AddListener(OnClickLogin);
    }

    private void OnDestroy()
    {
        loginButton.onClick.RemoveListener(OnClickLogin);
    }

    private void OnClickLogin()
    {
        var nick = nicknameInput.text;
        GuestLoginManager.Instance.LoginAsGuestWithNickname(nick);
    }
}