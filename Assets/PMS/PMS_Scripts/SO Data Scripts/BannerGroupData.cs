using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BannerGroupData", menuName = "UI/BannerGroup/BannerGroupData")]
public class BannerGroupData : ScriptableObject
{
    public List<BannerData> banners;
}
