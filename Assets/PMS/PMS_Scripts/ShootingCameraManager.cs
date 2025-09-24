using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DesignPattern;
using Cinemachine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.VisualScripting.Antlr3.Runtime;

namespace ShootingScene
{
    public class ShootingCameraManager : PunSingleton<ShootingCameraManager>
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachineVirtualCamera vcamDefault; // 기본 시점
        [SerializeField] private CinemachineVirtualCamera vcamFollow;  // 알 따라가기

        [SerializeField] private FollowCam vc2Follow;
        [SerializeField] Vector3 initCameraPos;

        private CancellationTokenSource zoomCancelTokenSource;

        [SerializeField] private float zoomDuration = 2f;
        [SerializeField] private float startFov = 60f;
        [SerializeField] private float endFov = 30f;
        [SerializeField] private float startRotX = 30f;
        [SerializeField] private float endRotX = 0f;

        public void StopZoomIn()
        {
            zoomCancelTokenSource?.Cancel();
            zoomCancelTokenSource.Dispose();
            zoomCancelTokenSource = null;

            // 상태 복원
            if (vcamFollow != null)
            {
                vcamFollow.m_Lens.FieldOfView = startFov;
                Vector3 rot = vcamFollow.transform.eulerAngles;
                vcamFollow.transform.eulerAngles = new Vector3(startRotX, rot.y, rot.z);
            }
        }

        public void ZoomInCamera()
        {
            if(zoomCancelTokenSource != null)
            {
                StopZoomIn();
            }
            zoomCancelTokenSource = new CancellationTokenSource();
            ZoomAndTiltCoroutine(zoomCancelTokenSource.Token).Forget(); //토큰 전달
        }

        private async UniTask ZoomAndTiltCoroutine(CancellationToken token)
        {
            float time = 0f;

            while (time < zoomDuration)
            {
                token.ThrowIfCancellationRequested(); // 중단 요청 체크
                float t = time / zoomDuration;

                // FOV 보간
                if (vcamFollow != null)
                {
                    vcamFollow.m_Lens.FieldOfView = Mathf.Lerp(startFov, endFov, t);
                }

                // Rotation.x 보간
                Vector3 currentRot = vcamFollow.transform.eulerAngles;
                float newX = Mathf.Lerp(startRotX, endRotX, t);
                vcamFollow.transform.eulerAngles = new Vector3(newX, currentRot.y, currentRot.z);

                time += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 최종 값 고정
            vcamFollow.m_Lens.FieldOfView = endFov;
            vcamFollow.transform.eulerAngles = new Vector3(endRotX, vcamFollow.transform.eulerAngles.y, vcamFollow.transform.eulerAngles.z);
        }

        protected override void OnAwake()
        {
            isPersistent = false;
            // 기본 우선순위 세팅
            if (vcamDefault) vcamDefault.Priority = 10;
            if (vcamFollow) vcamFollow.Priority = 5;

            initCameraPos = vcamDefault.transform.position;

            startFov = vcamFollow.m_Lens.FieldOfView;
            startRotX = vcamFollow.transform.eulerAngles.y;
        }

        public void StartFollowTarget(GameObject currentUnimoEgg)
        {
            // Follow 타깃 지정 + 우선순위 스위치
            // 발사체 따라가기 시작
            vcamFollow.Priority = 20;
            vcamFollow.LookAt = currentUnimoEgg.transform;

            ZoomInCamera(); // 줌인 효과 실행
            //vcamFollow.Follow = currentUnimoEgg.transform;
            //vc2Follow.target = currentUnimoEgg.transform;
        }

        public void StopFollowTarget()
        {
            // Follow 타깃 지정 + 우선순위 스위치
            // 발사체 따라가기 시작
            vcamFollow.Priority = 5;
            vcamFollow.LookAt = null;

            //StopZoomIn(); // 줌 효과 중단

            //vcamFollow.Follow = null;
            //vc2Follow.target = null;
        }

        public void SwipePosInit()
        {
            vcamDefault.transform.position = initCameraPos;
        }
    }
}
