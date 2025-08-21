using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// �̴ϰ��� Jenga���� �� �÷��̾��� �����͸� �����ϴ� Ŭ����
/// GamePlayer���� �⺻���� ����(UID, �г��� ��)�� ����ǰ�,
/// �� �̴ϰ��� �� ���Ǵ� �����ʹ� ���� �и��ϴ� ���� ���� �� ���� ������ Ŭ����
/// </summary>

[Serializable]
public class JengaPlayerData
{
    public int score = 0;
    public bool isAlive = true;
    public bool isFinished = false;

    public Vector3 towerPosition; // ���� Ÿ�� ��ġ
    public Vector3 lastTouchPosition;

    public float gameStartTime; // ���� ���� �ð� (���� ����)
    public float lastSuccessTime; // ������ ��� ���� �ð� (or �� �ҿ� �ð�)
}