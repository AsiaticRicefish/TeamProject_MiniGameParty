using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICoroutineGameComponent
{
    IEnumerator InitializeCoroutine(); // ���� �ʱ�ȭ (UI, ���� ��)
}