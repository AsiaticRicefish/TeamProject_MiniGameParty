using Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class AutoRegisterVCam : MonoBehaviour
{
    [SerializeField] private string cameraId;
    public void Awake()
    {
        var vcam = GetComponent<CinemachineVirtualCamera>();
        if (!vcam) 
        { 
            Debug.LogError("[AutoRegisterVCam] Missing CinemachineVirtualCamera"); 
            return; 
        }

        CameraManager.Instance.RegisterCamera(cameraId, vcam);
    }

   public void OnDestroy()
    {
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.UnregisterCamera(cameraId);
        }

    }
}
