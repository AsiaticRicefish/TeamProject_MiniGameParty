using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace RhythmGame
{

    public static class RhythmRoomProps
    {
        public const string KEY_PREFIX = "ry_";
        public const string KEY_RANK_UIDS = KEY_PREFIX + "rank_uids";   // string[]
        public const string KEY_RANK_VALS = KEY_PREFIX + "rank_vals";   // int[]

        public static bool TryGet<T>(Hashtable table, string key, out T value)
        {
            if (table != null && table.ContainsKey(key) && table[key] is T t) { value = t; return true; }
            value = default; return false;
        }
    }
}