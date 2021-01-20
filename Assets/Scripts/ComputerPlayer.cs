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
        var cardToRetun = Card.Empty;

        for (int i = 0; i < Hand.Count; i++)
        {
            if (Hand[i].CanPlay(cardToPlayAgainst))
            {
                if (Hand[i].Color == Card.CardColor.Wild)
                {
                    ChooseWildColor(Hand[i], new System.Random().Next(1, 5));
                    Hand[i].SetProps(Hand[i].Value, Hand[i].WildColor);
                }
                Console.WriteLine();
                cardToRetun = Hand[i];
                cardToRetun.FlipCardOver();
                cardToRetun.Unhide();
                cardToRetun.Dim(false);
                Hand.Remove(cardToRetun);
                playedCard = cardToRetun;
                return cardToRetun;
            }
        }

        RestHand();
        return cardToRetun;
    }
}
