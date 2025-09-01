using System;
using Managers;
using UnityEngine;

namespace LDH_Camera
{
    public class VirtualCamera_Lobby : VirtualCam_Base
    {
        [SerializeField] private bool pushCameraOnAwake;

        private void Start()
        {
            if(pushCameraOnAwake)
                Manager.Camera.PushCamera(cameraID);
        }
    }
}