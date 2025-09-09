using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단일 랭킹 행 UI
/// </summary>
[DisallowMultipleComponent]
public class RankingRow : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image bg;
    [SerializeField] private CanvasGroup cg;

    [Header("Delta Icon (▲/▼)")]
    [SerializeField] private Image deltaIcon;           // 아이콘 이미지(작은 화살표)
    [SerializeField] private Sprite upSprite;           // 순위 상승(▲)
    [SerializeField] private Sprite downSprite;         // 순위 하락(▼)
    [SerializeField] private float iconShowTime = 1f; // 표시 시간
    [SerializeField] private float iconMoveY = 12f;     // 살짝 위로 이동 연출

    [Header("Style")]
    [SerializeField] private Color normalColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
    [SerializeField] private Color firstColor = new Color(0.98f, 0.82f, 0.25f, 1f);

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float punchScale = 0.15f;
    [SerializeField] private float punchDuration = 0.40f;

    [Header("Delta FX")]
    [SerializeField] private Color upColor = new Color(0.30f, 0.85f, 0.40f, 1f);
    [SerializeField] private Color downColor = new Color(0.95f, 0.30f, 0.30f, 1f);
    [SerializeField] private float deltaFlash = 0.25f;

    private RectTransform _rt;
    private float _baseScale = 1f;

    private Tween _fadeTween, _punchTween, _flashTween, _iconSeqTween;

    public string CurrentNickname => nameText ? nameText.text : string.Empty;
    public int CurrentRank
    {
        get
        {
            if (!rankText) return 0;
            return int.TryParse(rankText.text, out var r) ? r : 0;
        }
    }

    private void Awake()
    {
        _rt = (RectTransform)transform;
        if (!cg) cg = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _baseScale = transform.localScale.x;
        ResetView();

        if (deltaIcon)
        {
            var col = deltaIcon.color; col.a = 0f;
            deltaIcon.color = col;
        }
    }

    public void SetContent(int rank, string nickname)
    {
        if (rankText) rankText.text = rank.ToString();
        if (nameText) nameText.text = nickname;
        if (bg) bg.color = (rank == 1) ? firstColor : normalColor;
    }

    // === 실시간용: 이름은 그대로, 순위 숫자만 '플립' 후 셋 ===
    public void SetRankAnimated(int newRank)
    {
        if (!rankText) return;

        // 숫자 플립: ScaleY 1→0 (변경) → 0→1
        Sequence seq = DOTween.Sequence();
        seq.Append(rankText.transform.DOScaleY(0f, 0.12f))
           .AppendCallback(() =>
           {
               rankText.text = newRank.ToString();
               if (bg) bg.color = (newRank == 1) ? firstColor : normalColor;
           })
           .Append(rankText.transform.DOScaleY(1f, 0.12f));
    }

    public void SetName(string nickname)
    {
        if (nameText) nameText.text = nickname;
    }

    public void EmphasizeFirstPlace()
    {
        KillFx();
        cg.alpha = 0f;
        _fadeTween = cg.DOFade(1f, Mathf.Max(0.01f, fadeInDuration));
        transform.localScale = Vector3.one * _baseScale;
        _punchTween = transform.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 10, elasticity: 0.9f);
    }

    public void PlayDeltaFx(int delta, int newRank)
    {
        if (!bg) return;
        var flash = delta > 0 ? upColor : downColor;
        var origin = (newRank == 1) ? firstColor : normalColor;

        bg.color = flash;
        _flashTween?.Kill();
        _flashTween = DOVirtual.Color(flash, origin, Mathf.Max(0.05f, deltaFlash), c => { if (bg) bg.color = c; });

        if (punchScale > 0f)
        {
            _punchTween?.Kill();
            _punchTween = transform.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 8, elasticity: 0.8f);
        }
    }

    public void ShowDeltaIcon(int delta)
    {
        if (!deltaIcon || delta == 0) return;

        _iconSeqTween?.Kill();
        deltaIcon.sprite = delta > 0 ? upSprite : downSprite;

        var rt = (RectTransform)deltaIcon.transform;
        var startPos = rt.anchoredPosition;

        var col = deltaIcon.color; col.a = 0f; deltaIcon.color = col;

        var seq = DOTween.Sequence();
        seq.Append(deltaIcon.DOFade(1f, 0.12f));
        seq.Join(rt.DOAnchorPosY(startPos.y + iconMoveY, 0.12f));
        seq.AppendInterval(iconShowTime);
        seq.Append(deltaIcon.DOFade(0f, 0.15f));
        seq.Join(rt.DOAnchorPosY(startPos.y, 0.15f));
        _iconSeqTween = seq;
    }

    public void ResetView()
    {
        if (cg) cg.alpha = 1f;
        transform.localScale = Vector3.one * _baseScale;
    }

    private void OnDisable() => KillAllTweens();

    private void KillFx()
    {
        _fadeTween?.Kill(); _punchTween?.Kill();
        _fadeTween = _punchTween = null;
    }
    private void KillFlash() { _flashTween?.Kill(); _flashTween = null; }
    private void KillAllTweens()
    {
        KillFx(); KillFlash();
        _iconSeqTween?.Kill(); _iconSeqTween = null;
    }
}