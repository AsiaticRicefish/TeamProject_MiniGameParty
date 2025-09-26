using UnityEngine;

namespace YG
{
    public interface ISoundFacade
    {
        /// <summary>SoundCollection의 soundName으로 SFX 1회 재생</summary>
        void PlaySFX(string soundName);
    }
}