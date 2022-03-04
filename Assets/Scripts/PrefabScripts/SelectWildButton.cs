using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Common;
using UnityEngine;

public class SelectWildButton : MonoBehaviour
{
    public static Card CardToChange { get; set; }

    public static Action<Card> ReturnCard;

    public void OnButtonPress(string cardColor)
    {
        CustomLogger.Log($"Wild card button pressed with {cardColor}");
        CardToChange.SetWildColor(cardColor);
        ReturnCard(CardToChange);
    }  // Start is called before the first frame update
}
