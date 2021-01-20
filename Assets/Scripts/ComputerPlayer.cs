using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputerPlayer : Player
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// Dims the cards that are dimmable to give visible feedback that the computer player is "thinking"
    /// </summary>
    /// <param name="dim">True to dim, Flase to highlight</param>
    public void DimCards(bool dim)
    {
        var allCards = transform.GetComponentsInChildren<SpriteRenderer>();
        foreach (var item in allCards)
        {
            if (item.tag == "Dimmable")
            {
                var dimColor = dim ? UnityEngine.Color.gray : UnityEngine.Color.white;
                item.color = dimColor;
            }
        }
    }


    public override void AddCard(Card cardToAdd)
    {
        cardToAdd.Hide();
        cardToAdd.transform.SetParent(this.transform);
        base.AddCardToHand(cardToAdd);
    }

    public override Card PlayCard(Card cardToPlayAgainst, bool addToHand)
    {
        // For this action we'll check to see if we have any cards to play
        // If we don't we need to return an empty card so we have the correct game action
        var cardToRetun = Card.Empty;

        for (int i = 0; i < Hand.Count; i++)
        {
            if (Hand[i].CanPlay(cardToPlayAgainst))
            {
                if (Hand[i].Color == Card.CardColor.Wild)
                {
                    ChooseWildColor(Hand[i], new System.Random().Next(1, 5));
                }
                Console.WriteLine();
                cardToRetun = Hand[i];
                cardToRetun.Unhide();
                cardToRetun.FlipCardOver();
                Hand.Remove(cardToRetun);
                playedCard = cardToRetun;
                return cardToRetun;
            }
        }

        return cardToRetun;
    }
}
