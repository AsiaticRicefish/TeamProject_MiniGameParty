using System;
using Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LDH_Util
{
    public class BGMController : MonoBehaviour
    {
        [SerializeField] private Define_LDH.BgmKey bgmKey;
        
        private void Start()
        {
            SoundManager.Instance.PlayBGM(bgmKey.ToString());   // 씬 들어왔을 때 재생
        }
        
        private void OnDestroy()
        {
           SoundManager.Instance?.StopAllSounds(); // 씬 나갈 때 정지(또는 StopBGM으로 페이드아웃)
        }
        
    }
}