using Cysharp.Threading.Tasks;
using InputBlocker;
using LDH_UI;
using Managers;
using ShootingScene;
using UnityEngine;
using UnityEngine.UI;
using PMS_Util;
using UnityEngine.EventSystems;

public class OpenGameSettingPopupButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private Button button;

    private void Awake()
    {
        if (!button) button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }
   
    
    public void OnPointerDown(PointerEventData eventData)
    {
        // 게임 입력을 즉시 UI 모드로 전환(터치 다운 프레임 차단)
        PlayerInputManager.Instance?.ShowPopup();
    }
    
    
    
    private void OnClick() => OnClickAsync().Forget();

    private async UniTask OnClickAsync()
    {
        // 입력 차단
        if (InputManager.Instance != null && InputManager.Instance.IsBlocked(InputType.UI)) return;
        
        if (!button || !button.interactable) return;

        button.interactable = false;
        UI_Popup_GameSetting popup = null;
        System.Action<UI_Base> restoreInput = null;

        try
        {
            popup = Manager.UI.CreatePopupUI<UI_Popup_GameSetting>();
            if (popup == null) return;

            //팝업 닫히면 입력 복원
            popup.OnCloseRequested += _ => PlayerInputManager.Instance?.ClosePopup();
            
            // if (PlayerInputManager.Instance != null)
            // {
            //     PlayerInputManager.Instance.ShowPopup();
            //     restoreInput = (_) => PlayerInputManager.Instance?.ClosePopup();
            //     popup.OnCloseRequested += restoreInput;
            // }

            await Manager.UI.ShowPopupUI(popup);

            // 팝업이 닫힐 때까지 간단 대기
            await UniTask.WaitWhile(() => popup != null && popup.gameObject.activeSelf);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            if (popup != null && restoreInput != null)
                popup.OnCloseRequested -= restoreInput;

            try { PlayerPrefs.Save(); } catch { }

            if (button) button.interactable = true;
        }

        /*var popup = Manager.UI.CreatePopupUI<UI_Popup_GameSetting>();

        // 슈팅게임 PlayerInputManager 차단
        // 팝업이 꺼질때 슈팅게임 PlayerInputManager 차단 해제
        if (PlayerInputManager.Instance != null)
        {
            PlayerInputManager.Instance.ShowPopup();

            popup.OnCloseRequested += (_) =>
            {
                PlayerInputManager.Instance?.ClosePopup();
            };
        }
        
        await Manager.UI.ShowPopupUI(popup);

        // 팝업이 닫힐 때까지 대기
        await UniTask.WaitWhile(() => popup && popup.gameObject.activeSelf);

        PlayerPrefs.Save();

        if (button) button.interactable = true;*/
    }

   
}