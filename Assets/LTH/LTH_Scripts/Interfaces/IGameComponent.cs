using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGameComponent
{
    void Initialize(); // 순차 초기화 (네트워크 등)
}