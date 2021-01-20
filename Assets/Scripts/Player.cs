using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    private const float zOrderSpacing = 0.01f;
    private const float horizontalSpacing = 0.5f;
    public Card cardPrefab;
    private int turnCounter = 0;
    public Game CurrentGame { get; set; }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// The player's hand.
    /// </summary>
    public List<Card> Hand { get; private set; } = new List<Card>();

    /// <summary>
    /// The player's name.
    /// </summary>
    public string Name { get; private set; }

    protected int maxTrys = 10;

    protected Card lastCardPulled = Card.Empty;

    protected bool canDrawAgain = true;

    /// <summary>
    /// Creates a new player instance.
    /// </summary>
    /// <param name="name">The name of the player.</param>
    public void SetName(string name)
    {
        this.Name = name;
    }

    /// <summary>
    /// Asks the player to play a card.
    /// </summary>
    /// <remarks>
    /// The player has the option to draw a card without using a card from their hand. This will return a special card that allows the player to go again. However, based on the rules of the game if that card that is drawn in playable it must be played.
    /// </remarks>
    /// <param name="cardToPlayAgainst">The card that is currently being played against.</param>
    /// <returns>The card selected from the player's hand.</returns>
    public virtual Card PlayCard(Card myCard, bool addCardToHand)
    {
        var cardToPlayAgainst = CurrentGame.TopCardOnDiscard;
        if (cardToPlayAgainst.CanPlay(myCard))
        {
            if (myCard.Color == Card.CardColor.Wild)
            {
                AskAboutWild(myCard);
            }
            Hand.Remove(myCard);
            RestHand();
            return myCard;
        }
        // Only add it if it can't be played...
        if (addCardToHand)
        {
            AddCard(myCard);
        }

        if (turnCounter > maxTrys)
        {
            return Card.DrawAndSkip;
        }
        return Card.Empty;
    }

    /// <summary>
    /// Checks to see if the player is out of cards.
    /// </summary>
    /// <returns></returns>
    public bool CheckWin()
    {
        return Hand.Count == 0;
    }

    /// <summary>
    /// Add a card to the players hand.
    /// </summary>
    /// <param name="cardToAdd"></param>
    public virtual void AddCard(Card cardToAdd)
    {
        cardToAdd.FlipCardOver();
        AddCardToHand(cardToAdd);
    }

    protected void AddCardToHand(Card cardToAdd)
    {
        this.Hand.Add(cardToAdd);
        if (!canDrawAgain)
        {
            lastCardPulled = cardToAdd;
        }
    }

    public void FixupCardPositions()
    {
        float cardsStartingPositionBase = (Hand.Count - 1) * horizontalSpacing;
        int itemNumber = 0;
        var v3 = this.transform.position;
        float cardNumber = 0.0f;
        foreach (var cardToAdd in Hand)
        {
            float cardsStartingPosition = ((cardsStartingPositionBase + cardToAdd.Width) * -0.5f) + (cardToAdd.Width * 0.5f);
            cardNumber -= zOrderSpacing;
            cardToAdd.transform.SetParent(this.transform);
            cardToAdd.transform.SetPositionAndRotation(new Vector3(v3.x + (cardsStartingPosition + (itemNumber++ * horizontalSpacing)), v3.y + 0, v3.z + cardNumber), Quaternion.identity);
        }
    }

    /// <summary>
    /// Returns the player to a starting state.
    /// </summary>
    public void RestHand()
    {
        lastCardPulled = Card.Empty;
        canDrawAgain = true;
    }

    private void AskAboutWild(Card cardToPlay)
    {

        // In here we should bring up some UI component that displays the wild card colors
        bool shouldStop = false;
        while (!shouldStop)
        {
            shouldStop = true;
            Console.WriteLine("You're playing a wild card. What color do you want the next to be?");
            Console.WriteLine("1. Red");
            Console.WriteLine("2. Yellow");
            Console.WriteLine("3. Blue");
            Console.WriteLine("4. Green");
            if (int.TryParse(Console.ReadLine(), out int colorNumber))
            {
                shouldStop = ChooseWildColor(cardToPlay, colorNumber);
            }
        }
    }

    protected bool ChooseWildColor(Card cardToPlay, int colorNumber)
    {
        bool whatToReturn = true;
        switch (colorNumber)
        {
            case 1:
                cardToPlay.SetWildColor(Card.CardColor.Red);
                break;
            case 2:
                cardToPlay.SetWildColor(Card.CardColor.Yellow);
                break;
            case 3:
                cardToPlay.SetWildColor(Card.CardColor.Blue);
                break;
            case 4:
                cardToPlay.SetWildColor(Card.CardColor.Green);
                break;
            default:
                Console.WriteLine("Invalid selection, please choose a number between 1 and 4.");
                whatToReturn = false;
                break;
        }
        return whatToReturn;
    }

}
