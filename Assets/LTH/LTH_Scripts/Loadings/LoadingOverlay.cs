using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingOverlay : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Slider progressBar;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        statusText = GetComponentInChildren<TMP_Text>();
        progressBar = GetComponentInChildren<Slider>();
    }

    private void Awake()
    {
        // 초기 비활성
        if (canvasGroup) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }
        if (progressBar) progressBar.value = 0f;
        if (statusText) statusText.text = "";
    }

    public void Show(string message = null, float progress = 0f)
    {
        if (statusText && !string.IsNullOrEmpty(message)) statusText.text = message;
        if (progressBar) progressBar.value = Mathf.Clamp01(progress);
        if (canvasGroup) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; canvasGroup.interactable = true; }
        gameObject.SetActive(true);
    }

    public void Set(string message = null, float? progress = null)
    {
        if (statusText && !string.IsNullOrEmpty(message)) statusText.text = message;
        if (progress.HasValue && progressBar) progressBar.value = Mathf.Clamp01(progress.Value);
    }

    public void Hide()
    {
        if (canvasGroup) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }
        gameObject.SetActive(false);
    }
}
