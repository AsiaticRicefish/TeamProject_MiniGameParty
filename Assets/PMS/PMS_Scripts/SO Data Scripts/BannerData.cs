using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "BannerData", menuName = "UI/BannerData")]
public class BannerData : ScriptableObject
{
    public string bannerBackGroundImageKey; // Addressable 키
    public string bannerImageKey;           // Addressable 키

    public Sprite bannerBackGroundImage;    // 배너 default 백그라운드 이미지 
    public Sprite bannerImage;              // 배너 이미지
    public Color bannerBackGroundColor;     // 배너 default BackGround Color

    public string url; // url 오픈이면 값을 넣어주도록
}
