﻿using Assets.Scripts.Common;
using Photon.Realtime;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LocalPlayer : LocalPlayerBase<Player>
{
    public Button unoButton;
    public Button challengeButton;
    public Toggle dimmableCardToggle;
    public Transform gradient;

    public override void PlayerLeftGame()
    {
    }
    
    public void ItsYourTurn(bool toggle)
    {
        gradient.gameObject.SetActive(toggle);
    }

    public void DimCardsThatCantBePlayed(bool toggle, Card currentCard)
    {
        foreach (var item in Hand)
        {
            if (!item.CanPlay(currentCard))
            {
                item.Dim(toggle);
            }
        }
    }

    public override bool CanCallUno(Card cardToCheck)
    {
        if (base.CanCallUno(cardToCheck))
        {
            CustomLogger.Log($"{Name} Set Uno Button Green");
            Color greenColor = new Color(0.3f, 1.0f, 0.0f, 1.0f);
            ChangeUnoButtonColor(greenColor);
        }
        return CalledUno;
    }

    public  void ChangeUnoButtonColor(Color greenColor)
    {
        var buttonColors = unoButton.colors;
        buttonColors.normalColor = greenColor;
        unoButton.colors = buttonColors;
    }

    public override bool AnimateCardToPlayer(Card cardToAnimate)
    {
        return cardToAnimate.AnimateToPosition(transform);
    }

    public override bool AnimateCardToDiscardDeck(Card cardToAnimate, CardDeck discardDeck)
    {
        return cardToAnimate.AnimateToPosition(discardDeck.transform);

    }
}