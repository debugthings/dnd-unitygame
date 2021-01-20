using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ComputerPlayer : Player
{

    
    private List<GameObject> dimmableCardList = new List<GameObject>();
    public AssetReference dimmableCardRef;
    void Awake()
    {
        
        InitializePlayer();
    }

    // Start is called before the first frame update
     void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public GameObject DimmableCardObject { get; set; }

    /// <summary>
    /// Dims the cards that are dimmable to give visible feedback that the computer player is "thinking"
    /// </summary>
    /// <param name="dim">True to dim, Flase to highlight</param>
    public void DimCards(bool dim)
    {
        Debug.Log($"Dimming cards for {this.Name}");
        foreach (var item in dimmableCardList)
        {
            if (item.tag == "Dimmable")
            {
                var allCards = item.GetComponent<SpriteRenderer>();
                var dimColor = dim ? UnityEngine.Color.gray : UnityEngine.Color.white;
                allCards.color = dimColor;
            }
        }
    }

    public override void AddCard(Card cardToAdd)
    {
        Debug.Log($"Adding card for {this.Name}");
        cardToAdd.Hide();
        cardToAdd.transform.SetParent(this.transform);
        dimmableCardList.Add(Instantiate(DimmableCardObject, transform));
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
                RemoveCard(cardToRetun);
                return cardToRetun;
            }
        }

        return cardToRetun;
    }

    public override void RemoveCard(Card cardToReturn)
    {
        Debug.Log($"Removing card for {this.Name}");

        var allCards = dimmableCardList.FirstOrDefault();
        if (allCards != null)
        {
            Destroy(allCards);
            dimmableCardList.RemoveAt(0);
        }
        base.RemoveCard(cardToReturn);
    }

    protected override void FixupCardPositions()
    {
        // We should only fixup the positions when a card is added or 
        Debug.Log($"Fixing up card positions for {this.Name}");

        float cardsStartingPositionBase = (dimmableCardList.Count - 1) * horizontalSpacing;
        int itemNumber = 0;
        float cardNumber = 0.0f;
        foreach (var cardToAdd in dimmableCardList)
        {
            if (cardToAdd.tag == "Dimmable")
            {
                var allCards = cardToAdd.GetComponent<SpriteRenderer>();
                var width = allCards.bounds.size.x;
                float cardsStartingPosition = ((cardsStartingPositionBase + width) * -0.5f) + (width * 0.5f);
                cardNumber -= zOrderSpacing;
                cardToAdd.transform.SetParent(this.transform);

                cardToAdd.transform.localPosition = new Vector3(
                    rand.NextFloat(-maxJitterTranslation, maxJitterTranslation) + (cardsStartingPosition + (itemNumber++ * horizontalSpacing)),
                    rand.NextFloat(-maxJitterTranslation, maxJitterTranslation) + 0,
                    cardNumber);
                cardToAdd.transform.eulerAngles += Vector3.forward * rand.NextFloat(-maxJitterRotation, maxJitterRotation);
            }
        }
    }
}
