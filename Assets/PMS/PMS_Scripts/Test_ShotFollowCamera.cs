using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DesignPattern;
using Cinemachine;

namespace ShootingScene
{
    public class Test_ShotFollowCamera : PunSingleton<Test_ShotFollowCamera>
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachineVirtualCamera vcamDefault; // 기본 시점
        [SerializeField] private CinemachineVirtualCamera vcamFollow;  // 알 따라가기

        [SerializeField] Transform initCameraPos;

        private Coroutine followCo;
        
        protected override void OnAwake()
        {
            // 기본 우선순위 세팅
            if (vcamDefault) vcamDefault.Priority = 10;
            if (vcamFollow) vcamFollow.Priority = 5;
        }

        // 발사하는 경우 내 발사체의 Rigidbody를 따라감
        public void StartFollow(GameObject currentUnimoEgg)
        {
            followCo = StartCoroutine(CoFollow(currentUnimoEgg));
        }

        private IEnumerator CoFollow(GameObject currentUnimoEgg)
        {
            Debug.Log("따라가는중");
            // Follow 타깃 지정 + 우선순위 스위치
            vcamFollow.Follow = currentUnimoEgg.transform;
            vcamFollow.Priority = 20;         // 기본보다 높게

            yield return new WaitForSeconds(5.0f);

            // 복귀하는 경우 우선순위 되돌리고 타깃 해제
            vcamFollow.Priority = 5;
            vcamFollow.Follow = null;
            followCo = null;
        }
    }
}
