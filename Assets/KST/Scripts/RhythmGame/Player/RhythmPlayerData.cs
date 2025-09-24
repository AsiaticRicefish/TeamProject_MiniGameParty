using System;

[Serializable]
public class RhythmPlayerData
{

    public int score = 0;
    public int verdictScore;
    public int totalScore;

    public void CalcTotal()
    {
        totalScore = score + verdictScore;
        if (totalScore < 0) totalScore = 0;
    }
}
