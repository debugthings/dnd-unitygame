using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using TMPro;
using System.Threading.Tasks;
using System.Threading;
using Photon.Realtime;
using Photon.Pun;
using ExitGames.Client.Photon;
using Assets.Scripts;
using UnityEngine.UI;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using System.Collections.Concurrent;
using Assets.Scripts.Common;

public partial class Game : MonoBehaviourPunCallbacks, IConnectionCallbacks
{

    private async void StartGame()
    {
        gameStarted = true;
        stopGame = false;

        // Remove the player ready flag so the new game doesn't start right away
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable() { [Constants.PlayerReady] = null });

        UpdateLog("Please wait while all players sync.");

        // What we want to do here is have a game where the number of decks is in some multiple of the number of players
        // However in the event that we have n-(n/2) > 2 players that would trigger a new deck, we should opt to increase the number of decks.
        // For example, if we have 4 players per deck and the game has 7 players, we should add another deck to be sure.
        int baseNumber = (numOfPlayers - (numOfPlayers % gameOptions.PlayersPerDeck)) / gameOptions.PlayersPerDeck;
        if ((numOfPlayers % gameOptions.PlayersPerDeck) > gameOptions.PlayersPerDeck / 2)
        {
            baseNumber++;
        }

        // We should always have 1 deck but a max of max decks (5)
        numberOfDecks = UnityEngine.Mathf.Clamp(baseNumber, gameOptions.BaseNumberOfDecks, gameOptions.MaxDecks);

        // Remove cards so we can just use the loops below
        FixupCardsPerColor();

        CustomLogger.Log("Build Number Cards");

        // Build the deck and create a random placement
        // Shuffling idea taken from https://blog.codinghorror.com/shuffling/
        // There are two sets of numbers and action cards per color per deck
        for (int num = 0; num < (numberOfDecks * 1); num++)
        {
            for (int colorIndex = 0; colorIndex < cardColorArray.Length; colorIndex++)
            {
                for (int cardValueIndex = 0; cardValueIndex < cardValuesArray.Count; cardValueIndex++)
                {
                    CreateCardOnDealDeck(rand.NextInt(), cardColorArray[colorIndex], cardValuesArray[cardValueIndex]);
                }
            }
        }

        CustomLogger.Log("Build Zero Cards");

        // There is one set of Zero cards per color per deck
        for (int num = 0; num < numberOfDecks; num++)
        {
            for (int colorIndex = 0; colorIndex < cardColorArray.Length; colorIndex++)
            {
                CreateCardOnDealDeck(rand.NextInt(), cardColorArray[colorIndex], Card.CardValue.Zero);
            }
        }

        CustomLogger.Log("Build Wild Cards");

        // Generate the correct number of wild cards 4 cards per number of decks...
        for (int i = 0; i < 4 * numberOfDecks; i++)
        {
            CreateCardOnDealDeck(rand.NextInt(), Card.CardColor.Wild, Card.CardValue.Wild);
            CreateCardOnDealDeck(rand.NextInt(), Card.CardColor.Wild, Card.CardValue.DrawFour);
        }

        // Use this to generate a proper z-order of the deck
        dealDeck.MaxStackDepth = dealDeck.Count() * cardStackZOrderOffset;
        discardDeck.MaxStackDepth = dealDeck.Count() * cardStackZOrderOffset;

        CustomLogger.Log("Build deck and add jitter");
        // Push the cards in the random order to a Stack...
        dealDeck.Shuffle();

        CustomLogger.Log("Deal cards to players");

        // For now we'll use the master client as the dealer for the deck.
        // This will make sure that all players start with the correct cards.

        // Player next to the master player
        Player nextToDealer = default;

        // Set the player order to be forward always
        playerRotation.Forward();
        // For each player in the player list find the one that is next to the dealer.
        foreach (var item in PhotonNetwork.PlayerList)
        {
            if (item.IsMasterClient)
            {
                nextToDealer = item.GetNext();
                break;
            }
        }

        // Set the circular list to the correct player grouping.
        playerRotation.SetPlayer(playerRotation.FindPlayerByNetworkPlayer(nextToDealer));

