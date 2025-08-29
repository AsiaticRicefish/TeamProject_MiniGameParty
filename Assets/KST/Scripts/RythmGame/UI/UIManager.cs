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

        [SerializeField] Slider overHeatSlider;// Image Fillamount로 변경해도 됨.

        
    }


}