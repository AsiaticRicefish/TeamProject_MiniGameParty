using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ShootingGamePlayerPropertyKeys 
{
    public const string MyTurnIndex = "STGame_PlayerProperty_MyTurnIndex";
    public const string MyPrefabName = "STGame_PlayerProperty_MyPrefabName";

    public enum TaskType
    {
        Initialized = 0,
    }

    public static readonly Dictionary<TaskType, string> TaskKeys = new()
    {
        { TaskType.Initialized, "TaskDone_Initialized" },
    };
}
