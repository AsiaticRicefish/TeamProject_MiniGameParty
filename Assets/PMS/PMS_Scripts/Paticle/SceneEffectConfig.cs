using UnityEngine;

[CreateAssetMenu(menuName = "Effects/SceneEffectConfig")]
public class SceneEffectConfig : ScriptableObject
{
    public string sceneName;
    public ParticleData[] effects;  // ParticleData에 addressableKey와 id 포함
}