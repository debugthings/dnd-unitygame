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
    private void RepoisitionGamePlayers()
    {
        // For the game we need the local player to be the 6 o'clock position on the table
        // Or, more accurately at 3pi/2 radians
        // We also need to make sure there is some repeatable way to get players in order
        int myNumber = 0;
        var playersInRoom = playerRotation.OrderBy(player => player.Player.ActorNumber);
        var maxPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        foreach (var player in playersInRoom)
        {
            if (player.Player == PhotonNetwork.LocalPlayer)
            {
                break;
            }
            myNumber++;
        }

        // Determine the eccentricity of the screen
        Camera cam = Camera.main;
        float minorAxis = cam.orthographicSize;
        float majorAxis = minorAxis * cam.aspect;

        int playerCounter = 1;
        foreach (var player in playersInRoom)
        {
            // Determin the position of the player in the circle
            int baseNumber = (maxPlayers - myNumber + playerCounter++);
            int position = baseNumber - maxPlayers <= 0 ? baseNumber : baseNumber - maxPlayers;



            CustomLogger.Log($"Creating player {player.Player.NickName} with actor number {player.Player.ActorNumber}");

            var photonPlayer = playerRotation.FindPlayerByNetworkPlayer(player.Player);
            DeterminePlayerPosition(position, numOfPlayers, minorAxis, majorAxis, out float angle, out float x, out float y);
            player.transform.position = new Vector3(x, y);
            player.transform.eulerAngles = Vector3.forward * Mathf.Rad2Deg * angle;
        }
    }

    private void FixupCardsPerColor()
    {
        // We should only add custom cards when it's time...
        cardValuesArray.Remove(Card.CardValue.FirstPlay);
        // We should only add custom cards when it's time...
        cardValuesArray.Remove(Card.CardValue.Custom);
        // There is only 1 zero per color...
        cardValuesArray.Remove(Card.CardValue.Zero);
        // Removing the color coded wild cards to match a deck
        cardValuesArray.Remove(Card.CardValue.DrawFour);
        cardValuesArray.Remove(Card.CardValue.Wild);
        // Remove the special card values
        cardValuesArray.Remove(Card.CardValue.Empty);
        cardValuesArray.Remove(Card.CardValue.DrawAndGoAgainOnce);
        cardValuesArray.Remove(Card.CardValue.DrawAndSkipTurn);
    }

    private void ResetDecks()
    {
        dealDeck.ClearDeck();
        discardDeck.ClearDeck();
        foreach (var item in playerRotation)
        {
            item.ClearHand();
        }
    }

    private void CreateCardOnDealDeck(int randomValue, Card.CardColor cardColor, Card.CardValue cardValue)
    {
        GameObject instantiatedCardObject = Instantiate(cardPrefab, dealDeck.transform);
        var instantiatedCard = instantiatedCardObject.GetComponent<Card>();
        instantiatedCard.SetProps(randomValue, cardValue, cardColor);
        instantiatedCard.name = instantiatedCard.ToString();
        // Log($"Built {instantiatedCard.name}");
        CustomLogger.Log($"Rand: {randomValue} Card: {instantiatedCard}");
        dealDeck.AddCardToDeck(instantiatedCard, false);
    }

    private async Task InitializeAssetsAndPlayers()
    {
        CustomLogger.Log("Entered Start()");
        if (PhotonNetwork.CurrentRoom.CustomProperties != null)
        {
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Constants.SeedKeyName, out var value);
            CustomLogger.Log($"Seed value {value}");
            uint roomSeed = Convert.ToUInt32(value);
            rand.InitState(roomSeed);
            dealDeck.SetRandomSeed(roomSeed);
            dealDeck.StackGrowsDown = false;
            discardDeck.SetRandomSeed(roomSeed);
            discardDeck.StackGrowsDown = true;
        }
        else
        {
            rand.InitState();
        }

        gameOptions = gameOptions ?? new GameOptions();

        LoadAllSounds();
        await LoadAllPrefabs();

        if (gameOptions.HumanPlayers < 1)
        {
            throw new ArgumentOutOfRangeException("You must have 1 or more human players for this game!");
        }

        numOfPlayers = PhotonNetwork.CurrentRoom.PlayerCount;

        CustomLogger.Log("Build players");
        BuildGamePlayers();
    }

    private async Task LoadAllPrefabs()
    {
        CustomLogger.Log("Loading Card Prefab");
        var cardPre = cardReference.LoadAssetAsync<GameObject>();
        cardPrefab = await cardPre.Task;

        CustomLogger.Log("Loading Dimmable Card Prefab");
        var dimmablePrefabOperation = dimmableCardReference.LoadAssetAsync<GameObject>();
        dimmableCardPrefab = await dimmablePrefabOperation.Task;

        CustomLogger.Log("Loading Player Prefab");
        var playerPrefabOperation = playerReference.LoadAssetAsync<GameObject>();
        playerPrefab = await playerPrefabOperation.Task;

        CustomLogger.Log("Loading Copmuter Player Prefab");
        var computerPlayerPrefabOperation = computerPlayerReference.LoadAssetAsync<GameObject>();
        computerPlayerPrefab = await computerPlayerPrefabOperation.Task;

        CustomLogger.Log("Loading Copmuter Player Prefab");
        var winnerBannerOperation = winnerBanner.LoadAssetAsync<GameObject>();
        winnerBannerPrefab = await winnerBannerOperation.Task;

        CustomLogger.Log("Loading Wild Card Select Prefab");
        var wildCardOperation = wildCardSelect.LoadAssetAsync<GameObject>();
        wildCardSelectPrefab = await wildCardOperation.Task;

    }

    private void LoadAllSounds()
    {
        CustomLogger.Log("Loading Card Play Sounds");

        for (int i = 1; i <= 5; i++)
        {
            AsyncOperationHandle<AudioClip[]> spriteHandle = Addressables.LoadAssetAsync<AudioClip[]>($"play_card_{i}");
            spriteHandle.Completed += LoadSoundToDictionary;
        }


    }

    private void LoadSoundToDictionary(AsyncOperationHandle<AudioClip[]> obj)
    {
        if (obj.Status == AsyncOperationStatus.Succeeded)
        {
            cardPlaySounds.Add(obj.Result.First());
        }
    }

    private void BuildGamePlayers()
    {
        // For the game we need the local player to be the 6 o'clock position on the table
        // Or, more accurately at 3pi/2 radians
        // We also need to make sure there is some repeatable way to get players in order
        int myNumber = 0;
        var playersInRoom = PhotonNetwork.CurrentRoom.Players.OrderBy(player => player.Value.ActorNumber);
        var maxPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        foreach (var player in playersInRoom)
        {
            if (player.Value == PhotonNetwork.LocalPlayer)
            {
                break;
            }
            myNumber++;
        }

        int playerCounter = 1;

        // Determine the eccentricity of the screen
        Camera cam = Camera.main;
        float minorAxis = cam.orthographicSize;
        float majorAxis = minorAxis * cam.aspect;

        foreach (var player in playersInRoom)
        {
            // Determin the position of the player in the circle
            int baseNumber = (maxPlayers - myNumber + playerCounter++);
            int position = baseNumber - maxPlayers <= 0 ? baseNumber : baseNumber - maxPlayers;

            // Scale down when over a specific size
            var scale = numOfPlayers >= 6 ? 0.75 : 1;
            var maxNumberOfCards = numOfPlayers >= 6 ? 8 : 10;

            CustomLogger.Log($"Creating player {player.Value.NickName} with actor number {player.Value.ActorNumber}");

            var photonPlayer = player.Value;
            if (player.Value != PhotonNetwork.LocalPlayer)
            {
                var networkPlayer = PlaceInCircle<NetworkPlayer>(transform, computerPlayerPrefab, position, numOfPlayers, minorAxis, majorAxis);
                networkPlayer.Player = player.Value;
                networkPlayer.CurrentGame = this;
                networkPlayer.SetName(player.Value.NickName, "0");
                networkPlayer.DimmableCardObject = dimmableCardPrefab;
                networkPlayer.MaxNumberOfCardsInRow = maxNumberOfCards;
                networkPlayer.transform.localScale = 0.5f * Vector3.one;
                networkPlayer.SetNetworkPlayerObject(photonPlayer);
                playerRotation.Add(networkPlayer);
            }
            else
            {
                // The local player is always at the 1 position.
                var localPlayer = PlaceInCircle<LocalPlayer>(transform, playerPrefab, position, numOfPlayers, minorAxis, majorAxis);
                localPlayer.Player = player.Value;
                localPlayer.CurrentGame = this;
                localPlayer.SetName(player.Value.NickName, string.Empty);
                LocalPlayerReference = localPlayer;
                localPlayer.SetNetworkPlayerObject(photonPlayer);

                localPlayer.HandChangedEvent += new EventHandler<Card>(delegate (object o, Card c)
                {
                    if (localPlayer.Hand.Count > 1)
                    {
                        Color whiteColor = Color.white;
                        localPlayer.ChangeUnoButtonColor(whiteColor);
                    }
                });

                playerToggle = localPlayer.dimmableCardToggle;
                playerToggle.onValueChanged.AddListener(delegate
                {
                    TogglePlayableDimming(false);
                });

                challengeButton = localPlayer.challengeButton;

                challengeButton.onClick.AddListener(() =>
                {
                    photonView.RPC("ChallengePlay", RpcTarget.AllViaServer, LocalPlayerReference.Player);
                    PhotonNetwork.SendAllOutgoingCommands();
                });

                unoButton = localPlayer.unoButton;
                unoButton.onClick.AddListener(() =>
                {
                    photonView.RPC("CallUno", RpcTarget.AllViaServer, localPlayer.Player);
                    PhotonNetwork.SendAllOutgoingCommands();
                });

                playerRotation.Add(localPlayer);
            }
        }
    }
}
