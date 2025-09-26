// 예: InGameUIManager.cs
using UnityEngine;

public class InGameUIManager : MonoBehaviour
{
    public static InGameUIManager Instance { get; private set; }

    [SerializeField] private CanvasGroup uiGroup; // 인게임 HUD 전체 묶음

    private void Awake()
    {
        Instance = this;
    }

    public void Show(bool show)
    {
        if (!uiGroup) return;
        uiGroup.alpha = show ? 1f : 0f;
        uiGroup.interactable = show;
        uiGroup.blocksRaycasts = show;
    }
}