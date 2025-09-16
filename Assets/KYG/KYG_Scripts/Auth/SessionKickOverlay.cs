using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SessionKickOverlay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup blocker;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button toTitleButton;
    [SerializeField] private Button quitButton;

    [Header("Config")]
    [SerializeField] private string titleSceneName = "TitleScene"; // 타이틀/로그인 씬명으로 교체

    private void Awake()
    {
        if (toTitleButton) toTitleButton.onClick.AddListener(GoToTitle);
        if (quitButton) quitButton.onClick.AddListener(QuitApp);
        Hide();
    }

    public void Show(string msg = "다른 기기에서 로그인이 감지되어 연결이 종료되었습니다.")
    {
        if (messageText) messageText.text = msg;
        if (blocker)
        {
            blocker.alpha = 1;
            blocker.blocksRaycasts = true;
            blocker.interactable = true;
        }
    }

    public void Hide()
    {
        if (blocker)
        {
            blocker.alpha = 0;
            blocker.blocksRaycasts = false;
            blocker.interactable = false;
        }
    }

    private void GoToTitle() => SceneManager.LoadScene(titleSceneName);

    private void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}