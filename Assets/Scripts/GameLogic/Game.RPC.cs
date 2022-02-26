using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts;
using Assets.Scripts.Common;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class Game : MonoBehaviourPunCallbacks, IConnectionCallbacks
{
    private HashSet<string> rpcCalls = new HashSet<string>();

    private async void SendMoveToRPC(Card cardToPlay, LocalPlayerBase<Player> playerMakingMove)
    {
        var updateGuid = Guid.NewGuid().ToString();
        CustomLogger.Log($"Calling SendMoveToAllPlayers with {updateGuid}");
        await Task.Delay(100); // Adding a simple delay to help throttle the messages coming in
        photonView.RPC("SendMoveToAllPlayers", RpcTarget.AllBufferedViaServer, playerMakingMove.Player.ActorNumber, cardToPlay.CardRandom, cardToPlay.Color, cardToPlay.WildColor, cardToPlay.Value, updateGuid);
        PhotonNetwork.SendAllOutgoingCommands(); // Send message immediately to avoid lag
    }

    #region PUN RPC Calls

    [PunRPC]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    void SendMoveToAllPlayers(int playerActorNumber, int cardRandom, Card.CardColor cardColor, Card.CardColor cardWildColor, Card.CardValue cardValue, string updateGuid)
    {
        if (rpcCalls.Contains(updateGuid)) return;
        rpcCalls.Add(updateGuid);
        // So we don't get a number of these things happening while we're in a loop we need to 
        // stop the message pump to be sure we don't invalidate state.
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
                var dealDeckCard = Card.Empty;
                try
                {
                    dealDeckCard = dealDeck.PeekTopCard();
                }
                catch (Exception)
                {
                }

                CustomLogger.Log($"Player {playerSending.Name}");
                CustomLogger.Log($"Card Random = {cardRandom}");
                CustomLogger.Log($"Deal Deck Card Random = {dealDeckCard?.CardRandom.ToString() ?? "null"}");
                CustomLogger.Log($"Deal Deck Card = {dealDeckCard?.ToString() ?? "null"}");

                // First, is the card one the player already has in their hand?
                var cardToPlay = playerSending.Hand.Find(card => card.CardRandom == cardRandom);

                // If it's not, lets try to pull that card for the player.
                // Otherwise log that it's there and continue with the play.
                if (cardToPlay == null)
                {
                    CustomLogger.Log($"Card was NOT found in player's hand");

                    // If the player says they have a card we need to see if it's in the deal deck and give it to them.
                    if (dealDeck.PeekTopCard().CardRandom == cardRandom)
                    {
                        cardToPlay = TakeFromDealPile();
                        CustomLogger.Log($"The card that was sent to the RPC is the one in the deck.");
                        CustomLogger.Log($"Giving {cardToPlay} with Id {cardToPlay.CardRandom} to {playerSending.Name}");
                    }
                }
                else
                {
                    // Removed the card player stuff
                    CustomLogger.Log($"Card was found in player's hand. We will attempt to play the card.");
                }

                // If the card can be played do not add it to the player's hand since we don't want to reset the Uno flag
                if (!cardToPlay.CanPlay(discardDeck.PeekTopCard()))
                {
                    CustomLogger.Log("This card cannot be played against the current discard. We will add it to the players hand.");
                    playerSending.AnimateCardToPlayer(cardToPlay);
                    playerSending.AddCard(cardToPlay);
                    cardToPlay = Card.Empty;
                }
                else
                {
                    CustomLogger.Log("This card can be played against the current discard. We will attempt to play the card.");
                    // Set the wild color first
                    if (cardToPlay.Color == Card.CardColor.Wild)
                    {
                        // When we're here we need to make sure we honor the player's wild color choice
                        CustomLogger.Log($"Set {cardToPlay} to wild color {cardWildColor}");
                        cardToPlay.SetWildColor(cardWildColor);
                    }

                }

                // Let's try to play the card.
                cardToPlay = playerSending.PlayCard(cardToPlay, discardDeck.PeekTopCard());

                // Play what ever card the PlayCard logic spits out.
                GameLoop(cardToPlay, playerSending);

                // If the player has taken the last card the the discard deck is swapped
                // we will need to check and pull a new card.
                if (dealDeck.Count == 0)
                {
                    CustomLogger.Log($"Discard deck is empty.");
                    PutCardOnDiscardPile(TakeFromDealPile(), true);
                }
            }
        }
        catch (Exception ex)
        {
            CustomLogger.Log(ex.StackTrace);
            CustomLogger.Log(ex.Message);
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
    void RestartGame(string updateGuid)
    {
        if (rpcCalls.Contains(updateGuid)) return;
        rpcCalls.Add(updateGuid);
        // So we don't get a number of these things happening while we're in a loop we need to 
        // stop the message pump to be sure we don't invalidate state.
        PhotonNetwork.IsMessageQueueRunning = false;
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
            // So we don't get a number of these things happening while we're in a loop we need to 
            // stop the message pump to be sure we don't invalidate state.
            PhotonNetwork.IsMessageQueueRunning = true;
        }
    }

    [PunRPC]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    void CallUno(Player playerToCallUno, string updateGuid)
    {
        if (rpcCalls.Contains(updateGuid)) return;
        rpcCalls.Add(updateGuid);
        // So we don't get a number of these things happening while we're in a loop we need to 
        // stop the message pump to be sure we don't invalidate state.
        PhotonNetwork.IsMessageQueueRunning = false;
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
            // So we don't get a number of these things happening while we're in a loop we need to 
            // stop the message pump to be sure we don't invalidate state.
            PhotonNetwork.IsMessageQueueRunning = true;
            CustomLogger.Log("Exit");
        }

    }

    [PunRPC]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    void ChallengePlay(Player challengePlayer, string updateGuid)
    {
        if (rpcCalls.Contains(updateGuid)) return;
        rpcCalls.Add(updateGuid);

        CustomLogger.Log("Enter");
        // So we don't get a number of these things happening while we're in a loop we need to 
        // stop the message pump to be sure we don't invalidate state.
        PhotonNetwork.IsMessageQueueRunning = false;
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
                    playerToGetTwoCards.AddCard(TakeFromDealPile(true));
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
            CustomLogger.Log("Exit");
            // So we don't get a number of these things happening while we're in a loop we need to 
            // stop the message pump to be sure we don't invalidate state.
            PhotonNetwork.IsMessageQueueRunning = true;
        }

    }

    [PunRPC]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    void LeaveGame(Player otherPlayer, string updateGuid)
    {
        try
        {
            var playerWhoLeft = playerRotation.FindPlayerByNetworkPlayer(otherPlayer);
            var tempList = new List<Card>();
            for (int i = 0; i < playerWhoLeft.Hand.Count; i++)
            {
                tempList.Add(playerWhoLeft.Hand[i]);
            }

            foreach (var item in tempList)
            {
                var c = playerWhoLeft.PlayCard(item, item);
                dealDeck.PutCardBackInDeckInRandomPoisiton(c, 0, Math.Max(0, dealDeck.Count - 1));
            }

            playerRotation.Remove(playerWhoLeft);
            playerWhoLeft.PlayerLeftGame();
            CustomLogger.Log($"Player {otherPlayer.NickName} has left the game");

            if (!stopGame)
            {
                // If there is only one person left in the game, they win
                if (playerRotation.Count == 1)
                {
                    var player = playerRotation.FirstOrDefault();
                    ShowWin(player);
                }
                else
                {
                    AdvanceNextPlayer();
                }
            }

        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
        finally
        {
        }

    }


    #endregion


}
