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
    private void TogglePlayableDimming(bool playSound = true)
    {
        if (playerRotation.Current().Player == LocalPlayerReference.Player)
        {
            if (playSound)
            {
                audioSource.clip = playerTurn;
                audioSource.Play();
            }

            LocalPlayerReference.ItsYourTurn(true);
            LocalPlayerReference.DimCardsThatCantBePlayed(playerToggle.isOn, discardDeck.PeekTopCard());
        }
        else
        {
            LocalPlayerReference.ItsYourTurn(false);
        }
    }

    private GameAction ConvertCardToAction(Card.CardValue cardValue)
    {
        switch (cardValue)
        {
            case Card.CardValue.Wild:
                return GameAction.Wild;
            case Card.CardValue.DrawTwo:
                return GameAction.DrawTwo;
            case Card.CardValue.Skip:
                return GameAction.Skip;
            case Card.CardValue.Reverse:
                return GameAction.Reverse;
            case Card.CardValue.DrawFour:
                return GameAction.DrawFour;
            case Card.CardValue.DrawAndGoAgainOnce:
                return GameAction.DrawAndPlayOnce;
            case Card.CardValue.DrawAndSkipTurn:
                return GameAction.DrawAndSkip;
            default:
                return GameAction.NextPlayer;
        }
    }

    public bool PlayerCanMakeMove()
    {
        return CurrentPlayer.Player == LocalPlayerReference.Player;
    }

    private void UpdateLog(string textToUpdate, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
    {
        playerLog.text = textToUpdate;
        CustomLogger.Log(textToUpdate, memberName);
    }

    /// <summary>
    /// Place the player around the unit cirlce at the specified section
    /// </summary>
    private static T PlaceInCircle<T>(Transform centerOfScreen, GameObject prefab, int itemNumber, int totalObjects, float minorAxis, float majorAxis) where T : MonoBehaviour
    {
        DeterminePlayerPosition(itemNumber, totalObjects, minorAxis, majorAxis, out float angle, out float x, out float y);
        GameObject playerInstantiate = Instantiate(prefab, new Vector3(x, y), Quaternion.LookRotation(Vector3.zero, Vector3.up), centerOfScreen);
        var player = playerInstantiate.GetComponent<T>();
        player.transform.eulerAngles = Vector3.forward * Mathf.Rad2Deg * angle;
        return player;
    }

    private static void DeterminePlayerPosition(int itemNumber, int totalObjects, float minorAxis, float majorAxis, out float angle, out float x, out float y)
    {
        // The local player will always be at 3pi/2 (270 degrees) on the circle
        // This is the bottom of the player screen. All others will be situated around the circle

        float degrees270 = (Mathf.PI * 3) / 2;
        angle = ((itemNumber - 1) * Mathf.PI * 2) / totalObjects;
        float angleOncircle = degrees270 + angle;
        x = majorAxis * Mathf.Cos(angleOncircle);
        y = minorAxis * Mathf.Sin(angleOncircle);
        CustomLogger.Log($"Player (x,y) coordinate = ({x},{y})");
        CustomLogger.Log($"Player angle on circle = {angleOncircle * Mathf.Rad2Deg}");
    }

    private void AdvanceNextPlayer()
    {
        // Un dim the computer player so it gives us an indication that they are playing
        var nextPlayer = playerRotation.Next();
        foreach (var item in playerRotation)
        {
            if (item != playerRotation.Current())
            {
                item.DimCards(true);
            }
            else
            {
                item.DimCards(false);
            }
        }

        // Always honor the card dimming when a player 
        TogglePlayableDimming();

        if (nextPlayer is NetworkPlayer)
        {
            // This ends the loop for this play and will wait for the network player to make a move
        }
    }

    private void ShowWin(LocalPlayerBase<Player> player)
    {

        winnerBannerPrefabToDestroy = Instantiate(winnerBannerPrefab, transform);
        winnerBannerPrefabToDestroy.name = Constants.WinnerPrefabName;

        // Tally up the winner's score
        int score = 0;
        foreach (var item in playerRotation)
        {
            if (!playerScore.ContainsKey(item))
            {
                playerScore[item] = 0;
            }

            if (item != player)
            {
                score += item.ScoreHand();
            }
        }

        playerScore[player] += score;


        // Get all of the buttons in the prefab
        var allButtons = winnerBannerPrefabToDestroy.GetComponentsInChildren<Button>(true);

        Button exitApplication = null;
        Button playerReady = null;
        Button leaveGame = null;


        foreach (var item in allButtons)
        {
            switch (item.name)
            {
                case "Exit":
                    exitApplication = item;
                    break;
                case "PlayAgain":
                    playerReady = item;
                    break;
                case "LeaveGame":
                    leaveGame = item;
                    break;
                default:
                    break;
            }
        }

        // Add callbacks to these buttons
        playerReady.onClick.AddListener(() =>
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [Constants.PlayerReady] = true });
            playerReady.interactable = false;

        });

        exitApplication.onClick.AddListener(() =>
        {
            Application.Quit();
        });

        leaveGame.onClick.AddListener(() =>
        {
            PhotonNetwork.LeaveRoom();
            SceneManager.LoadScene("CreateGame", LoadSceneMode.Single);
        });

        // Generate WinnerBanner
        var allTextMesh = winnerBannerPrefabToDestroy.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI winnerBanner = null;

        foreach (var item in allTextMesh)
        {
            switch (item.name)
            {
                case "WinnerBanner":
                    winnerBanner = item;
                    break;
                default:
                    break;
            }
        }

        winnerBanner.text = $"{player.name} WINS!";

        // Generate score card
        GenerateScoreCard();

        audioSource.clip = playerWin;
        audioSource.Play();

        stopGame = true;
    }

    private void GenerateScoreCard()
    {
        // Create the scorecard list
        var orderedScore = from scores in playerScore orderby scores.Value descending select new { Name = scores.Key.Player.NickName, Score = scores.Value, PointsGiven = scores.Key.ScoreHand(), Ready = scores.Key.Player.CustomProperties.ContainsKey(Constants.PlayerReady) };

        // Get all text in the banner prefab
        var scoreCardTextMesh = winnerBannerPrefabToDestroy.GetComponentsInChildren<TextMeshProUGUI>(true);

        TextMeshProUGUI playerNameScoreCard = null;
        TextMeshProUGUI dotsScoreCard = null;
        TextMeshProUGUI playerScoreScoreCard = null;
        TextMeshProUGUI pointsGivenScoreCard = null;

        foreach (var item in scoreCardTextMesh)
        {
            switch (item.name)
            {
                case "PlayerNames":
                    playerNameScoreCard = item;
                    break;
                case "DOTS":
                    dotsScoreCard = item;
                    break;
                case "PlayerScores":
                    playerScoreScoreCard = item;
                    break;
                case "PointsGiven":
                    pointsGivenScoreCard = item;
                    break;
                default:
                    break;
            }
        }

        // We will create text buffers and append the strings. No need to optimize since we're typically dealing with less than 10 strings.
        var playerNameBuffer = string.Empty;
        var playerScoreBuffer = string.Empty;
        var dotsBuffer = string.Empty;
        var pointsGivenBuffer = string.Empty;

        foreach (var item in orderedScore)
        {
            // We will want to display a "ready" prompt next to the score so we can indicate who's left
            string playerReady = string.Empty;
            if (item.Ready)
            {
                playerReady = " (ready)";
            }
            playerNameBuffer += $"{item.Name}\n";
            dotsBuffer += ".........\n";
            playerScoreBuffer += $"{item.Score}\n";
            pointsGivenBuffer += item.PointsGiven == 0 ? $"{playerReady}\n" : $"(+{item.PointsGiven}){playerReady}\n";
        }

        playerNameScoreCard.text = playerNameBuffer;
        dotsScoreCard.text = dotsBuffer;
        playerScoreScoreCard.text = playerScoreBuffer;
        pointsGivenScoreCard.text = pointsGivenBuffer;
    }

    private static bool CheckAllPlayersAreReady()
    {
        bool allPlayersReady = true;
        if (PhotonNetwork.CurrentRoom.Players.Count == 1)
        {
            return false;
        }
        foreach (var item in PhotonNetwork.CurrentRoom.Players)
        {
            if (item.Value.CustomProperties.ContainsKey(Constants.PlayerReady))
            {
                allPlayersReady &= true;
            }
            else
            {
                allPlayersReady &= false;
            }
        }

        return allPlayersReady;
    }

    private bool CheckAllPlayersAreGameReady()
    {
        bool allPlayersReady = true;
        int playersCount = 0;
        if (PhotonNetwork.CurrentRoom.Players.Count == 1)
        {
            return false;
        }
        foreach (var item in PhotonNetwork.CurrentRoom.Players)
        {
            if (item.Value.CustomProperties.ContainsKey(Constants.PlayerGameLoaded))
            {
                allPlayersReady &= true;
                playersCount++;
            }
            else
            {
                allPlayersReady &= false;
            }
        }

        if (!allPlayersReady)
        {
            UpdateLog($"{playersCount} of {PhotonNetwork.CurrentRoom.Players.Count} are ready.");
        }

        return allPlayersReady;
    }

    private void CheckAndStartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            if (CheckAllPlayersAreReady())
            {
                CustomLogger.Log($"Calling RestartGame");
                photonView.RPC("RestartGame", RpcTarget.AllViaServer);
            }
        }
    }

}
