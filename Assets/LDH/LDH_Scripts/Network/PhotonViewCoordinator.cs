using System;
using System.Collections;
using LDH_MainGame;
using Photon.Pun;
using UnityEngine;

namespace LDH.LDH_Scripts.Network
{
    [DefaultExecutionOrder(-10000)]
    public class PhotonViewCoordinator : MonoBehaviour
    {
        public static PhotonViewCoordinator Instance { get; private set; }
        
        [SerializeField] private PhotonView[] sceneViews;
        [SerializeField] private GameObject[] roots;   // 바인딩 전까지 비활성화
        
        
        void OnEnable() => Instance = this;
        private void OnDestroy() => Instance = null;


        private bool _isComplete = false;
        public bool IsComplete => _isComplete;

        private void Awake()
        {
            if (roots.Length != 0 || sceneViews.Length != null)
            {
                _isComplete = false;
                foreach (var r in roots) if (r) r.SetActive(false);
            }
            
        }

        public IEnumerator Start()
        {
            Debug.Log("===== 포톤 뷰 조정 ========");
            yield return new WaitUntil(() => LDH_MainGame.PhotonViewSync.Instance != null);
            // 씬 내 PV가 모두 생성/등록된 뒤 바인딩 시작
            yield return LDH_MainGame.PhotonViewSync.Instance.StartCoroutine(
                LDH_MainGame.PhotonViewSync.Instance.SyncSceneViewsAndActivate()
            );
        }

        public PhotonView[] GetSceneViews() => sceneViews;

       
        public void ApplyIdsAndActivate(int[] ids)
        {
            int n = Mathf.Min(sceneViews.Length, ids.Length);
            for (int i = 0; i < n; i++)
            {
                var pv = sceneViews[i];
                if ( pv.ViewID != ids[i])
                    pv.ViewID = ids[i];
            }
            foreach (var r in roots) if (r) r.SetActive(true);

            _isComplete = true;
        }
    }
}