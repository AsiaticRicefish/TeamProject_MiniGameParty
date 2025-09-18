using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{


    public class Utils
    {

        //리스트 셔플
        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}