using System.Collections;
using System.Collections.Generic;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// Firebase Auth 접근을 위한 전역 헬퍼 매니저
/// - Firebase 초기화는 GuestLoginManager에서만 수행
/// - 여기서는 단순히 Auth 인스턴스를 보관/공유
/// </summary>
public class BackendManager : MonoBehaviour
{
    public static BackendManager Instance { get; private set; }

    public static FirebaseAuth Auth { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        //  Firebase 초기화는 하지 않음 (GuestLoginManager가 담당)
        //  Auth 인스턴스만 가져오기
        try
        {
            if (KYG.Auth.GuestLoginManager.Instance != null && 
                KYG.Auth.GuestLoginManager.Instance.IsFirebaseReady)
            {
                Auth = FirebaseAuth.DefaultInstance;
                Debug.Log("[BackendManager] FirebaseAuth instance ready.");
            }
            else
            {
                Debug.Log("[BackendManager] GuestLoginManager not ready yet. Auth will be set later.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BackendManager] FirebaseAuth not available yet: {e.Message}");
        }
    }

    private void Update()
    {
        // 준비 안 된 상태에서 나중에 GuestLoginManager가 초기화 완료되면 Auth 동기화
        if (Auth == null && 
            KYG.Auth.GuestLoginManager.Instance != null && 
            KYG.Auth.GuestLoginManager.Instance.IsFirebaseReady)
        {
            Auth = FirebaseAuth.DefaultInstance;
            Debug.Log("[BackendManager] FirebaseAuth instance linked (late).");
        }
    }
}