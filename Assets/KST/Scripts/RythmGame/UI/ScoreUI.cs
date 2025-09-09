using System.Collections;
using RhythmGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    public class ScoreUI : MonoBehaviour
    {
        //UI
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text _heatscoreText;
        [SerializeField] Slider _heatSlider;
        ScoreManager _sm;

        void OnEnable()
        {
            StartCoroutine(IE_DelaySubscribe());
        }

        void OnDisable()
        {
            _sm.OnScoreChanged -= OnScoreValueChanged;
            _sm.OnOverHeatScoreChanaged -= OnOverHeatValueChanged;
        }

        //초기화 순서 문제 발생 방지를 위해 딜레이 구독
        IEnumerator IE_DelaySubscribe()
        {
            yield return new WaitUntil(() => ScoreManager.Instance != null);

            //초기값 지정
            _sm = ScoreManager.Instance;

            scoreText.text = $"indivisual score : {_sm.Score}";
            _heatscoreText.text = $"OverHeat Score : {_sm.HeatScore}";
            _heatSlider.value = _sm.HeatScore;

            //이벤트 구독
            _sm.OnScoreChanged += OnScoreValueChanged;
            _sm.OnOverHeatScoreChanaged += OnOverHeatValueChanged;
        }

        void OnScoreValueChanged(int value)
        {
            scoreText.text = $"indivisual score : {value}";
        }

        void OnOverHeatValueChanged(int value)
        {
            _heatscoreText.text = $"OverHeat Score : {value}";
            _heatSlider.value = value;
        }
    }
}