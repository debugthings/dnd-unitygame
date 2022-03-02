using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class NetworkPlayer : LocalPlayerBase<Player>
{
    private List<GameObject> dimmableCardList;
    public AssetReference dimmableCardRef;
    public TextMeshProUGUI playerNameObject;
    public GameObject gradeint;
    public GameObject UnoTitle;


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
        dimmableCardList = new List<GameObject>();
        base.InitializePlayer();
    }

    public GameObject DimmableCardObject { get; set; }

    /// <summary>
    /// Creates a new player instance.
    /// </summary>
    /// <param name="name">The name of the player.</param>
    public override void SetName(string name, string additionalInfo)
    {
        playerNameObject.text = $"{Name} ({additionalInfo})";
        playerNameObject.autoSizeTextContainer = true;
        playerNameObject.canvas.transform.Rotate(-playerNameObject.canvas.transform.eulerAngles);
        base.SetName(name, additionalInfo);
    }

    public override void AddCard(Card cardToAdd)
    {

        cardToAdd.Hide();
        cardToAdd.transform.SetParent(this.transform);
        base.AddCardToHand(cardToAdd);
        dimmableCardList.Add(Instantiate(DimmableCardObject, transform));
        CustomLogger.Log($"Dimmable card list count {dimmableCardList.Count}");
    }

    public override Card PlayCard(Card cardToPlay, Card cardToPlayAgainst, bool removeFromHand = true)
    {
        CustomLogger.Log($"Playing card {cardToPlay} with Id {cardToPlay.CardRandom}");
        if (cardToPlay.CanPlay(cardToPlayAgainst))
        {
            CustomLogger.Log($"We can play {cardToPlay} against {cardToPlayAgainst}");
            cardToPlay.Unhide();
            cardToPlay.FlipCardOver();
            if (removeFromHand)
            {
                RemoveCard(cardToPlay);
            }
            return cardToPlay;
        }
        return Card.Empty;
    }

    public override void RemoveCard(Card cardToRemove)
    {
        CustomLogger.Log($"Removing dimmed card from {this.Name} hand in NetworkPlayer");

        var dimmableCardToRemove = dimmableCardList.FirstOrDefault();
        if (dimmableCardToRemove != null)
        {
            Destroy(dimmableCardToRemove);
            dimmableCardList.RemoveAt(0);
        }
        base.RemoveCard(cardToRemove);
    }

    public override void FixupCardPositions()
    {
        FixupCardPositions(PlayerStatus.ACTIVE);
    }

    private void FixupCardPositions(PlayerStatus hasLeft)
    {
        // We should only fixup the positions when a card is added or 
        // CustomLogger.Log($"Fixing up card positions for {this.Name}");

        // In some cases we can see wh
        if (dimmableCardList.Count < Hand.Count)
        {
            for (int i = 0; i < Hand.Count - dimmableCardList.Count; i++)
            {
                dimmableCardList.Add(Instantiate(DimmableCardObject, transform));
            }
        }
        else if (dimmableCardList.Count > Hand.Count)
        {
            for (int i = 0; i < dimmableCardList.Count - Hand.Count; i++)
            {
                var dimmableCardToRemove = dimmableCardList.FirstOrDefault();
                if (dimmableCardToRemove != null)
                {
                    Destroy(dimmableCardToRemove);
                    dimmableCardList.RemoveAt(0);
                }
            }
        }

        // Add the player name and make it parallel to the screen
        switch (hasLeft)
        {
            case PlayerStatus.INACTIVE:
                SetName(Name, "DISCONNECTED");
                break;
            case PlayerStatus.LEFT:
                SetName(Name, "LEFT GAME");
                break;
            case PlayerStatus.ACTIVE:
            default:
                SetName(Name, dimmableCardList.Count.ToString());
                break;
        }

        if (Hand.Count > 1)
        {
            UnoTitle.SetActive(false);
        }

        // We should only fixup the positions when a card is added or 
        // CustomLogger.Log($"Fixing up card positions for {this.Name}");

        // Add the player name and make it parallel to the screen
        float cardsStartingPositionBase = Math.Min(dimmableCardList.Count - 1, MaxNumberOfCardsInRow - 1) * horizontalSpacing;
        int itemNumber = 0;
        float rowNumber = 0;
        float cardNumber = 0.0f;
        foreach (var cardToAdd in dimmableCardList)
        {
            // Do a simple hide of the dimmable card if the playable card is inflight
            if (!Hand[itemNumber].IsInFlight)
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
    }

    public override void DimCards(bool dim)
    {
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
        FixupCardPositions(PlayerStatus.LEFT);
    }

    public override void PlayerDisconnected()
    {
        FixupCardPositions(PlayerStatus.INACTIVE);
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

    public override bool CanCallUno(Card cardToCheck)
    {
        if (base.CanCallUno(cardToCheck))
        {
            CustomLogger.Log($"{Name} Set Uno Title Active");
            UnoTitle.SetActive(true);
        }
        return CalledUno;
    }

    public override bool AnimateCardToPlayer(Card cardToAnimate)
    {
        return cardToAnimate?.AnimateToPosition(transform) ?? false;
    }

    public override bool AnimateCardToDiscardDeck(Card cardToAnimate, CardDeck discardDeck)
    {
        var dimmableCardToRemove = dimmableCardList?.FirstOrDefault();
        var cardAnimator = dimmableCardToRemove?.GetComponent<CardAnimator>();
        return cardAnimator?.AnimateToPosition(discardDeck.transform) ?? false;
    }
}
