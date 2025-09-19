[System.Serializable]
public class VerdictConfig
{
    public float touchPerfect= 0.2f; //터치 블록 퍼펙트 판정 길이

    public float holdGood = 0.8f; //80%정도만 해도 Good 판정해주기

    public int verdictScoreMin = -10;
    public int verdictScoreMax = 15;

}