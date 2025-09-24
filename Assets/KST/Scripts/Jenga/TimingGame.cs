using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TimingGame : MonoBehaviour
{
    //UI
    [SerializeField] TMP_Text _timeText; //타이머 텍스트
    [SerializeField] Slider _slider; // 슬라이더
    [SerializeField] RectTransform _successZone; // 성공 영역
    [SerializeField] RectTransform _sliderRect;
    [SerializeField] GameObject _sliderGo; // 타이밍 미니게임 Wrap오브젝트
    [SerializeField] GameObject _finishPanel; //성공, 실패 여부 패널
    [SerializeField] TMP_Text _finishText;


    [Header("타이밍 세팅")]
    [SerializeField] float _limitTime = 5f; // 제한시간
    [SerializeField] float _speed = 1.6f; // 별 이동 속도

    [Header("성공 존")]
    [SerializeField]
    float _zoneWRate = 0.45f;                         // 성공존 가로폭 (슬라이더 폭 비율)
    [SerializeField]
    float _zoneHeight = 40f;                          // 성공존 세로 높이(px)


    bool _isRun = false; // 게임 실행 여부
    float _remainTime; // 잔여 시간
    float _pingPongTimer;
    float _zoneW;
    public event Action<bool, float> OnFinished; // 성공여부 이벤트

    void Awake() => Init();

    void Update()
    {
        if (!_isRun) return;

        //스타 이동
        _pingPongTimer += Time.deltaTime;

        float x = Mathf.PingPong(_pingPongTimer / _speed, 1f);
        _slider.value = Mathf.Lerp(0f, 100f, x);

        bool tapped = false;

        tapped = (Mouse.current?.leftButton.wasPressedThisFrame == true)
              || (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true);

        if (tapped) 
        {
            var (ok, acc) = Calculate(); 
            GameEnd(ok, ok ? acc : 0f); 
        }
    }

    private void Init()
    {
        //슬라이더 초기화
        _slider.minValue = 0f;
        _slider.maxValue = 100f;
        _slider.value = 0f;

        //슬라이더 핸들 크기(별 크기) 조절
        RectTransform handle = _slider.handleRect;

        float handleW = _sliderRect.rect.width * 0.15f;
        float handleH = _sliderRect.rect.height * 0.15f;

        handle.sizeDelta = new(handleW, handleH);

        //텍스트 초기화
        _timeText.text = "";

        //성공 존 초기화
        _successZone.gameObject.SetActive(false);
    }

    /// <summary>
    /// 미니 게임 시작
    /// </summary>
    public void GameStart()
    {
        gameObject.SetActive(true);

        if (_isRun) return;

        // 타이밍 게임 시작 사운드
        SoundManager.Instance.PlaySFX("Timing");

        _isRun = true;

        // 성공 존 설정
        _sliderGo.SetActive(true);
        SetSuccesZone();

        // 초기화
        _slider.value = 0f;
        _pingPongTimer = 0f;

        _remainTime = _limitTime;
        _timeText.text = Mathf.CeilToInt(_remainTime).ToString();
    }

    /// <summary>
    /// 성공존 랜덤 범위 설정
    /// </summary>
    void SetSuccesZone()
    {
        if (!_successZone) return;

        // 성공존을 Handle Slide Area 아래로 강제(좌표계 통일)
        var slideArea = _slider.handleRect ? _slider.handleRect.parent as RectTransform : null;
        if (slideArea && _successZone.parent != slideArea)
        {
            _successZone.SetParent(slideArea, worldPositionStays: false);
        }


        float w = slideArea ? slideArea.rect.width : _sliderRect.rect.width;
        _zoneW = w * _zoneWRate;

        // 가로폭은 비율, 세로 높이는 인스펙터 값 사용
        _successZone.sizeDelta = new(_zoneW, _zoneHeight);

        // 중앙 배치
        _successZone.anchoredPosition = Vector2.right * ((w - _zoneW) * 0.5f);

        // 그리기 순서: 성공존 뒤, 핸들 앞
        _successZone.SetSiblingIndex(0);                 // 성공존을 맨 뒤로
        _slider.handleRect.SetAsLastSibling();           // 핸들을 맨 앞으로

        _successZone.gameObject.SetActive(true);
    }

    public IEnumerator IE_CountDownPublic() => IE_CountDown(); // JengaTimingManager에서 코루틴이 접근하도록 수정

    /// <summary>
    /// 카운트 다운 진행
    /// </summary>
    /// <returns>매 프레임 마다</returns>
    IEnumerator IE_CountDown()
    {
        while (_isRun && _remainTime > 0f)
        {
            _remainTime -= Time.deltaTime;
            _timeText.text = Mathf.CeilToInt(Mathf.Max(0f, _remainTime)).ToString();
            yield return null;
        }
        //시간 초과 시
        if (_isRun) GameEnd(false, 0f);
    }


    (bool isSuccess, float accuracy) Calculate()
    {
        var handleGraphic = _slider.handleRect.GetComponentInChildren<Image>()?.rectTransform
                            ?? _slider.handleRect;

        float handleCenterX = GetWorldCenterX(handleGraphic);

        var z = new Vector3[4];
        _successZone.GetWorldCorners(z);
        float start = z[0].x;   // left
        float end   = z[3].x;   // right
        float center = 0.5f * (start + end);
        float half   = 0.5f * (end - start);

        bool inside = (start <= handleCenterX) && (handleCenterX <= end);
        if (!inside) return (false, 0f);

        float acc = 1f - Mathf.Clamp01(Mathf.Abs(handleCenterX - center) / half);

        return (true, acc);
    }

    /// <summary>
    /// 타이밍 미니 게임 종료
    /// </summary>
    /// <param name="isSuccess">성공 여부</param>
    /// <param name="accuracy">정확도 </param>
   void GameEnd(bool isSuccess, float accuracy)
    {
        if (!_isRun) return;
        _isRun = false;

        _sliderGo.SetActive(false);
        StartCoroutine(IE_PanelCount(isSuccess, accuracy));
    }

    /// <summary>
    /// 성공 혹은 실패 여부를 띄우는 UI
    /// </summary>
    /// <param name="isSuccess">타이밍 미니게임 성공 여부</param>
    /// <returns></returns>
    IEnumerator IE_PanelCount(bool isSuccess, float accuracy)
    {
        _finishPanel.SetActive(true);
        if (isSuccess)
        {
            _finishText.text = "성공!";
            // 성공 SFX 실행
            SoundManager.Instance.PlaySFX("Success");
        }
        else
        {
            _finishText.text = "실패...";
            // 실패 SFX 실행
            SoundManager.Instance.PlaySFX("Fail");
        }

        yield return new WaitForSeconds(2f);

        _finishPanel.SetActive(false);
        _finishText.text = "";

        // UI 정리
        _finishPanel.SetActive(false);
        _finishText.text = "";

        gameObject.SetActive(false);

        OnFinished?.Invoke(isSuccess, accuracy);
    }


    public void DifficultyChange(int level)
    {
        //랜덤으로 아래 중 하나 고르기
        bool type = UnityEngine.Random.Range(0, 2) == 0;
        bool isChnaged = type ? DescSpeed(level) || DescZone(level) : DescZone(level) || DescSpeed(level);
    }


    /// <summary>
    /// 왕복 속도 감소 로직
    /// 입력받은 레벨에 맞는 속도 지정
    /// </summary>
    /// <param name="level">난이도 레벨, 높을 수록 왕복 속도가 더 빨라짐.</param>
    /// <returns></returns>
    bool DescSpeed(int level)
    {
        float before = _speed;
        float temp = before - level * 0.25f;
        float after = Mathf.Clamp(temp, 0.6f, 1.6f);

        if (after < before)
        {
            _speed = after;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 판정 영역 감소 로직
    /// 입력받은 레벨에 맞는 판정역역 지정
    /// </summary>
    /// <param name="level">난이도 레벨, 높을 수록 판정 영역이 더 좁아짐.</param>
    /// <returns></returns>
    bool DescZone(int level)
    {
        float before = _zoneWRate;
        float temp = before - level * 0.05f;
        float after = Mathf.Clamp(temp, 0.25f, 0.45f);

        if (after < before)
        {
            _zoneWRate = after;
            return true;
        }
        return false;
    }

    float GetWorldCenterX(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return 0.5f * (c[0].x + c[3].x);
    } 
}