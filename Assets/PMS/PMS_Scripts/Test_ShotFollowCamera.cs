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
            if (currentUnimoEgg == null) yield break;
            Debug.Log("따라가는중");

            CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();

            // Follow 타깃 지정 + 우선순위 스위치
            // 발사체 따라가기 시작
            vcamFollow.Follow = currentUnimoEgg.transform;
            vcamFollow.Priority = 20;                       // 자연스러운 Blend 적용

            // Blend가 시작될 시간을 조금 기다리거나, 최소 따라가기 시간 확보
            yield return new WaitForSeconds(0.1f);


            //타이머를 통한 리턴
            float timer = 0f;
            float timeout = 3f;
            while (brain.ActiveBlend != null && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.0f);
            Debug.Log("카메라 언제호출?");


            //yield return new WaitUntil(() => Camera.main.GetComponent<CinemachineBrain>().ActiveBlend == null);

            //float timer = 0f;
            //while (Camera.main.GetComponent<CinemachineBrain>().ActiveBlend == null && timer < 2f)
            //{
            //    timer += Time.deltaTime;
            //    yield return null;
            //}

            // 복귀: 우선순위 낮추고 MoveToTopOfPrioritySubqueue 호출 준비
            // 복귀하는 경우 우선순위 되돌리고 타깃 해제
            vcamFollow.Priority = 5;
            vcamFollow.Follow = null;
            // Blend 종료 후 즉시 컷        

            followCo = null;
        }

        public void StartFollowTarget(GameObject currentUnimoEgg)
        {
            // Follow 타깃 지정 + 우선순위 스위치
            // 발사체 따라가기 시작
            vcamFollow.Follow = currentUnimoEgg.transform;
            vcamFollow.Priority = 20;
        }

        public void StopFollowTarget()
        {
            // Follow 타깃 지정 + 우선순위 스위치
            // 발사체 따라가기 시작
            vcamFollow.Priority = 5;
            vcamFollow.Follow = null;
        }
    }
}
