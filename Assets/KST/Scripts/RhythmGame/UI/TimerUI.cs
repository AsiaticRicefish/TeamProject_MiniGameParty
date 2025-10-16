using System;
using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;

namespace RhythmGame
{
    public class TimerUI : MonoBehaviour
    {
        //UI
        [SerializeField] TMP_Text _countDownText; //카운트 다운 텍스트
        [SerializeField] TMP_Text _gameStartText; //게임 시작
        [SerializeField] TMP_Text _timerText; //타이머

        [SerializeField] string _gameStart = "Game Start";
        float _gameStartfloatingTime = 0.4f;
        double _startTime;
        double _endTime;
        bool _isInit;
        GameManager _gm;

        void OnEnable()
        {
            StartCoroutine(IE_DelaySubscribe());
        }

        void OnDisable()
        {
            _gm.OnTimer -= OnTimerInit;
            _gm.OnGameStart -= OnGameStart;
            _gm.OnGameOver -= OnGameOver;

            StopAllCoroutines();
        }

        IEnumerator IE_DelaySubscribe()
        {
            yield return new WaitUntil(() => GameManager.Instance != null);

            //초기값 지정
            _gm = GameManager.Instance;

            _gm.OnTimer += OnTimerInit;
            _gm.OnGameStart += OnGameStart;
            _gm.OnGameOver += OnGameOver;

            InitUI();
        }

        void InitUI()
        {
            _countDownText.gameObject.SetActive(false);
            _gameStartText.gameObject.SetActive(false);
            _timerText.gameObject.SetActive(false);
        }

        void OnGameStart()
        {
            StartCoroutine(IE_ShowGameStart());
        }


        void OnGameOver()
        {
            _countDownText.gameObject.SetActive(false);
            _gameStartText.gameObject.SetActive(false);
            _timerText.gameObject.SetActive(true);
            _timerText.text = "00";

        }

        void OnTimerInit(double startTime, double endTime)
        {
            _startTime = startTime;
            _endTime = endTime;
            _isInit = true;

            StopAllCoroutines();
            StartCoroutine(IE_TimerUI());
        }

        IEnumerator IE_TimerUI()
        {
            if (!_isInit) yield break;

            _countDownText.gameObject.SetActive(true);
            //TODO 김승태 : 카운트다운 사운드 (임시);
            // SoundManager.Instance.PlaySFX_UI(SFX_UI.SFX_Btn1);
            if (SoundManager.Instance == null)
                Debug.LogError("사운드매니저 없음");
            else
            {
                SoundManager.Instance.PlaySFX("321");
            }


            while (PhotonNetwork.Time < _startTime)
            {
                double remain = _startTime - PhotonNetwork.Time;

                int sec = Mathf.CeilToInt((float)remain);

                _countDownText.text = sec.ToString();


                yield return null;
            }
            SoundManager.Instance.StopSFX();
            _countDownText.gameObject.SetActive(false);
            _timerText.gameObject.SetActive(true);

            while (PhotonNetwork.Time < _endTime)
            {
                double remain = _endTime - PhotonNetwork.Time;
                if (remain < 0) remain = 0;

                int sec = Mathf.FloorToInt((float)remain);
                // int mm = sec / 60;
                // int ss = sec % 60;

                // _timerText.text = $"{mm:00}:{ss:00}";
                if (sec <= 10)
                    _timerText.color = Color.red;
                    
                _timerText.text = $"{sec:00}";
                yield return null;
            }

            _timerText.text = "00";
        }

        IEnumerator IE_ShowGameStart()
        {
            _gameStartText.text = _gameStart;
            _gameStartText.gameObject.SetActive(true);

            yield return new WaitForSeconds(_gameStartfloatingTime);
            _gameStartText.gameObject.SetActive(false);


        }

    }
}