using UnityEngine;

namespace YG
{
    public static class SoundFacade
    {
        /// <summary>
        /// 씬에서 MeteorTapSoundBridge를 찾아 그걸 ISoundFacade로 사용.
        /// 못 찾으면 null 반환(미연결 시에도 게임 진행은 가능)
        /// </summary>
        public static ISoundFacade TryFindInScene()
        {
            if (MeteorTapSoundBridge.Instance != null)
                return MeteorTapSoundBridge.Instance;

            var found = Object.FindObjectOfType<MeteorTapSoundBridge>(true);
            if (found != null) return found;

            Debug.LogWarning("[SoundFacade] MeteorTapSoundBridge가 씬에 없습니다. (사운드만 미출력)");
            return null;
        }
    }
}