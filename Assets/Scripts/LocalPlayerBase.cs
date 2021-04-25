using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class LocalPlayerBase<T> : MonoBehaviour
{
    protected const float zOrderSpacing = 0.01f;
    protected const float horizontalSpacing = 0.8f;
    protected const float maxJitterTranslation = 0.06f;
    protected const float maxJitterRotation = 2.0f;
    protected Unity.Mathematics.Random rand = new Unity.Mathematics.Random();
    public Game CurrentGame { get; set; }

    public event EventHandler<Card> HandChangedEvent;



    /// <summary>
    /// The player's hand.
    /// </summary>
    public List<Card> Hand { get; private set; } = new List<Card>();

    public int MaxNumberOfCardsInRow { get; set; } = 15;

    /// <summary>
    /// The player's name.
    /// </summary>
    public string Name { get; private set; }

    protected int maxTrys = 10;

    protected Card lastCardPulled = Card.Empty;

    protected Vector3 startingPosition = Vector3.negativeInfinity;

    public T NetworkPlayer { get; protected set; } = default(T);

    // Start is called before the first frame update
    void Start()
    {

    }

    void Awake()
    {
        InitializePlayer();
    }
    // Update is called once per frame
    void Update()
    {

    }

    public void SetNetworkPlayerObject(T networkPlayerObject)
    {
        this.NetworkPlayer = networkPlayerObject;
    }

    /// <summary>
    /// Creates a new player instance.
    /// </summary>
    /// <param name="name">The name of the player.</param>
    public void SetName(string name)
    {
        this.Name = name;
        this.name = name;
    }

    public Photon.Realtime.Player Player { get; set; }

    /// <summary>
    /// Asks the player to play a card.
    /// </summary>
    /// <remarks>
    /// The player has the option to draw a card without using a card from their hand. This will return a special card that allows the player to go again. However, based on the rules of the game if that card that is drawn in playable it must be played.
    /// </remarks>
    /// <param name="cardToPlayAgainst">The card that is currently being played against.</param>
    /// <returns>The card selected from the player's hand.</returns>
    public virtual Card PlayCard(Card myCard, Card cardToPlayAgainst, bool addCardToHand, bool removeFromHand = true)
    {
        if (myCard.CanPlay(cardToPlayAgainst))
        {
            Debug.Log($"We can play {myCard} against {cardToPlayAgainst}");
            if (removeFromHand)
            {
                RemoveCard(myCard);
            }
            return myCard;
        }

        // Only add it if it can't be played...
        if (addCardToHand)
        {
            AddCard(myCard);
        }
        return Card.Empty;
    }

    protected void HandChanged(object sender, Card card)
    {
        FixupCardPositions();
    }

    public virtual void RemoveCard(Card cardToRemove)
    {
        Debug.Log($"Removing {cardToRemove} from {this.Name} hand");
        Hand.Remove(cardToRemove);
        HandChangedEvent(this, cardToRemove);
    }

    /// <summary>
    /// Checks to see if the player is out of cards.
    /// </summary>
    /// <returns></returns>
    public bool CheckWin(int playersLeft)
    {
        return playersLeft == 1 || Hand.Count == 0;
    }

    /// <summary>
    /// Add a card to the players hand and if it's a human player flip it over.
    /// </summary>
    /// <param name="cardToAdd"></param>
    public virtual void AddCard(Card cardToAdd)
    {
        cardToAdd.SetCardFaceUp(true);
        AddCardToHand(cardToAdd);
    }

    /// <summary>
    /// Adds the card to the players hand and triggers the <see cref="HandChangedEvent"/>
    /// </summary>
    /// <param name="cardToAdd">The card to add</param>
    protected void AddCardToHand(Card cardToAdd)
    {
        Debug.Log($"Adding card {cardToAdd} for {this.Name}");
        this.Hand.Add(cardToAdd);
        HandChangedEvent(this, cardToAdd);
    }

    /// <summary>
    /// Take all of the cards that are in our hand and parent them to us. As well we should place them in a pattern
    /// </summary>
    protected virtual void FixupCardPositions()
    {
        // We should only fixup the positions when a card is added or 
        Debug.Log($"Fixing up card positions for {this.Name}");

        if (startingPosition.Equals(Vector3.negativeInfinity))
        {
            this.startingPosition = this.transform.localPosition;
        }
        // If we're showing multiple rows we need to shift up by some number
        if (Hand.Count > MaxNumberOfCardsInRow)
        {
            var shiftUpby = ((Hand.Count - (Hand.Count % MaxNumberOfCardsInRow)) / MaxNumberOfCardsInRow) * .8f;
            this.transform.localPosition = startingPosition + (shiftUpby * Vector3.up);
        }

        float cardsStartingPositionBase = Math.Min(Hand.Count - 1, MaxNumberOfCardsInRow - 1) * horizontalSpacing;
        int itemNumber = 0;
        float rowNumber = 0;
        float cardNumber = 0.0f;
        foreach (var cardToAdd in Hand)
        {
            // There should only be 5 cards in each row
            if (itemNumber > 0 && itemNumber % MaxNumberOfCardsInRow == 0)
            {
                itemNumber = 0;
                rowNumber++;
            }

            // Increase the z-order for every row sligthly so we can see them overlap
            cardNumber += rowNumber * -0.01f;

            if (cardToAdd.tag == "Dimmable")
            {
                var allCards = cardToAdd.GetComponent<SpriteRenderer>();
                var width = allCards.bounds.size.x;
                float cardsStartingPosition = ((cardsStartingPositionBase + width) * -0.5f) + (width * 0.5f);
                cardNumber -= zOrderSpacing;
                cardToAdd.transform.SetParent(this.transform);

                cardToAdd.transform.localPosition = new Vector3(
                    rand.NextFloat(-maxJitterTranslation, maxJitterTranslation) + (cardsStartingPosition + (itemNumber++ * horizontalSpacing)),
                    rand.NextFloat(-maxJitterTranslation, maxJitterTranslation) + (rowNumber * -1.0f),
                    cardNumber);
                cardToAdd.transform.eulerAngles += Vector3.forward * rand.NextFloat(-maxJitterRotation, maxJitterRotation);
            }
        }
    }

    /// <summary>
    /// Initializes the default player state.
    /// </summary>
    /// <remarks>
    /// By default this will initialize the random jitter and set the default <see cref="HandChangedEvent"/> to the protected <see cref="HandChanged(object, Card)"/> handler which updates the card poisitions
    /// </remarks>
    protected virtual void InitializePlayer()
    {
        rand.InitState();
        this.HandChangedEvent += HandChanged;
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

    public virtual void DimCards(bool dim)
    {
        Debug.Log($"Dimming cards for {this.Name}");
        foreach (var item in Hand)
        {
            if (item.tag == "Dimmable")
            {
                var allCards = item.GetComponent<SpriteRenderer>();
                var dimColor = dim ? UnityEngine.Color.gray : UnityEngine.Color.white;
                allCards.color = dimColor;
            }
        }
    }

    public abstract void PlayerLeftGame();
}