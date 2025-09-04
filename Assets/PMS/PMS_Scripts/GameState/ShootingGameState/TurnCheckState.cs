using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnCheckState : ShootingGameState
{
    public override void Enter() 
    {
        Debug.Log("[TurnCheckState] - TurnCheckState Enter");
    }
    public override void Update() { }
    public override void Exit() 
    {
        Debug.Log("[TurnCheckState] - TurnCheckState Exit");
    }
}
