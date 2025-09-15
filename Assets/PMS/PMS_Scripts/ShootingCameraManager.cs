using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DesignPattern;
using Cinemachine;

namespace ShootingScene
{
    public class ShootingCameraManager : PunSingleton<ShootingCameraManager>
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachineVirtualCamera vcamDefault; // 기본 시점
        [SerializeField] private CinemachineVirtualCamera vcamFollow;  // 알 따라가기

        [SerializeField] private FollowCam vc2Follow;
        [SerializeField] Vector3 initCameraPos;
        
        protected override void OnAwake()
        {
            isPersistent = false;
            // 기본 우선순위 세팅
            if (vcamDefault) vcamDefault.Priority = 10;
            if (vcamFollow) vcamFollow.Priority = 5;

            initCameraPos = vcamDefault.transform.position;
        }

        public void StartFollowTarget(GameObject currentUnimoEgg)
        {
            // Follow 타깃 지정 + 우선순위 스위치
            // 발사체 따라가기 시작
            vcamFollow.Priority = 20;
            vcamFollow.Follow = currentUnimoEgg.transform;
            vc2Follow.target = currentUnimoEgg.transform;
        }

        public void StopFollowTarget()
        {
            // Follow 타깃 지정 + 우선순위 스위치
            // 발사체 따라가기 시작
            vcamFollow.Priority = 5;
            vcamFollow.Follow = null;
            vc2Follow.target = null;
        }

        public void SwipePosInit()
        {
            vcamDefault.transform.position = initCameraPos;
        }
    }
}
