using UnityEngine;
using Photon.Pun;

public class SessionKickGuard : MonoBehaviourPunCallbacks
{
    [SerializeField] private SessionKickOverlay overlayPrefab;

    private SessionKickOverlay _overlay;
    private bool _shown;

    private static SessionKickGuard _instance;
    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void EnsureOverlay()
    {
        if (_overlay != null) return;
        if (overlayPrefab != null)
        {
            _overlay = Instantiate(overlayPrefab);
            DontDestroyOnLoad(_overlay.gameObject);
        }
    }

    private void Update()
    {
        if (_shown) return;
        if (SessionEnforcer.KickedByRemote)
        {
            _shown = true;
            EnsureOverlay();
            _overlay?.Show();
        }
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        if (_shown) return;
        if (SessionEnforcer.KickedByRemote)
        {
            _shown = true;
            EnsureOverlay();
            _overlay?.Show();
        }
    }
}