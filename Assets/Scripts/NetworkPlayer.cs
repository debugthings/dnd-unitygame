using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class NetworkPlayer : LocalPlayer
{

    private List<GameObject> dimmableCardList = new List<GameObject>();
    public AssetReference dimmableCardRef;
    public GameObject playerNameObject;
    public GameObject gradeint;

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
    protected override void InitializePlayer()
    {
        base.InitializePlayer();
    }

    public GameObject DimmableCardObject { get; set; }

    public override void AddCard(Card cardToAdd)
    {
        cardToAdd.Hide();
        cardToAdd.transform.SetParent(this.transform);
        dimmableCardList.Add(Instantiate(DimmableCardObject, transform));
        base.AddCardToHand(cardToAdd);

    }

    public override Card PlayCard(Card myCard, Card cardToPlayAgainst, bool addToHand, bool removeCard)
    {
        if (myCard.CanPlay(cardToPlayAgainst))
        {
            Debug.Log($"We can play {myCard} against {cardToPlayAgainst}");
            myCard.Unhide();
            myCard.FlipCardOver();
            if (removeCard)
            {
                RemoveCard(myCard);
            }
            return myCard;
        }
        return Card.Empty;
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
        FixupCardPositions(false);
    }

    private void FixupCardPositions(bool hasLeft)
    {
        // We should only fixup the positions when a card is added or 
        Debug.Log($"Fixing up card positions for {this.Name}");

        // Add the player name and make it parallel to the screen
        var playerName = playerNameObject.GetComponent<TMPro.TMP_Text>();
        if (hasLeft)
        {
            playerName.text = $"{Name} (LEFT GAME)";
        }
        else
        {
            playerName.text = $"{Name} ({dimmableCardList.Count})";
        }
        playerName.autoSizeTextContainer = true;
        playerName.canvas.transform.Rotate(-playerName.canvas.transform.eulerAngles);


        // We should only fixup the positions when a card is added or 
        Debug.Log($"Fixing up card positions for {this.Name}");

        // Add the player name and make it parallel to the screen
        float cardsStartingPositionBase = Math.Min(dimmableCardList.Count - 1, MaxNumberOfCardsInRow - 1) * horizontalSpacing;
        int itemNumber = 0;
        float rowNumber = 0;
        float cardNumber = 0.0f;
        foreach (var cardToAdd in dimmableCardList)
        {
            // There should only be a specific amount of cards in each row
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

    public override void DimCards(bool dim)
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

    public override void PlayerLeftGame()
    {
        FixupCardPositions(true);
    }

    public override void ClearHand()
    {
        foreach (var item in dimmableCardList)
        {
            Destroy(item);
        }
        dimmableCardList.Clear();
        base.ClearHand();
    }

}
