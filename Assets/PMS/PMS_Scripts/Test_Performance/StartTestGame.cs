using System.Collections;
using System.Collections.Generic;
using LDH_Game;
using LDH_Util;
using Managers;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartTestGame : MonoBehaviour
{
    public Button StartButton;
    
    // Start is called before the first frame update
    void Start()
    {
        StartButton.onClick.RemoveListener(StartGame);
        StartButton.onClick.AddListener(StartGame);
    }

    private void StartGame()
    {
        
        Manager.UI.EnqueueToast(Define_LDH.ToastType.Notify, "테스트 게임 모드 시작");
        Manager.Network.SetTestNicknameAndID();
        //game 리소스 다운 / 초기화 및 파이어베이스 데이터 로드 진행 후 서버로 연결하기 위해 game boot strap을 생성한다.
        Util_LDH.ConsoleLog(this, "------------Game Start Bootstrap을 만듭니다. -----------");
        GameObject gameBootstrap = new GameObject("GameStartBootstrap", typeof(GameStartBootstrap));

    }
}
