using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JsonDebugTest : MonoBehaviour
{
    private void Start()
    {
        var json = Resources.Load<TextAsset>("admob_config");
        if (json == null)
        {
            Debug.LogError("admob_config.json을 찾을 수 없음!");
        }
        else
        {
            Debug.Log("JSON 불러오기 성공");
            Debug.Log(json.text);  // 내용 그대로 출력
        }
    }
}