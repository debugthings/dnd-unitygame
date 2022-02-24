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
    void SendMoveToAllPlayers(int playerActorNumber, int cardRandom, Card.CardColor cardColor, Card.CardColor cardWildColor, Card.CardValue cardValue, string updateGuid)
    {
        PhotonNetwork.IsMessageQueueRunning = false;
        try
        {
            CustomLogger.Log("Enter");
            CustomLogger.Log($"Room properties updated at {DateTime.Now}");
            CustomLogger.Log($"Room properties update guid {updateGuid}");
            CustomLogger.Log($"cardString = {cardRandom}\tcardColor = {cardColor}\tcardWildColor = {cardWildColor}\tcardValue = {cardValue}");

            var playerSending = lastPlayer = playerRotation.Find(player => player.Player.ActorNumber == playerActorNumber);

            if (playerSending != null)
            {
                // Log safely
                try
                {
                    CustomLogger.Log($"Found player {playerSending.Name}");
                    CustomLogger.Log($"cardRandom = {cardRandom}");
                    CustomLogger.Log($"dealDeck = {dealDeck.PeekTopCard().CardRandom}");
                    CustomLogger.Log($"dealDeck Card = {dealDeck.PeekTopCard()}");
                }
                catch (Exception)
                {
                }

                var cardToPlay = playerSending.Hand.Find(card => card.CardRandom == cardRandom);

                if (cardToPlay == null)
                {
                    CustomLogger.Log($"Card was NOT found in player's hand");

                    // If the remote player says they have a card we need to see if it's in the deal deck and give it to them.
                    if (dealDeck.PeekTopCard().CardRandom == cardRandom)
                    {
                        CustomLogger.Log($"We were able to peek the card");
                        cardToPlay = TakeFromDealPile();
                        CustomLogger.Log($"Card was NOT found in player's hand but was found in the deal deck. Giving {cardToPlay} with Id {cardToPlay.CardRandom} to {playerSending.Name}");

                        // If the card can be played do not add it to the player's hand since we don't want to reset the Uno flag
                        if (!cardToPlay.CanPlay(discardDeck.PeekTopCard()))
                        {
                            CustomLogger.Log("Added card to hand");
                            playerSending.AnimateCardToPlayer(cardToPlay);
                            playerSending.AddCard(cardToPlay);
                            cardToPlay = Card.Empty;
                        }
                        else
                        {
                            CustomLogger.Log("Did not add card to hand");
                        }
                    }
                }
                else
                {
                    CustomLogger.Log($"Card was found in player's hand");
                    CustomLogger.Log($"Playing card {cardToPlay} with Id {cardToPlay.CardRandom}");
                    cardToPlay = playerSending.PlayCard(cardToPlay, discardDeck.PeekTopCard(), false);
                }

                if (cardToPlay.Color == Card.CardColor.Wild)
                {
                    // When we're here we need to make sure we honor the player's wild color choice
                    CustomLogger.Log($"Set {cardToPlay} to wild color {cardWildColor}");
                    cardToPlay.SetWildColor(cardWildColor);
                }

                GameLoop(cardToPlay, playerSending);

                // If the player has taken the last card the the discard deck is swapped
                // we will need to check and pull a new card.
                if (dealDeck.Count == 0)
                {
                    CustomLogger.Log($"Discard deck is empty.");
                    discardDeck.AddCardToDeck(TakeFromDealPile(), true);
                }
            }
        }
        catch (Exception ex)
        {
            CustomLogger.Log($"{ex.StackTrace}");
            CustomLogger.Log($"{ex.Message}");
            throw;
        }
        finally
        {
            PhotonNetwork.IsMessageQueueRunning = true;
        }
        CustomLogger.Log("Exit");
        CustomLogger.Log("", "");
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
        CustomLogger.Log("Enter");
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
            CustomLogger.Log("Enter");
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
