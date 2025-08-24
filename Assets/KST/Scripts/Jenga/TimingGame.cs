using System;
using System.Collections;
using TMPro;
using UnityEngine;
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


    //설정
    [SerializeField] float _limitTime = 5f; //제한시간 -> 5초로 변경하기
    [SerializeField] float _speed = 1.6f; //별 이동 속도
    [SerializeField] float _zoneW; //성공 존 너비
    [SerializeField] float _zoneWRate = 0.45f; //성공 존 너비 비율
    bool _isRun = false; //게임 실행 여부
    float _remainTime; // 잔여 시간
    float _pingPongTimer;
    public event Action<bool, float> OnFinished; //성공여부 이벤트

    void Awake() =>
        Init();

    void Update()
    {
        if (!_isRun) return;

        //스타 이동
        _pingPongTimer += Time.deltaTime;

        float x = Mathf.PingPong(_pingPongTimer / _speed, 1f);
        _slider.value = Mathf.Lerp(0f, 100f, x);

        //TODO 김승태 : 화면 터치 관련 로직(임시), 모바일 버전 터치로 변경 필요.
        if (Input.GetMouseButtonDown(0))
        {
            //사운드 삽입하기.
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

        _isRun = true;

        //TODO 김승태 : 타이밍 게임 BGM 실행

        //성공 존 설정
        _sliderGo.SetActive(true);
        SetSuccesZone();

        //초기화
        _slider.value = 0f;
        _pingPongTimer = 0f;
        _speed = 1.6f;
        _zoneWRate = 0.45f;

        _remainTime = _limitTime;
        gameObject.SetActive(true);

        _timeText.text = Mathf.CeilToInt(_remainTime).ToString();
        StartCoroutine(IE_CountDown());

        Debug.Log($"현재 속도 {_speed} 현재 영역 {_zoneWRate}");
    }

    /// <summary>
    /// 성공존 랜덤 범위 설정
    /// </summary>
    void SetSuccesZone()
    {
        if (!_successZone) return;

        float sliderWidth = _sliderRect.rect.width;

        _zoneW = sliderWidth * _zoneWRate;

        _successZone.sizeDelta = new(_zoneW, _successZone.sizeDelta.y);
        _successZone.anchoredPosition = new((sliderWidth - _zoneW) / 2f, 0f);

        _successZone.gameObject.SetActive(true);
    }

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

    /// <summary>
    /// 슬라이더 위치 값에 따른 성공 존 내부 여부와 정확도 계산 로직
    /// </summary>
    /// <returns>성공 여부 및 정확도 반환.</returns>
    (bool isSuccess, float accuracy) Calculate()
    {
        //슬라이더 폭(px)을 구하기
        float width = _sliderRect.rect.width;

        //슬라이더 핸들 위치를 픽셀 값으로 매핑
        float nowPos = Mathf.Lerp(0f, width, _slider.value / 100f);

        //성공 존 시작, 끝 지점
        float start = _successZone.anchoredPosition.x;
        float end = start + _successZone.rect.width;

        bool isInside = (start <= nowPos) && (nowPos <= end);

        if (!isInside)
            //게임 종료(실패 처리)
            return (false, 0f);

        //정확도 판정하기
        float center = (start + end) * 0.5f;
        float half = (end - start) * 0.5f;
        float acc = 1f - Mathf.Clamp01(Mathf.Abs(nowPos - center) / half);

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

        Debug.Log($"성공 여부 : {isSuccess}, 정확도 : {accuracy}");

        _sliderGo.SetActive(false);
        StartCoroutine(IE_PanelCount(isSuccess));

        //이벤트 퍼블리싱
        OnFinished?.Invoke(isSuccess, accuracy);
    }

    /// <summary>
    /// 성공 혹은 실패 여부를 띄우는 UI
    /// </summary>
    /// <param name="isSuccess">타이밍 미니게임 성공 여부</param>
    /// <returns></returns>
    IEnumerator IE_PanelCount(bool isSuccess)
    {
        _finishPanel.SetActive(true);
        if (isSuccess)
        {
            _finishText.text = "Success!";
            //TODO 김승태 : 성공 SFX 실행
        }
        else
        {
            _finishText.text = "Fail!";
            //TODO 김승태 : 실패 SFX 실행
            //TODO 김승태 : 추후 젠가 실패 애니메이션 추가.
        }

        yield return new WaitForSeconds(2f);

        _finishPanel.SetActive(false);
        _finishText.text = "";

        //비활성화(테스트를 위해 잠시 비활성화)
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 성공 판정 영역 5% 감소(30% 이하로 감소 불가)
    /// 혹은 움직이는 오브젝트의 왕복 시간 0.2초 감소(1초 이하로 감소 불가)
    /// </summary>
    /// <param name="level">난이도 레벨, 높을수록 더욱 어려워짐.</param>
    public void DifficultyChange(int level)
    {
        if (level < 0) return;
        //랜덤으로 아래 중 하나 고르기
        bool type = UnityEngine.Random.Range(0, 2) == 0;
        bool isChnaged = type ? DescSpeed(level) || DescZone(level) : DescZone(level) || DescSpeed(level);

        if (!isChnaged)
            Debug.Log("최고 난이도입니다.");
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
        float temp = before - level * 0.2f;
        float after = Mathf.Clamp(temp, 1f, 1.6f);

        if (after < before)
        {
            _speed = after;
            Debug.Log($"after : {after} before : {before}");
            Debug.Log($"왕복 시간 감소 {_speed}");
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
        float after = Mathf.Clamp(temp, 0.3f, 0.45f);

        if (after < before)
        {
            _zoneWRate = after;
            Debug.Log($"after : {after} before : {before}");
            Debug.Log($"판정 영역 감소 {_zoneWRate}");
            return true;
        }
        return false;
    }


}