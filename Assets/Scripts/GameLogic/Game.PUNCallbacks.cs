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
    #region PUN Callbacks
    public override void OnPlayerLeftRoom(Player otherPlayer)
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
                var c = playerWhoLeft.PlayCard(item, item, false);
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

            base.OnPlayerLeftRoom(otherPlayer);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }
        finally
        {
        }

    }

    public override async void OnJoinedRoom()
    {
        if (!gameStarted)
        {
            await InitializeAssetsAndPlayers();
            StartGame();
        }

        base.OnJoinedRoom();
    }

    public override void OnConnected()
    {
        base.OnConnected();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarningFormat("PUN Basics Tutorial/Launcher: OnDisconnected() was called by PUN with reason {0}", cause);
    }

    public override void OnCreatedRoom()
    {
        base.OnCreatedRoom();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (stopGame)
        {
            GenerateScoreCard();
            CheckAndStartGame();
        }
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);
    }

    #endregion
}
