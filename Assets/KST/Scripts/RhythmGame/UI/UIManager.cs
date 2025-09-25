using DesignPattern;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame
{
    public class UIManager : PunSingleton<UIManager>
    {
        [SerializeField] TMP_Text playTime;
        [SerializeField] Button btn1;
        [SerializeField] Button btn2;

        public void SpeedUp()
        {
            Time.timeScale *= 1.2f;
        }

        public void SpeedDown()
        {
            Time.timeScale /= 1.2f;
        }

    }


}