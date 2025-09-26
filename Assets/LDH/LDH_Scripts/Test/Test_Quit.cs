using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class Test_Quit : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
#if UNITY_EDITOR
                // 에디터에서 플레이 모드 종료
                // UnityEditor.EditorApplication.isPlaying = false;
                if (PhotonNetwork.InRoom && PhotonNetwork.NetworkClientState != ClientState.Leaving &&
                    PhotonNetwork.NetworkClientState != ClientState.Joining)
                {
                    PhotonNetwork.LeaveRoom();
                }
#else
                // 빌드 환경에서 앱 종료
                Application.Quit();
#endif
            }
        }
    }
}