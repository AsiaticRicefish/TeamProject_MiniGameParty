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
            isPersistent = false;
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
            // 발사체 따라가기 시작
            vcamFollow.Follow = currentUnimoEgg.transform;
            vcamFollow.Priority = 20;                       // 자연스러운 Blend 적용

            // Blend가 시작될 시간을 조금 기다리거나, 최소 따라가기 시간 확보
            yield return new WaitForSeconds(0.1f);

            yield return new WaitUntil(() => Camera.main.GetComponent<CinemachineBrain>().ActiveBlend == null);          //따라가는 시간

            // 복귀: 우선순위 낮추고 MoveToTopOfPrioritySubqueue 호출 준비
            // 복귀하는 경우 우선순위 되돌리고 타깃 해제
            vcamFollow.Priority = 5;
            vcamFollow.Follow = null;
            // Blend 종료 후 즉시 컷        
            vcamFollow.MoveToTopOfPrioritySubqueue();

            yield return new WaitUntil(() => Camera.main.GetComponent<CinemachineBrain>().ActiveBlend == null);
            followCo = null;
        }
    }
}
