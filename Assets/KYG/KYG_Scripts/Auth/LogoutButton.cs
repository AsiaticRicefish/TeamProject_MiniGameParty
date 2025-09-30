// LogoutButton.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LogoutButton : MonoBehaviour
{
    [SerializeField] private string loginSceneName = "Login Scene";
    [SerializeField] private Button button; // (선택) 연결 시 중복 클릭 막기

    public async void OnClick_Logout()
    {
        if (button) button.interactable = false;

        // 1) 모든 계정/연결 정리 + 자동로그인 억제 마킹
        await AuthAccount.SignOutAllAsync(releaseGuestNickname: false);

        // 2) 짧은 한 프레임 양보(디바이스에 따라 Firebase StateChanged 전파 보정)
        await System.Threading.Tasks.Task.Yield();

        // 3) 로그인 씬으로 이동(이때 부트스트랩은 Consume() 때문에 대기)
        SceneManager.LoadScene(loginSceneName);
    }
}