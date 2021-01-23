using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectWildButton : MonoBehaviour
{
    public static Card CardToChange { get; set; }

    public static Action<Card> ReturnCard;

    public void OnButtonPress(string cardColor)
    {
        CardToChange.SetWildColor(cardColor);
        ReturnCard(CardToChange);
        Debug.Log("Button clicked " + 1 + " times.");
    }  // Start is called before the first frame update
}
