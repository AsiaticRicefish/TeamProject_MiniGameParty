using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class JenGaLoadingUI : MonoBehaviour
{
    [SerializeField] GameObject loadingGo; //로딩 패널
    [SerializeField] Image loadingImg; //로딩 이미지 바 Filled, Raidal 360

    /// <summary>
    /// 로딩 로직 시작시 호출해야하는 메서드
    /// </summary>
    public void ActiveLoading()
    {
        loadingGo.SetActive(true);
        SetProgress(0f);
    }

    /// <summary>
    /// 로딩 로직 종료시 호출해야하는 메서드
    /// </summary>
    public void DeActiveLoading()
    {
        loadingGo.SetActive(false);
    }

    /// <summary>
    /// 진행도에 따라 로딩바를 채우는 역할
    /// 
    /// 예시 ) JenGaLoadingUI.SetProgress(PhotonNetwork.LevelLoadingProgress)
    /// </summary>
    /// <param name="amount"></param>
    public void SetProgress(float amount)
    {
        amount = Mathf.Clamp01(amount);
        loadingImg.fillAmount = amount;
    }

    //테스트 코드
    /*
    void Start()
    {
        StartCoroutine(IE_Test());
    }
    IEnumerator IE_Test()
    {
        float test = 0f;
        while (test < 1f)
        {
            test += 0.1f;
            SetProgress(test);
            yield return new WaitForSeconds(1f);
        }
        DeActiveLoading();
    }
    */
}