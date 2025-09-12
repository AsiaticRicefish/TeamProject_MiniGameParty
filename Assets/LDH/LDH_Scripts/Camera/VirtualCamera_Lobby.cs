using System;
using Managers;
using UnityEngine;

namespace LDH_Camera
{
    public class VirtualCamera_Lobby : VirtualCam_Base
    {
        [SerializeField] private int focusPriority = 10; // 활성
        [SerializeField] private int offPriority = 0;     // 비활성

        public int FocusPriority => focusPriority;
        public int OffPriority => offPriority;

        protected override void Init()
        {
            base.Init();
            
        }
    }
}