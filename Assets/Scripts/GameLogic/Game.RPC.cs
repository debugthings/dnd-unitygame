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
    private async void SendMoveToRPC(Card cardToPlay, LocalPlayerBase<Player> playerMakingMove)
    {
        var updateGuid = Guid.NewGuid().ToString();
        CustomLogger.Log($"Calling SendMoveToAllPlayers with {updateGuid}");
        await Task.Delay(100); // Adding a simple delay to help throttle the messages coming in
        photonView.RPC("SendMoveToAllPlayers", RpcTarget.AllViaServer, playerMakingMove.Player.ActorNumber, cardToPlay.CardRandom, cardToPlay.Color, cardToPlay.WildColor, cardToPlay.Value, updateGuid);
        PhotonNetwork.SendAllOutgoingCommands(); // Send message immediately to avoid lag
    }

    #region PUN RPC Calls
    [PunRPC]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    async void SendMoveToAllPlayers(int playerActorNumber, int cardRandom, Card.CardColor cardColor, Card.CardColor cardWildColor, Card.CardValue cardValue, string updateGuid)
    {
        try
        {

            CustomLogger.Log("SendMoveToAllPlayers: Enter");
            CustomLogger.Log($"SendMoveToAllPlayers: Room properties updated at {DateTime.Now}");
            CustomLogger.Log($"SendMoveToAllPlayers: Room properties update guid {updateGuid}");
            CustomLogger.Log($"SendMoveToAllPlayers: cardString = {cardRandom}\tcardColor = {cardColor}\tcardWildColor = {cardWildColor}\tcardValue = {cardValue}");

            var playerSending = lastPlayer = playerRotation.Find(player => player.Player.ActorNumber == playerActorNumber);

            if (playerSending != null)
            {
                CustomLogger.Log($"SendMoveToAllPlayers: Found player {playerSending.Name}");
                CustomLogger.Log($"SendMoveToAllPlayers: cardRandom = {cardRandom}");
                CustomLogger.Log($"SendMoveToAllPlayers: dealDeck = {dealDeck.PeekTopCard().CardRandom}");
                CustomLogger.Log($"SendMoveToAllPlayers: dealDeck Card = {dealDeck.PeekTopCard()}");

                var cardToPlay = playerSending.Hand.Find(card => card.CardRandom == cardRandom);

                if (cardToPlay == null)
                {
                    CustomLogger.Log($"SendMoveToAllPlayers: Card was NOT found in player's hand");
                    // If the remote player says they have a card we need to see if it's in the deal deck and give it to them.
                    if (dealDeck.PeekTopCard().CardRandom == cardRandom)
                    {
                        CustomLogger.Log($"SendMoveToAllPlayers: We were able to peek the card");
                        cardToPlay = TakeFromDealPile();
                        CustomLogger.Log($"SendMoveToAllPlayers: Card was NOT found in player's hand but was found in the deal deck. Giving {cardToPlay} with Id {cardToPlay.CardRandom} to {playerSending.Name}");


                        // If the card can be played do not add it to the player's hand since we don't want to reset the Uno flag
                        if (!cardToPlay.CanPlay(discardDeck.PeekTopCard()))
                        {
                            CustomLogger.Log("SendMoveToAllPlayers: Added card to hand");
                            await playerSending.AnimateCardToPlayer(cardToPlay);
                            playerSending.AddCard(cardToPlay);
                            cardToPlay = Card.Empty;
                        }
                        else
                        {
                            CustomLogger.Log("SendMoveToAllPlayers: Did not add card to hand");
                        }
                    }
                }
                else
                {
                    CustomLogger.Log($"SendMoveToAllPlayers: Card was found in player's hand");
                    CustomLogger.Log($"SendMoveToAllPlayers: Playing card {cardToPlay} with Id {cardToPlay.CardRandom}");
                    cardToPlay = playerSending.PlayCard(cardToPlay, discardDeck.PeekTopCard(), false);
                }

                if (cardToPlay.Color == Card.CardColor.Wild)
                {
                    // When we're here we need to make sure we honor the player's wild color choice
                    CustomLogger.Log($"SendMoveToAllPlayers: Set {cardToPlay} to wild color {cardWildColor}");
                    cardToPlay.SetWildColor(cardWildColor);
                }

                GameLoop(cardToPlay, playerSending);

                // If the player has taken the last card the the discard deck is swapped
                // we will need to check and pull a new card.
                if (dealDeck.Count == 0)
                {
                    CustomLogger.Log($"SendMoveToAllPlayers: Discard deck is empty.");
                    discardDeck.AddCardToDeck(TakeFromDealPile(), true);
                }
            }
            CustomLogger.Log("SendMoveToAllPlayers: exit");
        }
        catch (Exception ex)
        {
            CustomLogger.Log($"SendMoveToAllPlayers: {ex.StackTrace}");
            CustomLogger.Log($"SendMoveToAllPlayers: {ex.Message}");
            throw;
        }
        finally
        {
        }
        CustomLogger.Log("SendMoveToAllPlayers: exit");
    }

    [PunRPC]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    void RestartGame()
    {
        try
        {
            Destroy(winnerBannerPrefabToDestroy);
            UpdateLog(string.Empty);
            ResetDecks();
            StartGame();
            CustomLogger.Log("Starting a new game!");
        }
        catch (Exception ex)
        {
            CustomLogger.Log(ex);
            throw;
        }
        finally
        {
        }

    }

    [PunRPC]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    void CallUno(Player playerToCallUno)
    {
        try
        {
            var unoCaller = playerRotation.FindPlayerByNetworkPlayer(playerToCallUno);
            if (unoCaller.CanCallUno(discardDeck.PeekTopCard()))
            {
                // If we need to do something here we can
            }
        }
        catch (Exception ex)
        {
            CustomLogger.Log(ex);
            throw;
        }
        finally
        {
        }

    }

    [PunRPC]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    void ChallengePlay(Player challengePlayer)
    {
        try
        {
            var localChallengePlayer = playerRotation.FindPlayerByNetworkPlayer(challengePlayer);
            var playerToGetTwoCards = lastPlayer;

            if (playerToGetTwoCards.CanBeChallengedForUno())
            {
                CustomLogger.Log($"{playerToGetTwoCards.Name} CAN be challenged");
                CustomLogger.Log($"CalledUno = {playerToGetTwoCards.CalledUno}");
                CustomLogger.Log($"HasBeenChallenged = {playerToGetTwoCards.HasBeenChallenged}");

                string message = $"{localChallengePlayer.Name} challenged {playerToGetTwoCards.Name} and won! {playerToGetTwoCards.Name} draws two cards!";

                // If the player remembered to click the uno button on the next play the the challenger gets the cards
                if (playerToGetTwoCards.CalledUno)
                {
                    CustomLogger.Log($"{playerToGetTwoCards.Name} Has called Uno");
                    message = $"{localChallengePlayer.Name} challenged {playerToGetTwoCards.Name} and lost! {localChallengePlayer.Name} draws two cards!";
                    playerToGetTwoCards = localChallengePlayer;

                    CustomLogger.Log($"{playerToGetTwoCards.Name} is now getting the cards");
                    CustomLogger.Log($"CalledUno = {playerToGetTwoCards.CalledUno}");
                    CustomLogger.Log($"HasBeenChallenged = {playerToGetTwoCards.HasBeenChallenged}");
                }
                else
                {
                    CustomLogger.Log($"{playerToGetTwoCards.Name} Has Not called Uno");
                    CustomLogger.Log($"CalledUno = {playerToGetTwoCards.CalledUno}");
                    CustomLogger.Log($"HasBeenChallenged = {playerToGetTwoCards.HasBeenChallenged}");
                }

                // Someone is getting two cards...
                for (int i = 0; i < 2; i++)
                {
                    playerToGetTwoCards.AddCard(TakeFromDealPile());
                }
                UpdateLog(message);
            }
            else
            {
                CustomLogger.Log($"{playerToGetTwoCards.Name} could not be challenged");
                CustomLogger.Log($"CalledUno = {playerToGetTwoCards.CalledUno}");
                CustomLogger.Log($"HasBeenChallenged = {playerToGetTwoCards.HasBeenChallenged}");
            }

        }
        catch (Exception ex)
        {
            CustomLogger.Log(ex);
            throw;
        }
        finally
        {
        }

    }
    #endregion


}
