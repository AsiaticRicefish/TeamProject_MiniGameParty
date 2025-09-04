using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ShootingGamePropertyKeys
{
    public const string State = "STGame_State";
    public const string Turn = "STGame_Turn";
    public const string Round = "STGame_Round";
    public const string PlayerScore_Prefix = "STGame_Score_Player_"; // STGame_Score_Player_ + uid

    //카드매니저 사용 룸 프로퍼티 키 
    public const string KEY_DECK_VALUES = "deckValues";
    public const string KEY_CARD_OWNERS = "cardOwners";
    public const string KEY_STATE = "state";
    public const string KEY_TURN_ORDER = "turnOrder";
}
