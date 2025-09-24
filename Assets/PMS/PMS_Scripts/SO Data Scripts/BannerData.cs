using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "BannerData", menuName = "UI/BannerData")]
public class BannerData : ScriptableObject
{
    public Sprite bannerBackGroundImage;    // 배너 백그라운드 이미지
    public Sprite bannerImage;              // 배너 이미지
    public Color bannerBackGroundColor;     // 배너 BackGround Color
    public UnityEvent onClickEvent;         // 클릭 시 실행할 커스텀 이벤트
}
