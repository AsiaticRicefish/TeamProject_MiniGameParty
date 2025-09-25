using System;
using System.Threading;
using Customization;
using Cysharp.Threading.Tasks;
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
    [SerializeField] private Image bg;
    [SerializeField] private Image colorChip;
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private Image profileImage;
    
    
    [Header("First Place Icon")]
    [SerializeField] private Image trophyIcon;   // 트로피 아이콘
    [SerializeField] private float trophyFade = 0.15f;
    [SerializeField] private float trophyPunch = 0.18f;
    private Tween _trophyTween;
    private bool _isFirstShown; // 현재 트로피 표시 상태 캐시 (계속 확대가 되는 현상 방지)

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

    [Header("FX Root (펀치/연출 전용)")]
    [SerializeField] private Transform fxRoot;
    private float _fxBaseScale = 1f;

    private RectTransform _rt;
    private float _baseScale = 1f;

    private Tween _fadeTween, _punchTween, _flashTween, _iconSeqTween;

    bool UseBgAsChip => colorChip == null || colorChip == bg;

    private string _profileId;
    
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

        _isFirstShown = false;

        if (!fxRoot)
        {
            fxRoot = transform; // 없으면 자기 자신 사용(점진 전환)
        }

        _fxBaseScale = fxRoot.localScale.x;

        if (deltaIcon)
        {
            var col = deltaIcon.color; col.a = 0f;
            deltaIcon.color = col;
        }

        if (trophyIcon)
        {
            var c = trophyIcon.color; c.a = 0f;
            trophyIcon.color = c;
            trophyIcon.transform.localScale = Vector3.one;
        }

        if (bg) bg.color = normalColor;
    }

    public void SetContent(int rank)
    {
        if (rankText) rankText.text = rank.ToString();
        if (bg && !UseBgAsChip) bg.color = normalColor;
        
    }

    public async void SetProfile(string profileId)
    {
        // 프로필이 없거나 동일 키면 아이콘 로드 생략
        if (!profileImage || string.IsNullOrEmpty(profileId) ||
            string.Equals(profileId, _profileId, System.StringComparison.Ordinal))
            return;
        
        _profileId = profileId; 
        
        try
        {
            var sprite = await CustomizationManager.Instance.GetIconAsync(profileId);
            Debug.Log( "sprite is null? " + (sprite==null));
            // 메인 스레드에서 UI 변경하기
            await UniTask.SwitchToMainThread();
            
            if (!this || !profileImage) return;
            profileImage.sprite = sprite;
            profileImage.enabled = true;
            
            Debug.Log($"profile image sprite = {profileImage.sprite.name}");

        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[RankingRow] Icon load failed ({profileId}): {e.Message}");
        }
    }

    // === 실시간용: 이름은 그대로, 순위 숫자만 '플립' 후 셋 ===
    public void SetRankAnimated(int newRank)
    {
        if (!rankText) return;

        Sequence seq = DOTween.Sequence();
        seq.Append(rankText.transform.DOScaleY(0f, 0.12f))
           .AppendCallback(() =>
           {
               rankText.text = newRank.ToString();
               if (bg && !UseBgAsChip) bg.color = normalColor;
               SetFirstPlace(newRank == 1);
           })
           .Append(rankText.transform.DOScaleY(1f, 0.12f));
    }

    public void SetFirstPlace(bool isFirst)
    {
        if (!trophyIcon) return;

        // 상태 변화 없으면 무시
        if (_isFirstShown == isFirst) return;
        _isFirstShown = isFirst;

        _trophyTween?.Kill(false);
        // 다음 트윈을 항상 동일 기준에서 시작 (계속 확대가 되면 안됨)
        trophyIcon.transform.localScale = Vector3.one;

        if (isFirst)
        {
            _trophyTween = DOTween.Sequence()
                .Append(trophyIcon.DOFade(1f, trophyFade))
                .Join(trophyIcon.transform.DOPunchScale(Vector3.one * trophyPunch, 0.25f, 8, 0.9f));
        }
        else
        {
            _trophyTween = trophyIcon.DOFade(0f, trophyFade);
        }
    }

    public void SetColor(Color c)
    {
        if (colorChip && !UseBgAsChip)
        {
            colorChip.color = new Color(c.r, c.g, c.b, colorChip.color.a);
        }
        else if (bg) // bg 자체를 칠함
        {
            bg.color = new Color(c.r, c.g, c.b, bg.color.a);
        }
    }

    public void HighlightMe(bool on)
    {
        if (!bg) return;
        var c = bg.color;
        c.a = on ? 1f : 0.85f;
        bg.color = c;
    }

    public void EmphasizeFirstPlace()
    {
        KillFx();
        if (cg) cg.alpha = 1f;

        fxRoot.localScale = Vector3.one * _fxBaseScale;
        _punchTween = fxRoot.DOPunchScale(Vector3.one * punchScale, punchDuration, 10, 0.9f);
    }

    public void PlayDeltaFx(int delta, int newRank)
    {
        if (!bg) return;
        var flash = delta > 0 ? upColor : downColor;
        var origin = (!UseBgAsChip) ? normalColor : bg.color;

        bg.color = flash;
        _flashTween?.Kill();
        _flashTween = DOVirtual.Color(flash, origin, Mathf.Max(0.05f, deltaFlash), c => { if (bg) bg.color = c; });

        if (punchScale > 0f)
        {
            _punchTween?.Kill(false);
            fxRoot.localScale = Vector3.one * _fxBaseScale;
            _punchTween = fxRoot.DOPunchScale(Vector3.one * punchScale, punchDuration, 8, 0.8f);
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
        if (bg && !UseBgAsChip) bg.color = normalColor;
        SetFirstPlace(false);
    }

    private void OnDisable() => KillAllTweens();

    private void KillFx()
    {
        _fadeTween?.Kill(); 
        _punchTween?.Kill();
        _fadeTween = _punchTween = null;
        if (fxRoot) fxRoot.localScale = Vector3.one * _fxBaseScale;
    }
    private void KillFlash() { _flashTween?.Kill(); _flashTween = null; }
    private void KillAllTweens()
    {
        KillFx(); KillFlash();
        _iconSeqTween?.Kill(); _iconSeqTween = null;
        _trophyTween?.Kill(); _trophyTween = null;
    }
}