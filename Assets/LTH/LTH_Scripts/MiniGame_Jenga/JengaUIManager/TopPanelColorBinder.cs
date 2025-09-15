using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public enum UidSource 
{ 
    LocalPlayer, 
    CurrentTurn, // 혹시 턴이 있는 게임에서 쓸 수도 있으니 남겨둠
    Custom 
}

public class TopPanelColorBinder : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private UidSource uidSource = UidSource.LocalPlayer;
    [SerializeField] private string customUid; // UidSource.Custom 일 때 사용
    [SerializeField] private JengaRankingUIAnimated provider; // 비워두면 FindObjectOfType로 찾음

    [Header("Targets")]
    [SerializeField] private Graphic[] targets; // Image, TMP_Text, Outline 등 원하는 만큼 할 수 있도록

    [Header("Options")]
    [SerializeField] private float targetAlpha = 1f;
    [SerializeField] private bool applyOnStart = true;

    private void Awake()
    {
        if (!provider) provider = FindObjectOfType<JengaRankingUIAnimated>(true);
        if (provider) provider.OnPaletteChanged += HandlePaletteChanged;
    }

    private void Start()
    {
        if (applyOnStart) ApplyNow();
    }

    private void OnDestroy()
    {
        if (provider) provider.OnPaletteChanged -= HandlePaletteChanged;
    }

    private void HandlePaletteChanged()
    {
        ApplyNow();
    }

    public void ApplyNow()
    {
        if (!provider || targets == null || targets.Length == 0) return;

        var uid = ResolveUid();
        if (string.IsNullOrEmpty(uid)) return;

        if (provider.TryGetColor(uid, out var c))
        {
            foreach (var g in targets.Where(t => t))
            {
                var col = c;
                col.a = targetAlpha <= 0 ? g.color.a : targetAlpha;
                g.color = col;
            }
        }
    }

    private string ResolveUid()
    {
        switch (uidSource)
        {
            case UidSource.LocalPlayer:
                return PhotonNetwork.LocalPlayer?.CustomProperties?["uid"] as string;
            case UidSource.CurrentTurn:
                return PhotonNetwork.LocalPlayer?.CustomProperties?["uid"] as string;
            case UidSource.Custom:
                return customUid;
        }
        return null;
    }
}