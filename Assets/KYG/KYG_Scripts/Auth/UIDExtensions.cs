using Photon.Realtime;
using ExitGames.Client.Photon;

public static class UIDExtensions
{
    private const string UID_KEY = "uid";

    /// <summary>Photon Player의 CustomProperties에서 uid를 안전하게 꺼냅니다.</summary>
    public static string GetUidSafe(this Player p)
    {
        if (p == null) return null;
        if (p.CustomProperties != null &&
            p.CustomProperties.TryGetValue(UID_KEY, out var v) &&
            v is string s && !string.IsNullOrEmpty(s))
            return s;

        // 보정: PUN이 AuthenticationValues.UserId 를 Player.UserId에 반영하는 경우가 있어, fallback으로 시도
        if (!string.IsNullOrEmpty(p.UserId)) return p.UserId;

        return null;
    }

    /// <summary>로컬만 사용. uid 누락 시 다시 셋업(GuestLoginManager.SafeReapplyUid와 동일 의도)</summary>
    public static void EnsureLocalUid(this Player local, string uid)
    {
        if (local == null || string.IsNullOrEmpty(uid)) return;
        var hasUid = local.CustomProperties != null &&
                     local.CustomProperties.ContainsKey(UID_KEY) &&
                     local.CustomProperties[UID_KEY] is string s &&
                     !string.IsNullOrEmpty(s);
        if (!hasUid)
        {
            var props = new Hashtable { { UID_KEY, uid } };
            local.SetCustomProperties(props);
        }
    }
}