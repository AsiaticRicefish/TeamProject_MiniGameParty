using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class JenGaLoadingUI : MonoBehaviour
{
    [SerializeField] GameObject loadingGo; //�ε� �г�
    [SerializeField] Image loadingImg; //�ε� �̹��� �� Filled, Raidal 360

    /// <summary>
    /// �ε� ���� ���۽� ȣ���ؾ��ϴ� �޼���
    /// </summary>
    public void ActiveLoading()
    {
        loadingGo.SetActive(true);
        SetProgress(0f);
    }

    /// <summary>
    /// �ε� ���� ����� ȣ���ؾ��ϴ� �޼���
    /// </summary>
    public void DeActiveLoading()
    {
        loadingGo.SetActive(false);
    }

    /// <summary>
    /// ���൵�� ���� �ε��ٸ� ä��� ����
    /// 
    /// ���� ) JenGaLoadingUI.SetProgress(PhotonNetwork.LevelLoadingProgress)
    /// </summary>
    /// <param name="amount"></param>
    public void SetProgress(float amount)
    {
        amount = Mathf.Clamp01(amount);
        loadingImg.fillAmount = amount;
    }

    //�׽�Ʈ �ڵ�
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