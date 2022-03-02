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
    private Room cachedRoom;
    #region PUN Callbacks
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.CurrentRoom.Players.ContainsValue(otherPlayer))
        {
            RemovePlayer(otherPlayer);
        }
        else
        {
            PlayerDisconnected(otherPlayer);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (newPlayer.HasRejoined)
        {
            PlayerRejoined(newPlayer);
        }
        base.OnPlayerEnteredRoom(newPlayer);
    }

    public override async void OnJoinedRoom()
    {
        cachedRoom = PhotonNetwork.CurrentRoom;
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
        CustomLogger.Log($"Client Disconnected: {cause}");

        switch (cause)
        {
            case DisconnectCause.Exception:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.DisconnectByServerLogic:
            case DisconnectCause.DisconnectByServerReasonUnknown:
                // These reasons were found in the PUN documentation
                Recover();
                break;

            case DisconnectCause.None:
                break;
            case DisconnectCause.AuthenticationTicketExpired:
                break;
            case DisconnectCause.CustomAuthenticationFailed:
                break;
            case DisconnectCause.DisconnectByClientLogic:
                break;
            case DisconnectCause.DisconnectByDisconnectMessage:
                break;
            case DisconnectCause.DisconnectByOperationLimit:
                break;
            case DisconnectCause.DnsExceptionOnConnect:
                break;
            case DisconnectCause.ExceptionOnConnect:
                break;
            case DisconnectCause.InvalidAuthentication:
                break;
            case DisconnectCause.InvalidRegion:
                break;
            case DisconnectCause.MaxCcuReached:
                break;
            case DisconnectCause.OperationNotAllowedInCurrentState:
                break;
            case DisconnectCause.ServerAddressInvalid:
                break;
            default:
                break;
        }
    }

    private void Recover()
    {
        if (!PhotonNetwork.ReconnectAndRejoin())
        {
            CustomLogger.Log("ReconnectAndRejoin failed, trying Reconnect");
            if (!PhotonNetwork.Reconnect())
            {
                return;
            }
            PhotonNetwork.RejoinRoom(cachedRoom.Name);
            CustomLogger.Log("Unable to reconnect");
        }
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

    void RemovePlayer(Player otherPlayer)
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
            CustomLogger.Log(ex.ToString());
        }
        finally
        {
        }
    }


    void PlayerDisconnected(Player otherPlayer)
    {
        try
        {
            var playerWhoLeft = playerRotation.FindPlayerByNetworkPlayer(otherPlayer);
            playerWhoLeft.PlayerDisconnected();
            CustomLogger.Log($"Player {otherPlayer.NickName} is disconnected.");
        }
        catch (Exception ex)
        {
            CustomLogger.Log(ex.ToString());
        }
        finally
        {
        }
    }

    void PlayerRejoined(Player otherPlayer)
    {
        try
        {
            var playerWhoLeft = playerRotation.FindPlayerByNetworkPlayer(otherPlayer);
            playerWhoLeft.FixupCardPositions();
            CustomLogger.Log($"Player {otherPlayer.NickName} has joined.");
        }
        catch (Exception ex)
        {
            CustomLogger.Log(ex.ToString());
        }
        finally
        {
        }
    }

    #endregion
}

