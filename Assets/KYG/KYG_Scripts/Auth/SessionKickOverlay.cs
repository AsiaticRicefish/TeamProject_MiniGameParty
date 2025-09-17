using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 킥 안내 오버레이(전면 차단 + 버튼 보장)
/// - 항상 최상단 Canvas로 동작
/// - Show 동안 씬의 모든 BaseRaycaster(자기 자신 제외)를 비활성화하여 입력 독점
/// - Blocker(풀스크린 반투명) 아래에 버튼이 깔리지 않도록 구조 자동 보정
/// </summary>
[DefaultExecutionOrder(10000)]
public class SessionKickOverlay : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup blocker;        // 풀스크린 차단층 (Image 필요)
    [SerializeField] private RectTransform contentRoot;  // 팝업 내용(텍스트/버튼) 부모
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button toTitleButton;
    [SerializeField] private Button quitButton;

    [Header("Config")]
    [SerializeField] private string titleSceneName = "Login Scene";
    [SerializeField] private int baseSortingOrder = 50000;
    [SerializeField] private bool pauseOnShow = true;

    private Canvas _canvas;
    private GraphicRaycaster _selfRaycaster;
    private Image _fullScreenImage;
    private float _prevTimeScale = 1f;

    // 표시 중 비활성화해 둔 레이캐스터들 기록(복구용)
    private readonly List<Behaviour> _disabledRaycasters = new List<Behaviour>();

    void Awake()
    {
        EnsureCanvasTop();
        EnsureBlocker();
        EnsureContentRoot();
        RescueChildrenFromBlocker(); // 버튼/텍스트가 Blocker 밑에 있으면 Content로 이동
        OrderSiblings();             // Blocker=첫째, Content=막내

        if (toTitleButton) { toTitleButton.interactable = true; toTitleButton.onClick.AddListener(GoToTitle); }
        if (quitButton)    { quitButton.interactable = true;    quitButton.onClick.AddListener(QuitApp);     }

        Hide(); // 초기 숨김
    }

    void OnEnable()
    {
        BringToFront();
        OrderSiblings();
    }

    public void Show(string msg = "다른 기기에서 로그인이 감지되어 연결이 종료되었습니다.")
    {
        if (messageText) messageText.text = msg;

        // 전면 차단 on
        if (blocker)
        {
            blocker.alpha = 1f;
            blocker.blocksRaycasts = true;
            blocker.interactable = true;
        }
        if (_fullScreenImage) _fullScreenImage.raycastTarget = true;

        if (pauseOnShow)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        BringToFront();
        OrderSiblings();

        // (핵심) 다른 모든 Raycaster를 비활성화 → 입력 독점
        FreezeOtherRaycasters(true);

        gameObject.SetActive(true);
        Debug.Log("[SessionKickOverlay] SHOW (input locked)");
    }

    public void Hide()
    {
        // 전면 차단 off
        if (blocker)
        {
            blocker.alpha = 0f;
            blocker.blocksRaycasts = false;
            blocker.interactable = false;
        }
        if (_fullScreenImage) _fullScreenImage.raycastTarget = false;

        if (pauseOnShow) Time.timeScale = _prevTimeScale;

        // (핵심) 레이캐스터 복구
        FreezeOtherRaycasters(false);

        Debug.Log("[SessionKickOverlay] HIDE (input restored)");
    }

    public void BringToFront()
    {
        // 씬 내 최대 sortingOrder + 100 으로 끌어올림
        var canvases = FindObjectsOfType<Canvas>(true);
        int maxOrder = canvases.Select(c => c ? c.sortingOrder : 0).DefaultIfEmpty(baseSortingOrder).Max();
        int target = maxOrder + 100;

        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.targetDisplay = 0;
        _canvas.sortingOrder = target;

        transform.SetAsLastSibling();
    }

    // ===== 내부 보조 =====
    void EnsureCanvasTop()
    {
        _canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = baseSortingOrder;
        _canvas.targetDisplay = 0;

        _selfRaycaster = GetComponent<GraphicRaycaster>() ?? gameObject.AddComponent<GraphicRaycaster>();
        _selfRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
    }

    void EnsureBlocker()
    {
        if (blocker == null)
        {
            var go = new GameObject("Blocker", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);
            blocker = go.GetComponent<CanvasGroup>();
        }
        var rt = blocker.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        _fullScreenImage = blocker.GetComponent<Image>() ?? blocker.gameObject.AddComponent<Image>();
        _fullScreenImage.color = new Color(0, 0, 0, 0.65f);
        _fullScreenImage.raycastTarget = true; // 뒤 UI 차단
    }

    void EnsureContentRoot()
    {
        if (contentRoot == null)
        {
            var go = new GameObject("Content", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            contentRoot = go.GetComponent<RectTransform>();
            contentRoot.anchorMin = new Vector2(0.05f, 0.05f);
            contentRoot.anchorMax = new Vector2(0.95f, 0.35f);
            contentRoot.offsetMin = contentRoot.offsetMax = Vector2.zero;
        }
        if (messageText == null)
        {
            var tgo = new GameObject("Message", typeof(RectTransform), typeof(TMP_Text));
            tgo.transform.SetParent(contentRoot, false);
            var rt = tgo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.55f);
            rt.anchorMax = new Vector2(0.95f, 0.95f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            messageText = tgo.GetComponent<TMP_Text>();
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.fontSize = 32;
            messageText.color = Color.white;
        }
    }

    // 버튼/텍스트가 Blocker 밑에 들어가 있으면 Content로 옮김
    void RescueChildrenFromBlocker()
    {
        if (blocker == null || contentRoot == null) return;
        var btr = blocker.transform;

        if (toTitleButton && toTitleButton.transform.IsChildOf(btr))
            toTitleButton.transform.SetParent(contentRoot, false);
        if (quitButton && quitButton.transform.IsChildOf(btr))
            quitButton.transform.SetParent(contentRoot, false);
        if (messageText && messageText.transform.IsChildOf(btr))
            messageText.transform.SetParent(contentRoot, false);
    }

    // Blocker=첫째, Content=막내 → 버튼이 항상 차단층 위
    void OrderSiblings()
    {
        if (blocker) blocker.transform.SetAsFirstSibling();
        if (contentRoot) contentRoot.SetAsLastSibling();
        if (toTitleButton) toTitleButton.transform.SetAsLastSibling();
        if (quitButton)    quitButton.transform.SetAsLastSibling();
        if (messageText)   messageText.transform.SetAsLastSibling();
    }

    // 오버레이가 떠있는 동안 다른 Raycaster 전부 off → 입력 독점
    void FreezeOtherRaycasters(bool freeze)
    {
        if (freeze)
        {
            _disabledRaycasters.Clear();

            var others = FindObjectsOfType<BaseRaycaster>(true);
            foreach (var r in others)
            {
                if (!r) continue;

                // 자기 자신 또는 자신의 자식은 유지
                if (r == _selfRaycaster) continue;
                if (r.transform.IsChildOf(transform)) continue;

                var bh = r as Behaviour;
                if (bh != null && bh.enabled)
                {
                    bh.enabled = false;
                    _disabledRaycasters.Add(bh);
                }
            }
            Debug.Log($"[SessionKickOverlay] Disabled {_disabledRaycasters.Count} other raycasters.");
        }
        else
        {
            for (int i = 0; i < _disabledRaycasters.Count; i++)
            {
                var bh = _disabledRaycasters[i];
                if (bh) bh.enabled = true;
            }
            _disabledRaycasters.Clear();
        }
    }

    private void GoToTitle()
    {
        if (pauseOnShow) Time.timeScale = _prevTimeScale;

        if (!string.IsNullOrEmpty(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
        else
            Debug.LogError("[SessionKickOverlay] titleSceneName is empty.");
    }

    private void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_ANDROID
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                activity.Call("finishAndRemoveTask"); // 태스크 제거까지
        } catch { /* no-op */ }
        Application.Quit();
#else
        Application.Quit();
#endif
    }
}
