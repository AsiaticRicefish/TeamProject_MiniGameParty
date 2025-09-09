using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct WindData
{
    public Vector3 direction;
    public float speed;

    public WindData(Vector3 dir, float spd)
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
