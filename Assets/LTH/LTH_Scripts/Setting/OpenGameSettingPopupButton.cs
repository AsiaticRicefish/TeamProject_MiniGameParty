using Cysharp.Threading.Tasks;
using LDH_UI;
using Managers;
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
        if (!button || !button.interactable) return;

        SoundManager.Instance?.PlaySFX("Click");

        button.interactable = false;

        var popup = Manager.UI.CreatePopupUI<UI_Popup_GameSetting>();
        await Manager.UI.ShowPopupUI(popup);

        // 팝업이 닫힐 때까지 대기
        await UniTask.WaitWhile(() => popup && popup.gameObject.activeSelf);

        if (button) button.interactable = true;
    }
}