using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct WindData
{
    public Vector3 direction;
    public int speed;

    public WindData(Vector3 dir, int spd)
    {
        direction = dir;
        speed = spd;
    }
}

public enum WindDirection
{
    Up,
    Down,
    Left,
    Right
}
