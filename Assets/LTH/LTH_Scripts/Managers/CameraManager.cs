using System;
using System.Collections.Generic;
using Cinemachine;
using DesignPattern;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraManager : CombinedSingleton<CameraManager>
{
    [Header("Priority Settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    private Dictionary<string, CinemachineVirtualCamera> _cameraDict = new();
    private Stack<string> _cameraStack = new();
    
    private Camera _mainCam;

    protected override void OnAwake()
    {
        isPersistent = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start() => _mainCam = Camera.main;

    protected private void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[CameraManager] OnSceneLoaded() �� ���� �ʱ�ȭ. ��: {scene.name}");
        ClearStack();  // ���� Ŭ����
    }


    #region Camera Main Reference


    /// <summary>
    /// ���� ���� MainCamera�� ã�� �������մϴ�.
    /// </summary>
    public void UpdateMainCamera()
    {
        _mainCam = Camera.main;
        Debug.Log("[CameraManager] UpdateMainCamera() ȣ��� �� MainCamera ����");
    }

    /// <summary>
    /// �ܺο��� ���� MainCamera�� �����մϴ�.
    /// </summary>
    public void SetMainCamera(Camera cam)
    {
        _mainCam = cam;

        // CinemachineBrain ������Ʈ�� ������ �߰�
        if (_mainCam && !_mainCam.TryGetComponent(out CinemachineBrain _))
        {
            _mainCam.gameObject.AddComponent<CinemachineBrain>();
        }
    }

    public Camera MainCamera => _mainCam != null ? _mainCam : Camera.main;

    #endregion

    #region Camera Register / UnRegister

    public bool HasCamera(string id) => _cameraDict.ContainsKey(id);

    /// <summary>
    /// VirtualCamera�� ID�� �Բ� ����մϴ�.
    /// </summary>
    public void RegisterCamera(string id, CinemachineVirtualCamera cam)
    {
        if (string.IsNullOrEmpty(id) || cam == null) return;
        if (_cameraDict.ContainsKey(id)) 
        { 
            Debug.LogWarning($"[CameraManager] Duplicate id: {id}"); 
            return; 
        }

        _cameraDict.Add(id, cam);
        cam.Priority = inactivePriority;

        Debug.Log($"[CameraManager] Registered: {id}");
    }

    /// <summary>
    /// ID�� ��ϵ� VirtualCamera�� �����մϴ�.
    /// </summary>
    public void UnregisterCamera(string id)
    {
        if (!_cameraDict.Remove(id)) return;
        Debug.Log($"[CameraManager] UnregisterCamera() �� ī�޶� ����: {id}");
    }

    #endregion

    /// <summary>
    /// ID�� �ش��ϴ� VirtualCamera�� ��ȯ�մϴ�.
    /// </summary>
    public CinemachineVirtualCamera GetCamera(string id)
    {
        _cameraDict.TryGetValue(id, out var cam);
        return cam;
    }

    #region Stack-based Camera Switching

    public string TopId => _cameraStack.Count > 0 ? _cameraStack.Peek() : null;

    /// <summary>
    /// ������ ī�޶� ���� �ֻ�ܿ� �ø��� �켱���� 10���� �����մϴ�.
    /// </summary>
    public void PushCamera(string id)
    {
        Debug.Log($"[CameraManager] PushCamera() ȣ���: {id}");

        if (!_cameraDict.ContainsKey(id))
        {
            Debug.LogWarning($"[CameraManager] PushCamera() �� {id} ī�޶� �������� ����");
            return;
        }

        // ���� top ��Ȱ��ȭ
        if (_cameraStack.Count > 0)
        {
            var prev = GetCamera(_cameraStack.Peek());
            if (prev) prev.Priority = inactivePriority;
        }

        _cameraStack.Push(id);

        var now = GetCamera(id);
        if (now) now.Priority = activePriority;

        EnsureBrain(); // ��ȯ ������ ���� Brain ���� Ȯ��
    }

    /// <summary>
    /// ���ÿ��� ���� ī�޶� �����ϰ�, �ٷ� �Ʒ� ī�޶� Ȱ��ȭ�մϴ�.
    /// </summary>
    public void PopCamera()
    {
        Debug.Log("[CameraManager] PopCamera() ȣ���");


        if (_cameraStack.Count == 0)
        {
            Debug.LogWarning("[CameraManager] PopCamera() �� ī�޶� ������ ��� ����");
            return;
        }

        var poppedId = _cameraStack.Pop();
        var popped = GetCamera(poppedId);
        if (popped) popped.Priority = inactivePriority;


        if (_cameraStack.Count > 0)
        {
            var top = GetCamera(_cameraStack.Peek());
            if (top) top.Priority = activePriority;
        }
        Debug.Log($"[CameraManager] Pop �� {poppedId} (nowTop: {TopId})");
    }

    /// <summary>
    /// ��� ī�޶� �켱������ 0���� �ʱ�ȭ�ϰ� ������ ���ϴ�.
    /// </summary>
    public void ClearStack()
    {
        Debug.Log("[CameraManager] ClearStack() ȣ���");
        foreach (var cam in _cameraDict.Values) if (cam) cam.Priority = inactivePriority;
        _cameraStack.Clear();
    }

    public void ResetAllPriorities()
    {
        foreach (var kv in _cameraDict) if (kv.Value) kv.Value.Priority = inactivePriority;
    }

    #endregion

    #region Blends / Impulse (ī�޶� ��鸲 ���)

    public void ApplyDefaultBlend(CinemachineBlendDefinition.Style style = CinemachineBlendDefinition.Style.EaseInOut,
                                  float time = 0.25f)
    {
        var brain = EnsureBrain();
        brain.m_DefaultBlend = new CinemachineBlendDefinition(style, time);
    }

    public void ApplyCustomBlends(CinemachineBlenderSettings settings)
    {
        var brain = EnsureBrain();
        brain.m_CustomBlends = settings;
    }

    public void PlayImpulseOnTop(float force = 1f)
    {
        if (TopId == null) { Debug.LogWarning("[CameraManager] Impulse �� no top"); return; }
        var cam = GetCamera(TopId);
        if (!cam) return;

        var src = cam.GetComponent<CinemachineImpulseSource>();
        if (!src) { Debug.LogWarning($"[CameraManager] Impulse �� no source on {TopId}"); return; }

        src.GenerateImpulseWithForce(force);
    }

    #endregion

    private CinemachineBrain EnsureBrain()
    {
        var cam = MainCamera != null ? MainCamera : Camera.main;
        if (!cam) cam = Camera.main;
        if (!cam) cam = FindObjectOfType<Camera>();
        if (!cam) throw new Exception("[CameraManager] No Camera found in scene.");

        if (!cam.TryGetComponent(out CinemachineBrain brain))
            brain = cam.gameObject.AddComponent<CinemachineBrain>();

        return brain;
    }
}