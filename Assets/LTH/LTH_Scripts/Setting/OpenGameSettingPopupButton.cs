using Cysharp.Threading.Tasks;
using InputBlocker;
using LDH_UI;
using Managers;
using ShootingScene;
using UnityEngine;
using UnityEngine.UI;

public class OpenGameSettingPopupButton : MonoBehaviour
{
    [SerializeField] private Button button;


    private void Awake()
    {
        if (!button) button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick() => OnClickAsync().Forget();

    private async UniTask OnClickAsync()
    {
        // 입력 차단
        if (InputManager.Instance != null && InputManager.Instance.IsBlocked(InputType.UI)) return;
        
        if (!button || !button.interactable) return;

        button.interactable = false;
        
        var popup = Manager.UI.CreatePopupUI<UI_Popup_GameSetting>();
        
        // 슈팅게임 PlayerInputManager 차단
        // 팝업이 꺼질때 슈팅게임 PlayerInputManager 차단 해제
        if (PlayerInputManager.Instance != null)
        {
            PlayerInputManager.Instance?.DisableAllInput();
            popup.OnCloseRequested += (_) =>
            {
                PlayerInputManager.Instance?.EnableAllInput();
            };
        }
        
        await Manager.UI.ShowPopupUI(popup);

        // 팝업이 닫힐 때까지 대기
        await UniTask.WaitWhile(() => popup && popup.gameObject.activeSelf);

        PlayerPrefs.Save();

        if (button) button.interactable = true;
    }
}