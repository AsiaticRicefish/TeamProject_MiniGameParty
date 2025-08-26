using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace LDH_MainGame
{
    public class PhotonViewSync : MonoBehaviour
    {
        public static IEnumerator SyncPhotonViewID(PhotonView callerView)
        {
            // 씬에 배치되어 있는 포톤 뷰를 수집한다.
            var sceneViews = FindObjectsOfType<PhotonView>(true)
                .Where(v => v != null)
                .OrderBy(v => GetTransformPath(v.transform))
                .ToList();

            if (PhotonNetwork.IsMasterClient)
            {
                var ids = new int[sceneViews.Count];

                for (int i = 0; i < sceneViews.Count; i++)
                {
                    var pv = sceneViews[i];
                    // 씬 오브젝트용 ViewID 할당
                    if (!PhotonNetwork.AllocateViewID(pv))
                    {
                        Debug.LogError($"AllocateViewID failed: {GetTransformPath(pv.transform)}");
                    }

                    ids[i] = pv.ViewID;
                }
                callerView.RPC(nameof(RpcAssignSceneViewIDs), RpcTarget.OthersBuffered, ids);
            }

            yield return null;
        }
        
        [PunRPC]
        void RpcAssignSceneViewIDs(int[] ids)
        {
            var sceneViews = FindObjectsOfType<PhotonView>(true)
                .Where(v => v != null && v.IsSceneView)
                .OrderBy(v => GetTransformPath(v.transform))
                .ToList();

            for (int i = 0; i < sceneViews.Count && i < ids.Length; i++)
            {
                if (sceneViews[i].ViewID != ids[i])
                    sceneViews[i].ViewID = ids[i];
            }
        }
        
        
        // 경로 문자열: 모든 클라에서 동일해야 함
        static string GetTransformPath(Transform t)
        {
            var stack = new System.Collections.Generic.Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", stack);
        }
    }
}