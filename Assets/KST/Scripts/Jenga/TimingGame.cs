using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimingGame : MonoBehaviour
{
    //UI
    [SerializeField] TMP_Text _timeText; //Ÿ�̸� �ؽ�Ʈ
    [SerializeField] Slider _slider; // �����̴�
    [SerializeField] RectTransform _successZone; // ���� ����
    [SerializeField] RectTransform _sliderRect;

    //����
    float _limitTime = 1000f; //���ѽð�
    float _speed; //�� �̵� �ӵ�
    [SerializeField] Vector2 speedRange = new(0.4f, 0.8f); //�� �̵� �ӵ� ����
    [SerializeField] Vector2 _succesZoneSize = new(100f, 200f); //���� ���� ������

    public event Action<bool, float> OnFinished; //�������� �̺�Ʈ

    bool _isRun = false; //���� ���� ����
    float _remainTime; // �ܿ� �ð�
    float _pingPongTimer;

    void Awake() =>
        Init();

    void Update()
    {
        if (!_isRun) return;

        //��Ÿ �̵�
        _pingPongTimer += Time.deltaTime;

        float x = Mathf.PingPong(_pingPongTimer / _speed, 1f);
        _slider.value = Mathf.Lerp(0f, 100f, x);

        //TODO ����� : ȭ�� ��ġ ���� ����(�ӽ�)
        if (Input.GetMouseButtonDown(0))
        {
            //���� �����ϱ�.
            var (ok, acc) = Calculate();
            GameEnd(ok, ok ? acc : 0f);
        }        
    }

    private void Init()
    {
        //�����̴� �ʱ�ȭ
        _slider.minValue = 0f;
        _slider.maxValue = 100f;
        _slider.value = 0f;

        //�ؽ�Ʈ �ʱ�ȭ
        _timeText.text = "";

        //���� �� �ʱ�ȭ
        _successZone.gameObject.SetActive(false);
    }

    /// <summary>
    /// �̴� ���� ����
    /// </summary>
    public void GameStart()
    {
        if (_isRun) return;

        _isRun = true;

        //���� �����ϱ�

        SetSuccesZone();

        //�ʱ�ȭ
        _slider.value = 0f;
        _pingPongTimer = 0f;
        _speed = UnityEngine.Random.Range(speedRange.x, speedRange.y);

        _remainTime = _limitTime;
        gameObject.SetActive(true);

        _timeText.text = Mathf.CeilToInt(_remainTime).ToString();
        StartCoroutine(IE_CountDown());
    }

    /// <summary>
    /// ������ ���� ���� ����
    /// </summary>
    void SetSuccesZone()
    {
        if (!_successZone) return;

        float sliderWidth = _sliderRect.rect.width;

        //���� ���� ���� �� ����
        float zoneWidth = Mathf.Clamp(
            UnityEngine.Random.Range(_succesZoneSize.x, _succesZoneSize.y),
            _succesZoneSize.x, sliderWidth);

        //���� �� ���� x ��ǥ ����
        float start = UnityEngine.Random.Range(0f, sliderWidth - zoneWidth);

        //������ ���� �� ����
        _successZone.sizeDelta = new(zoneWidth, _successZone.sizeDelta.y);

        //���� �� ���� �𼭸��� start�� ��ġ�� �°� ����
        _successZone.anchoredPosition = new(start, 0f);

        _successZone.gameObject.SetActive(true);
    }

    /// <summary>
    /// ī��Ʈ �ٿ� ����
    /// </summary>
    /// <returns>�� ������ ����</returns>
    IEnumerator IE_CountDown()
    {
        while (_isRun && _remainTime > 0f)
        {
            _remainTime -= Time.deltaTime;
            _timeText.text = Mathf.CeilToInt(Mathf.Max(0f, _remainTime)).ToString();
            yield return null;
        }
        //�ð� �ʰ� ��
        if (_isRun) GameEnd(false, 0f);
    }

    /// <summary>
    /// �����̴� ��ġ ���� ���� ���� �� ���� ���ο� ��Ȯ�� ��� ����
    /// </summary>
    /// <returns>���� ���� �� ��Ȯ�� ��ȯ.</returns>
    (bool isSuccess, float accuracy) Calculate()
    {
        //�����̴� ��(px)�� ���ϱ�
        float width = _sliderRect.rect.width;
        
        //�����̴� �ڵ� ��ġ�� �ȼ� ������ ����
        float nowPos = Mathf.Lerp(0f, width, _slider.value / 100f);

        //���� �� ����, �� ����
        float start = _successZone.anchoredPosition.x;
        float end = start + _successZone.rect.width;

        bool isInside = (start <= nowPos) && (nowPos <= end);

        if (!isInside)
        {
            //���� ����(���� ó��)
            return (false, 0f);
        }

        //��Ȯ�� �����ϱ�
        float center = (start + end) * 0.5f;
        float half = (end - start) * 0.5f;
        float acc = 1f - Mathf.Clamp01(Mathf.Abs(nowPos - center) / half);

        return (true, acc);
    }

    /// <summary>
    /// Ÿ�̹� �̴� ���� ����
    /// </summary>
    /// <param name="isSuccess">���� ����</param>
    /// <param name="accuracy">��Ȯ�� </param>
    void GameEnd(bool isSuccess, float accuracy)
    {
        if (!_isRun) return;
        _isRun = false;


        //�̺�Ʈ �ۺ���
        OnFinished?.Invoke(isSuccess, accuracy);

        Debug.Log($"���� ���� : {isSuccess}, ��Ȯ�� : {accuracy}");

        //��Ȱ��ȭ(�׽�Ʈ�� ���� ��� ��Ȱ��ȭ)
        // gameObject.SetActive(false);
    }


}