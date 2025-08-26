using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICoroutineGameComponent
{
    IEnumerator InitializeCoroutine(); // 병렬 초기화 (UI, 사운드 등)
}