        // Deal out the players 
        for (int i = 0; i < gameOptions.NumberOfCardsToDeal; i++)
        {
            for (int j = 0; j < numOfPlayers; j++)
            {
                // The circular list allows us to start dealing from the "first" position
                var cardToDeal = TakeFromDealPile();
                var currentPlayer = playerRotation.Current();
                currentPlayer.AnimateCardToPlayer(cardToDeal);
                currentPlayer.AddCard(cardToDeal);
                playerRotation.Next();
            }
        }

        CustomLogger.Log("Dim computer player cards");
        foreach (var item in playerRotation)
        {
            if (item != playerRotation.Current())
            {
                item.DimCards(true);
            }
        }

        CustomLogger.Log("Play top card");
        PutCardOnDiscardPile(TakeFromDealPile(), true);

        // When we're here we know that we're not actively animating the card loads and the game should execute at about the same pace.
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable() { [Constants.PlayerGameLoaded] = true });
        while (CheckAllPlayersAreGameReady())
        {
            await Task.Delay(20);
        }
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable() { [Constants.PlayerGameLoaded] = null });

        // Run the first move. This will ensure the player gets the correct game action applies to them
        FirstPlay(playerRotation.Current());

        TogglePlayableDimming();

        CustomLogger.Log("Leaving Start()");
    }

    /// <summary>
    /// Performs the primary game mechanic of checking the cards and the player that is performing the action.
    /// </summary>
    /// <param name="cardBeingPlayed"></param>
    /// <param name="playerMakingMove"></param>
    public void GameLoop(Card cardBeingPlayed, LocalPlayerBase<Player> playerMakingMove)
    {
        if (cardBeingPlayed != Card.Empty && playerMakingMove == playerRotation.Current() && !stopGame)
        {
            CustomLogger.Log($"Card {cardBeingPlayed} is in the GameLoop");
            playerMakingMove.AnimateCardToDiscardDeck(cardBeingPlayed, discardDeck);
            playerMakingMove.FixupCardPositions();
            PerformGameAction(cardBeingPlayed, false);
        }
        else
        {
            UpdateLog($"{playerMakingMove.Name} did not draw a playable card! {playerRotation.PeekNext().Name} is next!");
        }

        // Every time we run a GameLoop we're confident that we need to check a winner or we need to move to the next player.
        if (playerMakingMove.CheckWin(playerRotation.Count))
        {
            ShowWin(playerMakingMove);
        }
        else
        {
            AdvanceNextPlayer();
        }
    }

    private void FirstPlay(LocalPlayerBase<Player> firstPlayerAfterDealer)
    {
        var firstCard = discardDeck.PeekTopCard();
        CustomLogger.Log($"First card value on first play {firstCard}");
        switch (firstCard.Value)
        {
            case Card.CardValue.Skip:
            case Card.CardValue.Reverse:
            case Card.CardValue.DrawTwo:
                // If these actions are first we need to start the game loop since the first player will be skipped
                PerformGameAction(firstCard, true);
                AdvanceNextPlayer();
                break;
            case Card.CardValue.DrawFour:
                // When we have a draw four we need to put it back into the deck.
                var Draw4Card = discardDeck.TakeTopCard();
                dealDeck.PutCardBackInDeckInRandomPoisiton(Draw4Card, 3, 50);
                // Lets make sure we don't miraculously get another D4
                var cardToCheck = dealDeck.PeekTopCard();
                while (cardToCheck.Value == Card.CardValue.DrawFour)
                {
                    dealDeck.PutCardBackInDeckInRandomPoisiton(discardDeck.TakeTopCard(), 3, 50);
                    cardToCheck = dealDeck.PeekTopCard();
                }
                discardDeck.AddCardToDeck(dealDeck.TakeTopCard(), true);
                break;
            default:
                UpdateLog($"First play to {firstPlayerAfterDealer.Name}!");
                break;
        }
    }

    /// <summary>
    /// Pulls a card from the deal pile and will automatically swap the discard pile if needed
    /// </summary>
    /// <returns></returns>
    public Card TakeFromDealPile()
    {
        CustomLogger.Log("Enter");
        try
        {
            if (dealDeck.Count > 0)
            {
                return dealDeck.TakeTopCard();
            }
            if (dealDeck.Count == 0 && discardDeck.Count > 1)
            {
                CustomLogger.Log($"Swapping decks");
                dealDeck.SwapCardsFromOtherDeck(discardDeck);
                return dealDeck.TakeTopCard();
            }
            return Card.Empty;
        }
        finally
        {
            CustomLogger.Log("Exit");

        }
    }

    /// <summary>
    /// Puts the card on the discard pile and handles the flupping and translation of the card
    /// </summary>
    /// <param name="cardToDiscard"></param>
    /// <param name="dontCheckEquals"></param>
    /// 
    /// <returns></returns>
    public GameAction PutCardOnDiscardPile(Card cardToDiscard, bool dontCheckEquals)
    {
        var cardAction = ConvertCardToAction(cardToDiscard.Value);
        // Some special cases where we don't push the card to the pile since it's a 
        if (cardAction == GameAction.DrawAndPlayOnce || cardAction == GameAction.DrawAndSkip)
        {
            CustomLogger.Log($"Card action is {cardAction}. Just return and don't add the card to the discard deck.");
            return cardAction;
        }

        // If we have a card that will play we need to continue on
        if (dontCheckEquals || cardToDiscard.CanPlay(discardDeck.PeekTopCard()))
        {
            CustomLogger.Log($"Card action is {cardAction}. Add the card to the discard deck.");
            // Take the card that is in play and put it on top.
            discardDeck.AddCardToDeck(cardToDiscard, true);
            return cardAction;
        }
        return GameAction.NextPlayer;
    }


    /// <summary>
    /// Called from the mouse handler to play the actual card. In here we'll handle what happens when a wild card is shown.
    /// </summary>
    /// <param name="cardObject"></param>
    public void PlayClickedCard(Card cardObject)
    {
        try
        {
            CustomLogger.Log("Enter");
            if (PlayerCanMakeMove())
            {
                var cardDeck = cardObject.GetComponentInParent<CardDeck>();
                var player = cardObject.GetComponentInParent<LocalPlayer>();
                Card cardToPlay = Card.Empty;

                if (cardDeck is CardDeck && cardDeck.name == "DealDeck")
                {
                    CustomLogger.Log("Player doubleclicked the deal deck to draw a card. Taking card from deal pile");

                    // If we're in play and the player decides to draw, either the card will be played or added to the hand.
                    // When we're in networked mode we'll need to take into account that anyone can double click the deck so we'll need to make sure
                    // the click originated from the player.
                    cardToPlay = TakeFromDealPile();
                    CustomLogger.Log("Adding card to hand");
                    LocalPlayerReference.AddCard(cardToPlay);

                    // If the card can't be played then we should add it to the hand.
                    if (cardToPlay != null && cardToPlay != Card.Empty && !cardToPlay.CanPlay(discardDeck.PeekTopCard()))
                    {

                    }
                    else
                    {
                        CustomLogger.Log("Did not add card to hand.");
                    }
                }
                else if (player is LocalPlayer)
                {
                    CustomLogger.Log($"Player double clicked {cardObject}");
                    // If we're here we've likely tried to play a card. We need to check to see the card is okay to play.
                    cardToPlay = LocalPlayerReference.PlayCard(cardObject, discardDeck.PeekTopCard(), false, false);
                }
                else if (cardDeck is CardDeck && cardDeck.name == "DiscardDeck")
                {
                    CustomLogger.Log($"Player double clicked the discard deck, do nothing.");
                }


                // Do the game action on the playable card.
                if (cardToPlay != Card.Empty && cardToPlay != null)
                {
                    if (cardToPlay.Color == Card.CardColor.Wild)
                    {
                        CustomLogger.Log($"Player double clicked a wild card, showing the wild card screen.");
                        HandleWildCard(cardToPlay);
                    }
                    else
                    {
                        SendMoveToRPC(cardToPlay, LocalPlayerReference);
                    }
                }
            }
            CustomLogger.Log("Exit");
            CustomLogger.Log("", "");
        }
        catch (Exception ex)
        {
            CustomLogger.Log("EXCEPTION");
            CustomLogger.Log(ex.StackTrace);
            CustomLogger.Log(ex.Message);
            throw;
        }
    }

    private void HandleWildCard(Card cardToPlay)
    {
        audioSource.clip = wildCardPopup;
        audioSource.Play();
        var wildCardPrefab = Instantiate(wildCardSelectPrefab, transform);
        SelectWildButton.CardToChange = cardToPlay;
        SelectWildButton.ReturnCard = (card) =>
        {
            SendMoveToRPC(card, LocalPlayerReference);
            Destroy(wildCardPrefab);
        };
    }

    /// <summary>
    /// The business end of the game mechanics. Will apply the actions to the correct players
    /// </summary>
    /// <param name="cardBeingPlayed"></param>
    /// <param name="firstPlay"></param>
    public void PerformGameAction(Card cardBeingPlayed, bool firstPlay = false)
    {
        var currentPlayer = playerRotation.Current();
        var nextPlayer = firstPlay ? playerRotation.Current() : playerRotation.PeekNext();
        // If it is not a player's turn we should just skip

        // Take the player's card and put it on the discard pile. 
        GameAction ga = PutCardOnDiscardPile(cardBeingPlayed, false);
        CustomLogger.Log($"Card {cardBeingPlayed} is in the Discard Pile");

        if (cardBeingPlayed.Color == Card.CardColor.Wild && !firstPlay)
        {
            CustomLogger.Log($"Card {cardBeingPlayed} is a wild card");
            cardBeingPlayed.SetProps(cardBeingPlayed.CardRandom, cardBeingPlayed.Value, cardBeingPlayed.WildColor);
        }

        var nextSound = rand.NextUInt(0, (uint)cardPlaySounds.Count);
        AudioClip clipToPlay = cardPlaySounds[(int)nextSound];

        switch (ga)
        {
            case GameAction.Reverse:
                playerRotation.SwapDirections();

                if (playerRotation.Count == 2)
                {
                    UpdateLog($"{currentPlayer.Name} played a {cardBeingPlayed}! Skipping {nextPlayer}!");
                    playerRotation.Next();
                }

                if (firstPlay)
                {
                    UpdateLog($"Starting in reverse direction. {playerRotation.PeekNext().Name} is first!");
                }
                else
                {
                    UpdateLog($"{currentPlayer.Name} reversed play, {playerRotation.PeekNext().Name} is next!");
                }
                break;

            case GameAction.Skip:
                if (firstPlay)
                {
                    UpdateLog($"{currentPlayer.Name} was skipped. {playerRotation.PeekNext().Name} is first!");
                }
                else
                {
                    UpdateLog($"{currentPlayer.Name} skipped {nextPlayer.Name}!");
                    playerRotation.Next();
                }
                break;

            case GameAction.DrawTwo:
                for (int i = 0; i < 2; i++)
                {
                    nextPlayer.AddCard(TakeFromDealPile());
                }

                if (firstPlay)
                {
                    UpdateLog($"Draw Two on first play! {nextPlayer.Name} takes 2 cards! {playerRotation.PeekNext().Name} starts!");
                }
                else
                {
                    UpdateLog($"{nextPlayer.Name} must Draw Two! Skipping {nextPlayer.Name}!");
                    playerRotation.Next();
                }
                break;
            case GameAction.DrawFour:
                for (int i = 0; i < 4; i++)
                {
                    nextPlayer.AddCard(TakeFromDealPile());
                }
                UpdateLog($"{nextPlayer.Name} must Draw Four! Skipping {nextPlayer.Name}!");
                playerRotation.Next();
                break;
            case GameAction.DrawAndSkip:
                UpdateLog($"{currentPlayer.Name} did not draw a playable card! {playerRotation.PeekNext().Name} is next!");
                currentPlayer.AddCard(TakeFromDealPile());
                break;
            case GameAction.DrawAndPlayOnce:
                CustomLogger.Log($"{currentPlayer.Name} chose DrawAndPlayOnce!");
                currentPlayer.AddCard(TakeFromDealPile());
                // Move the player cursor back to the previous player so this player can go again.
                playerRotation.Prev();
                break;
            case GameAction.Wild:
                if (!firstPlay)
                {
                    UpdateLog($"{currentPlayer.Name} played a {cardBeingPlayed.WildColor} Wild!");
                }
                break;
            case GameAction.NextPlayer:
            // In this case we just let it slide to the next player by using the loop.
            default:
                UpdateLog($"{currentPlayer.Name} played a {cardBeingPlayed}");
                break;
        }

        audioSource.clip = clipToPlay;
        audioSource.Play();

    }

}
