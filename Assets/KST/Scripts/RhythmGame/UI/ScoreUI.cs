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
        ScoreManager _sm;
        [SerializeField] TMP_Text scoreText;
        // [SerializeField] TMP_Text _heatscoreText;
        [SerializeField] Slider _heatSlider;

        //콤보
        [SerializeField] GameObject _comboGO;
        [SerializeField] TMP_Text _comboText;
        [SerializeField] TMP_Text _verdictScoreText;
        [SerializeField] TMP_Text _verdictText;

        void OnEnable()
        {
            StartCoroutine(IE_DelaySubscribe());
        }

        void OnDisable()
        {
            _sm.OnScoreChanged -= OnScoreValueChanged;
            // _sm.OnOverHeatScoreChanaged -= OnOverHeatValueChanged;
        }

        //초기화 순서 문제 발생 방지를 위해 딜레이 구독
        IEnumerator IE_DelaySubscribe()
        {
            yield return new WaitUntil(() => ScoreManager.Instance != null);

            //초기값 지정
            _sm = ScoreManager.Instance;

            scoreText.text = $"indivisual score : {_sm.Score}";
            // _heatscoreText.text = $"OverHeat Score : {_sm.HeatScore}";
            _heatSlider.value = _sm.HeatScore;
            _verdictText.text = "";
            _comboText.text = "";
            _verdictScoreText.text = "";

            //이벤트 구독
            _sm.OnScoreChanged += OnScoreValueChanged;
            // _sm.OnOverHeatScoreChanaged += OnOverHeatValueChanged;
            _sm.OnVerdict += OnVerdict;
        }

        void OnScoreValueChanged(int value)
        {
            scoreText.text = $"indivisual score : {value}";
        }

        // void OnOverHeatValueChanged(int value)
        // {
        //     _heatscoreText.text = $"OverHeat Score : {value}";
        //     _heatSlider.value = value;
        // }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="verdict">판정 </param>
        /// <param name="combo"></param>
        /// <param name="verdictScore"></param>
        void OnVerdict(Verdict verdict, int combo, int verdictScore)
        {
            /*
            switch (verdict)
            {
                case Verdict.Perfect:
                    _verdictText.text = $"Perfect!";
                    break;
                case Verdict.Good:
                    _verdictText.text = $"Good!";
                    break;
                case Verdict.Miss:
                    _verdictText.text = $"Miss!";
                    break;
                case Verdict.Bad:
                    _verdictText.text = $"Bad!";
                    break;
            }
            */
            _verdictText.text = $"{verdict} !";
            _comboText.text = $"COMBO {combo} !!";
            _verdictScoreText.text = $"Extra Score {verdictScore}";

            StartCoroutine(IE_GoSetActive(_comboGO));
        }

        IEnumerator IE_GoSetActive(GameObject go)
        {
            go.SetActive(true);
            yield return new WaitForSeconds(1f);
            go.SetActive(false);

        }
    }
}