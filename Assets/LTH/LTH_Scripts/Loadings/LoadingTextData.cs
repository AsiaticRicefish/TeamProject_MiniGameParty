using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// 게임 테마 Enum 정의
[Serializable]
public enum GameThemeType
{
    Default,        // 공용
    Shooting,       // 슈팅 미니게임
    Meteor,         // 별똥별 미니게임
    Jenga,          // 젠가 미니게임
    Rhythm,         // 리듬 미니게임
}

[Serializable]
/// <summary>
/// 구글 시트 연동용 로딩 텍스트 데이터 클래스
/// </summary>
public class LoadingTextData
{
    public string gameTitle;
    public string smallDescription;
    public string category;

    public LoadingTextData(string title, string smallDesc, string cat = "Default")
    {
        gameTitle = title;
        smallDescription = smallDesc;
        category = cat;
    }
}
