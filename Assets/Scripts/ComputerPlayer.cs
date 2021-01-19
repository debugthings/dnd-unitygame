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
    public override void AddCard(Card cardToAdd)
    {
        cardToAdd.Hide();
        base.AddCardToHand(cardToAdd);
    }
    public override Card PlayCard(Card cardToPlayAgainst, bool addToHand)
    {
        System.Threading.Thread.Sleep(1000);
        var cardToRetun = Card.Empty;

        for (int i = 0; i < Hand.Count; i++)
        {
            if (Hand[i].CanPlay(cardToPlayAgainst))
            {
                Console.Write($"{Name} played ");
                Hand[i].WriteCard(false);
                if (Hand[i].Color == Card.CardColor.Wild)
                {
                    ChooseWildColor(Hand[i], new System.Random().Next(1, 5));
                }
                Console.WriteLine();
                cardToRetun = Hand[i];
                Hand.Remove(cardToRetun);
                return cardToRetun;
            }
        }

        if (canDrawAgain)
        {
            canDrawAgain = false;
            return Card.DrawOnce;
        }


        if (lastCardPulled.CanPlay(cardToPlayAgainst))
        {
            cardToRetun = lastCardPulled;
            Console.WriteLine("Card pulled from the deck is a match so it must be played.");
            Hand.Remove(lastCardPulled);
            RestHand();
            return cardToRetun;
        }

        RestHand();
        return cardToRetun;
    }
}
