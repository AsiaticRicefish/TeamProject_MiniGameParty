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
        [SerializeField] private PhotonView[] sceneViews;
        [SerializeField] private GameObject[] roots;   // 바인딩 전까지 비활성화
        

        private bool _isComplete = false;
        public bool IsComplete => _isComplete;


        private bool _isActiveAll = false;
        public bool IsActiveAll => _isActiveAll;
            
            
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
            Debug.Log("===== PhotonViewCoordinator : Start Photon View Id Coordination ========");
            yield return new WaitUntil(() => LDH_MainGame.PhotonViewSync.Instance != null);
            // 씬 내 PV가 모두 생성/등록된 뒤 바인딩 시작
            yield return LDH_MainGame.PhotonViewSync.Instance.StartCoroutine(
                LDH_MainGame.PhotonViewSync.Instance.SafePhotonViewSync(this)
            );
            
            Debug.Log("======= Complete Coordination =====");
            
            ActiveObjects();
        }

        public PhotonView[] GetSceneViews() => sceneViews;

       
        public void ApplyIds(int[] ids)
        {
            //Debug.Log("apply id 실행");
            if(_isComplete) return;
            int n = Mathf.Min(sceneViews.Length, ids.Length);
            for (int i = 0; i < n; i++)
            {
                var pv = sceneViews[i];
                if ( pv.ViewID != ids[i])
                    pv.ViewID = ids[i];
            }
            
            Debug.Log("_isComplete = true 로 변경된");
            _isComplete = true;
        }

        public void ActiveObjects()
        {
            Debug.Log("[PhotonViewCoordinator] Active Target Objects");
            foreach (var r in roots) if (r) r.SetActive(true);

            _isActiveAll = true;

        }
    }
}