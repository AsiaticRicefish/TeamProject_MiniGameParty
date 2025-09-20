using UnityEngine;

[CreateAssetMenu(fileName = "SceneSoundMappingConfig", menuName = "Audio/Scene Sound Mapping Config")]
public class SceneSoundMappingConfig : ScriptableObject
{
    public SceneSoundMapping[] sceneMappings;

    public GameType GetGameTypeForScene(string sceneName)
    {
        SceneSoundMapping mapping = null;

        for (int i = 0; i < sceneMappings.Length; i++)
        {
            if (sceneMappings[i].sceneName == sceneName)
            {
                mapping = sceneMappings[i];
                break;
            }
        }

        if (mapping != null)
        {
            return mapping.gameType;
        }
        else
        {
            return GameType.Title;
        }
    }
}