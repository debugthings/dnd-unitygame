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

public class Game : MonoBehaviourPunCallbacks, IConnectionCallbacks
{


    private const float cardStackZOrderOffset = 0.01f;

    private CircularList<LocalPlayerBase<Player>, Player> players = new CircularList<LocalPlayerBase<Player>, Player>();

    private List<Card.CardValue> cardValuesArray = new List<Card.CardValue>((Card.CardValue[])Enum.GetValues(typeof(Card.CardValue)));
    private Card.CardColor[] cardColorArray = new Card.CardColor[] { Card.CardColor.Red, Card.CardColor.Green, Card.CardColor.Blue, Card.CardColor.Yellow };

    private List<AudioClip> cardPlaySounds = new List<AudioClip>();

    private GameOptions gameOptions;

    private Toggle playerToggle;
    private Button unoButton;
    private Button challengeButton;
    private bool playerChallenge = false;
    private Queue<LocalPlayerBase<Player>> playerUno = new Queue<LocalPlayerBase<Player>>();


    private int numOfPlayers = 4;
    private int numberOfDecks = 1;

    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();
    private IDictionary<LocalPlayerBase<Player>, int> playerScore = new Dictionary<LocalPlayerBase<Player>, int>();

    // Used for our animations to and from the deck.
    private ConcurrentDictionary<UnityEngine.Object, Tuple<LocalPlayerBase<Player>, CardDeck>> cardTransformDictionary = new ConcurrentDictionary<UnityEngine.Object, Tuple<LocalPlayerBase<Player>, CardDeck>>();

    // All prefabs for this gameboard
    private GameObject cardPrefab;
    private GameObject dimmableCardPrefab;
    private GameObject playerPrefab;
    private GameObject computerPlayerPrefab;
    private GameObject winnerBannerPrefab;
    private GameObject wildCardSelectPrefab;
    private GameObject winnerBannerPrefabToDestroy;

    // Used to calculate screen resize events
    private Vector2 lastScreenSize;

    private LocalPlayerBase<Player> lastPlayer;

    private float maxDistance;

    // The two decks that handle cards
    public CardDeck dealDeck;
    public CardDeck discardDeck;

    // The banner that says what just happened in the game
    public TextMeshProUGUI playerLog;

    // These are objects that are created on the fly so we want to load the asset from the asset store
    public AssetReference playerReference;
    public AssetReference computerPlayerReference;
    public AssetReference cardReference;
    public AssetReference dimmableCardReference;
    public AssetReference winnerBanner;
    public AssetReference wildCardSelect;
    public AudioSource audioSource;
    public AudioClip playerWin;
    public AudioClip playerTurn;
    public AudioClip wildCardPopup;

    public LocalPlayer CurrentPlayer => (LocalPlayer)players.Current();

    /// <summary>
    /// Gets the singular local player
    /// </summary>
    public LocalPlayer LocalPlayer { get; private set; }

    private bool stopGame;

    private bool gameStarted = false;

    public float cardDealSpeed;


    // Start is called before the first frame update
    async void Start()
    {
        // TODO Initialize a please wait here.
        if (PhotonNetwork.IsConnectedAndReady)
        {
            await InitializeAssetsAndPlayers();
            StartGame();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        try
        {
            var playerWhoLeft = players.FindPlayerByNetworkPlayer(otherPlayer);
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

            players.Remove(playerWhoLeft);
            playerWhoLeft.PlayerLeftGame();
            Debug.Log($"Player {otherPlayer.NickName} has left the game");

            if (!stopGame)
            {
                // If there is only one person left in the game, they win
                if (players.Count == 1)
                {
                    var player = players.FirstOrDefault();
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

    private void UpdateLog(string textToUpdate)
    {
        playerLog.text = textToUpdate;
    }

    private void ResetDecks()
    {
        dealDeck.ClearDeck();
        discardDeck.ClearDeck();
        foreach (var item in players)
        {
            item.ClearHand();
        }
    }

    private async void StartGame()
    {
        gameStarted = true;
        stopGame = false;

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
        numberOfDecks = MathExtension.Clamp(baseNumber, gameOptions.BaseNumberOfDecks, gameOptions.MaxDecks);

        // Remove cards so we can just use the loops below
        FixupCardsPerColor();

        Debug.Log("Build Number Cards");
        // Build the deck and create a random placement
        // Shuffling idea taken from https://blog.codinghorror.com/shuffling/
        // There are two sets of numbers and action cards per color per deck
        for (int num = 0; num < (numberOfDecks * 2); num++)
        {
            for (int colorIndex = 0; colorIndex < cardColorArray.Length; colorIndex++)
            {
                for (int cardValueIndex = 0; cardValueIndex < cardValuesArray.Count; cardValueIndex++)
                {
                    CreateCardOnDealDeck(rand.NextInt(), cardColorArray[colorIndex], cardValuesArray[cardValueIndex]);
                }
            }
        }

        Debug.Log("Build Zero Cards");

        // There is one set of Zero cards per color per deck
        for (int num = 0; num < numberOfDecks; num++)
        {
            for (int colorIndex = 0; colorIndex < cardColorArray.Length; colorIndex++)
            {
                CreateCardOnDealDeck(rand.NextInt(), cardColorArray[colorIndex], Card.CardValue.Zero);
            }
        }

        Debug.Log("Build Wild Cards");

        // Generate the correct number of wild cards 4 cards per number of decks...
        for (int i = 0; i < 4 * numberOfDecks; i++)
        {
            CreateCardOnDealDeck(rand.NextInt(), Card.CardColor.Wild, Card.CardValue.Wild);
            CreateCardOnDealDeck(rand.NextInt(), Card.CardColor.Wild, Card.CardValue.DrawFour);
        }

        // Use this to generate a proper z-order of the deck
        dealDeck.MaxStackDepth = dealDeck.Count() * cardStackZOrderOffset;
        discardDeck.MaxStackDepth = dealDeck.Count() * cardStackZOrderOffset;

        Debug.Log("Build deck and add jitter");
        // Push the cards in the random order to a Stack...
        dealDeck.Shuffle();

        Debug.Log("Deal cards to players");

        // For now we'll use the master client as the dealer for the deck.
        // This will make sure that all players start with the correct cards.

        // Player next to the master player
        Player nextToDealer = default;

        // Set the player order to be forward always
        players.Forward();
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
        players.SetPlayer(players.FindPlayerByNetworkPlayer(nextToDealer));

        // Deal out the players 
        for (int i = 0; i < gameOptions.NumberOfCardsToDeal; i++)
        {
            for (int j = 0; j < numOfPlayers; j++)
            {
                // The circular list allows us to start dealing from the "first" position
                var cardToDeal = TakeFromDealPile();
                var currentPlayer = players.Current();
                AnimateCardFromDealDeckToPlayer(cardToDeal, currentPlayer);
                currentPlayer.AddCard(cardToDeal);
                players.Next();
                await BlockOnCardFlight(cardToDeal);
            }
        }

        Debug.Log("Dim computer player cards");
        foreach (var item in players)
        {
            if (item != players.Current())
            {
                item.DimCards(true);
            }
        }

        Debug.Log("Play top card");
        PutCardOnDiscardPile(TakeFromDealPile(), true, true);

        // When we're here we know htat we're not actively animating the card loads and the game should execute at about the same pace.
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable() { [Constants.PlayerGameLoaded] = true });

        while (CheckAllPlayersAreGameReady())
        {
            await Task.Delay(20);
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable() { [Constants.PlayerGameLoaded] = null });

        // There are a couple of rules on the first play
        // If it's a wild the first player chooses the color.
        // If it's a wild draw four the card goes back into the pile.
        FirstPlay(players.Current());

        TogglePlayableDimming();


        Debug.Log("Leaving Start()");
    }

    private static async Task BlockOnCardFlight(Card cardToDeal)
    {
        while (cardToDeal.IsInFlight)
        {
            await Task.Delay(20);
        }
    }

    private void AnimateCardFromDealDeckToPlayer(Card cardToDeal, LocalPlayerBase<Player> animatingPlayer, bool fromRPC = false)
    {
        cardToDeal.IsInFlight = true;
        // Add a blank card to show the deal
        if (!fromRPC && animatingPlayer is NetworkPlayer)
        {
            cardTransformDictionary.GetOrAdd(Instantiate(dimmableCardPrefab, transform), new Tuple<LocalPlayerBase<Player>, CardDeck>(animatingPlayer, dealDeck));
        }
        cardTransformDictionary.GetOrAdd(cardToDeal, new Tuple<LocalPlayerBase<Player>, CardDeck>(animatingPlayer, dealDeck));
    }

    private void AnimateCardFromPlayerToDiscardDeck(Card cardToDeal, LocalPlayerBase<Player> animatingPlayer)
    {
        cardToDeal.IsInFlight = true;
        cardToDeal.transform.SetPositionAndRotation(animatingPlayer.transform.position, animatingPlayer.transform.rotation);
        // Add a blank card to show the deal
        cardTransformDictionary.GetOrAdd(cardToDeal, new Tuple<LocalPlayerBase<Player>, CardDeck>(animatingPlayer, discardDeck));
    }

    private void AnimateCardTransforms()
    {
        // Determine the eccentricity of the screen
        Camera cam = Camera.main;
        float minorAxis = cam.orthographicSize;
        float majorAxis = minorAxis * cam.aspect;

        float maxDistance = Vector2.Distance(new Vector2(0, 0), new Vector2(minorAxis, majorAxis));

        foreach (var itemToMove in cardTransformDictionary.Keys)
        {
            if (cardTransformDictionary.ContainsKey(itemToMove))
            {
                Transform cardToMoveToTarget = null;

                // The source can either be a Card object or a dimmable prefab (aka, the back of the card)
                if (itemToMove is Card cardAsSource)
                {
                    cardToMoveToTarget = cardAsSource?.transform;
                }
                else if (itemToMove is GameObject blankCardAsSource)
                {
                    cardToMoveToTarget = blankCardAsSource?.transform;
                }

                var targetToCast = cardTransformDictionary[itemToMove];
                Transform playerTarget = targetToCast.Item1.transform;
                Transform deckTarget = targetToCast.Item2.transform;

                bool isDealDeck = deckTarget.name.Equals("DealDeck", StringComparison.InvariantCultureIgnoreCase);

                if (cardToMoveToTarget != null && playerTarget != null)
                {
                    var percentageOfMax = Mathf.Abs(Vector3.Distance(deckTarget.position, playerTarget.position)) / maxDistance;
                    var targetTransform = playerTarget.transform;

                    if (!isDealDeck)
                    {
                        targetTransform = deckTarget.transform;
                    }

                    // Move our position a step closer to the target.
                    // To make sure someone with a REAAALLY large screen doesn't have a disadvantage when someone is using a small screen
                    // In this example we'll do all of our animations based on the fact that speed/distance = the time to animate one distance
                    float step = (percentageOfMax * cardDealSpeed) * Time.fixedDeltaTime; // calculate distance to move

                    // Rotate the card two revolutions in a second
                    float stepRotate = 720.0f * Time.fixedDeltaTime; // calculate distance to move
                    cardToMoveToTarget.position = Vector3.MoveTowards(cardToMoveToTarget.position, targetTransform.transform.position, step);
                    cardToMoveToTarget.rotation = Quaternion.RotateTowards(cardToMoveToTarget.rotation, targetTransform.transform.rotation, stepRotate);

                    // Check if the position of the cube and sphere are approximately equal.
                    if (Vector3.Distance(cardToMoveToTarget.position, targetTransform.position) < 0.001f)
                    {
                        // Swap the position of the cylinder.
                        while (cardTransformDictionary.ContainsKey(itemToMove) && cardTransformDictionary.TryRemove(itemToMove, out var player))
                        {
                            if (itemToMove is Card cardToRelease)
                            {
                                cardToRelease.IsInFlight = false;
                            }
                            else if (itemToMove is GameObject cardToDestroy)
                            {
                                Destroy(cardToDestroy);
                            }

                            player.Item1.FixupCardPositions();
                        }
                    }
                }
            }
        }
    }

    private async Task InitializeAssetsAndPlayers()
    {
        Debug.Log("Entered Start()");
        if (PhotonNetwork.CurrentRoom.CustomProperties != null)
        {
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Constants.SeedKeyName, out var value);
            Debug.Log($"Seed value {value}");
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

        Debug.Log("Build players");
        BuildGamePlayers();
    }

    private void TogglePlayableDimming()
    {
        if (players.Current() == LocalPlayer)
        {
            audioSource.clip = playerTurn;
            audioSource.Play();

            LocalPlayer.ItsYourTurn(true);
            LocalPlayer.DimCardsThatCantBePlayed(playerToggle.isOn, discardDeck.PeekTopCard());
        }
        else
        {
            LocalPlayer.ItsYourTurn(false);
        }
    }

    private void CreateCardOnDealDeck(int randomValue, Card.CardColor cardColor, Card.CardValue cardValue)
    {
        GameObject instantiatedCardObject = Instantiate(cardPrefab, dealDeck.transform);
        var instantiatedCard = instantiatedCardObject.GetComponent<Card>();
        instantiatedCard.SetProps(randomValue, cardValue, cardColor);
        instantiatedCard.name = instantiatedCard.ToString();
        // Debug.Log($"Built {instantiatedCard.name}");
        dealDeck.AddCardToDeck(instantiatedCard, false);
    }

    private async Task LoadAllPrefabs()
    {
        Debug.Log("Loading Card Prefab");
        var cardPre = cardReference.LoadAssetAsync<GameObject>();
        cardPrefab = await cardPre.Task;

        Debug.Log("Loading Dimmable Card Prefab");
        var dimmablePrefabOperation = dimmableCardReference.LoadAssetAsync<GameObject>();
        dimmableCardPrefab = await dimmablePrefabOperation.Task;

        Debug.Log("Loading Player Prefab");
        var playerPrefabOperation = playerReference.LoadAssetAsync<GameObject>();
        playerPrefab = await playerPrefabOperation.Task;

        Debug.Log("Loading Copmuter Player Prefab");
        var computerPlayerPrefabOperation = computerPlayerReference.LoadAssetAsync<GameObject>();
        computerPlayerPrefab = await computerPlayerPrefabOperation.Task;

        Debug.Log("Loading Copmuter Player Prefab");
        var winnerBannerOperation = winnerBanner.LoadAssetAsync<GameObject>();
        winnerBannerPrefab = await winnerBannerOperation.Task;

        Debug.Log("Loading Wild Card Select Prefab");
        var wildCardOperation = wildCardSelect.LoadAssetAsync<GameObject>();
        wildCardSelectPrefab = await wildCardOperation.Task;

    }

    private void LoadAllSounds()
    {
        Debug.Log("Loading Card Play Sounds");

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

    private void FirstPlay(LocalPlayerBase<Player> player)
    {
        var firstCard = discardDeck.PeekTopCard();
        Debug.Log($"First card value on first play {firstCard}");
        switch (firstCard.Value)
        {
            case Card.CardValue.Skip:
            case Card.CardValue.Reverse:
            case Card.CardValue.DrawTwo:
                Debug.Log($"Reverse on first play");
                // If these actions are first we need to start the game loop since the first player will be skipped
                PerformGameAction(firstCard, true);
                GameLoop(Card.FirstPlay, player);
                break;
            case Card.CardValue.DrawFour:
                Debug.Log($"Draw four on first play");
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
                UpdateLog($"First play to {player.Name}!");
                break;
        }
    }

    public bool PlayerCanMakeMove()
    {
        return CurrentPlayer == LocalPlayer;
    }

    /// <summary>
    /// Called from the mouse handler to play the actual card. In here we'll handle what happens when a wild card is shown.
    /// </summary>
    /// <param name="cardObject"></param>
    public async void PlayClickedCard(Card cardObject)
    {
        await DelayOnPlayerChallenge();

        Debug.Log("");
        Debug.Log("Entered PlayClickedCard");
        if (PlayerCanMakeMove())
        {
            Debug.Log("");
            var cardDeck = cardObject.GetComponentInParent<CardDeck>();
            var player = cardObject.GetComponentInParent<LocalPlayer>();
            Card cardToPlay = Card.Empty;
            if (cardDeck is CardDeck && cardDeck.name == "DealDeck")
            {
                Debug.Log("Player doubleclicked the deal deck to draw a card. Taking card from deal pile");

                // If we're in play and the player decides to draw, either the card will be played or added to the hand.
                // When we're in networked mode we'll need to take into account that anyone can double click the deck so we'll need to make sure
                // the click originated from the player.
                cardToPlay = TakeFromDealPile();
                if (cardToPlay != Card.Empty)
                {
                    LocalPlayer.AddCard(cardToPlay);
                }
            }

            if (cardDeck is CardDeck && cardDeck.name == "DiscardDeck")
            {
                Debug.Log($"Player double clicked the discard deck, do nothing.");
            }
            else if (player is LocalPlayer)
            {
                Debug.Log($"Player double clicked {cardObject}");
                // If we're here we've likely tried to play a card. We need to check to see the card is okay to play.
                cardToPlay = LocalPlayer.PlayCard(cardObject, discardDeck.PeekTopCard(), false, false);
            }

            // Do the game action on the playable card.
            if (cardToPlay != Card.Empty)
            {
                if (cardToPlay.Color == Card.CardColor.Wild)
                {
                    Debug.Log($"Player double clicked a wild card, showing the wild card screen.");
                    HandleWildCard(cardToPlay);
                }
                else
                {
                    CallPUNRpc(cardToPlay, LocalPlayer);
                }
            }
        }
        Debug.Log("Left PlayClickedCard");
        Debug.Log("");
    }

    private async Task DelayOnPlayerChallenge()
    {
        while (playerChallenge)
        {
            await Task.Delay(20);
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
            CallPUNRpc(card, LocalPlayer);
            Destroy(wildCardPrefab);
        };
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

            Debug.Log($"Creating player {player.Value.NickName} with actor number {player.Value.ActorNumber}");

            var photonPlayer = player.Value;
            if (player.Value != PhotonNetwork.LocalPlayer)
            {
                var ply = PlaceInCircle<NetworkPlayer>(transform, computerPlayerPrefab, position, numOfPlayers, minorAxis, majorAxis);
                ply.Player = player.Value;
                ply.CurrentGame = this;
                ply.SetName(player.Value.NickName, "0");
                ply.DimmableCardObject = dimmableCardPrefab;
                ply.MaxNumberOfCardsInRow = maxNumberOfCards;
                ply.transform.localScale = 0.5f * Vector3.one;
                ply.SetNetworkPlayerObject(photonPlayer);
                players.Add(ply);
            }
            else
            {
                // The local player is always at the 1 position.
                var ply = PlaceInCircle<LocalPlayer>(transform, playerPrefab, position, numOfPlayers, minorAxis, majorAxis);
                ply.Player = player.Value;
                ply.CurrentGame = this;
                ply.SetName(player.Value.NickName, string.Empty);
                LocalPlayer = ply;
                ply.SetNetworkPlayerObject(photonPlayer);

                ply.HandChangedEvent += new EventHandler<Card>(delegate (object o, Card c)
                {
                    if (o is LocalPlayer playerToCheck)
                    {
                        if (playerToCheck.Hand.Count > 1)
                        {
                            Color whiteColor = Color.white;
                            ChangeUnoButtonColor(whiteColor);
                        }
                    }
                });

                playerToggle = ply.dimmableCardToggle;
                playerToggle.onValueChanged.AddListener(delegate
                {
                    TogglePlayableDimming();
                });

                challengeButton = ply.challengeButton;

                challengeButton.onClick.AddListener(() =>
                {
                    photonView.RPC("ChallengePlay", RpcTarget.AllViaServer, LocalPlayer.Player);
                });

                unoButton = ply.unoButton;
                unoButton.onClick.AddListener(() =>
                {
                    photonView.RPC("CallUno", RpcTarget.AllViaServer, ply.Player);
                });

                players.Add(ply);
            }
        }
    }

    private void ChangeUnoButtonColor(Color greenColor)
    {
        var buttonColors = unoButton.colors;
        buttonColors.normalColor = greenColor;
        unoButton.colors = buttonColors;
    }

    private void RepoisitionGamePlayers()
    {
        // For the game we need the local player to be the 6 o'clock position on the table
        // Or, more accurately at 3pi/2 radians
        // We also need to make sure there is some repeatable way to get players in order
        int myNumber = 0;
        var playersInRoom = players.OrderBy(player => player.Player.ActorNumber);
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



            Debug.Log($"Creating player {player.Player.NickName} with actor number {player.Player.ActorNumber}");

            var photonPlayer = players.FindPlayerByNetworkPlayer(player.Player);
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

    void FixedUpdate()
    {
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        if (lastScreenSize != screenSize)
        {
            lastScreenSize = screenSize;
            RepoisitionGamePlayers();
        }
        AnimateCardTransforms();
    }

    /// <summary>
    /// Place the player around the unit cirlce at the specified section
    /// </summary>
    private static T PlaceInCircle<T>(Transform centerOfScreen, GameObject prefab, int itemNumber, int totalObjects, float minorAxis, float majorAxis) where T : MonoBehaviour
    {
        float angle, x, y;
        DeterminePlayerPosition(itemNumber, totalObjects, minorAxis, majorAxis, out angle, out x, out y);
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
        Debug.Log($"Player (x,y) coordinate = ({x},{y})");
        Debug.Log($"Player angle on circle = {angleOncircle * Mathf.Rad2Deg}");
    }

    /// <summary>
    /// Pulls a card from the deal pile and will automatically swap the discard pile if needed
    /// </summary>
    /// <returns></returns>
    public Card TakeFromDealPile()
    {
        if (dealDeck.Count > 0)
        {
            return dealDeck.TakeTopCard();
        }
        if (dealDeck.Count == 0 && discardDeck.Count > 1)
        {
            Debug.Log($"Swapping decks");
            dealDeck.SwapCardsFromOtherDeck(discardDeck);
            return dealDeck.TakeTopCard();
        }
        return Card.Empty;
    }

    /// <summary>
    /// Puts the card on the discard pile and handles the flupping and translation of the card
    /// </summary>
    /// <param name="card"></param>
    /// <param name="dontCheckEquals"></param>
    /// <param name="flipCard"></param>
    /// <returns></returns>
    public GameAction PutCardOnDiscardPile(Card card, bool dontCheckEquals, bool flipCard)
    {
        var cardAction = ConvertCardToAction(card.Value);
        // Some special cases where we don't push the card to the pile since it's a 
        if (cardAction == GameAction.DrawAndPlayOnce || cardAction == GameAction.DrawAndSkip)
        {
            Debug.Log($"Card action is {cardAction}. Just return and don't add the card to the discard deck.");
            return cardAction;
        }

        // If we have a card that will play we need to continue on
        if (dontCheckEquals || card.CanPlay(discardDeck.PeekTopCard()))
        {
            Debug.Log($"Card action is {cardAction}. Add the card to the discard deck.");
            // Take the card that is in play and put it on top.
            discardDeck.AddCardToDeck(card, true);
            return cardAction;
        }
        return GameAction.NextPlayer;
    }

    [PunRPC]
    async void SendMoveToAllPlayers(Hashtable propertiesThatChanged)
    {
        try
        {
            var cardStringCheck = propertiesThatChanged?[Constants.CardToPlay] ?? "NULL";
            var cardColorCheck = propertiesThatChanged?[Constants.CardColor] ?? "NULL";
            var cardWildColorCheck = propertiesThatChanged?[Constants.CardWildColor] ?? "NULL";
            var cardValueCheck = propertiesThatChanged?[Constants.CardValue] ?? "NULL";
            var updateGuid = propertiesThatChanged?[Constants.UpdateGuid] ?? "NULL";

            Debug.Log("");
            Debug.Log($"Room properties updated at {DateTime.Now}");
            Debug.Log($"Room properties update guid {updateGuid}");
            Debug.Log($"cardString = {cardStringCheck}\tcardColor = {cardColorCheck}\tcardWildColor = {cardWildColorCheck}\tcardValue = {cardValueCheck}");

            if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(Constants.PlayerSendingMessage) && propertiesThatChanged.ContainsKey(Constants.CardToPlay))
            {
                var playerSending = players.Find(player => player.Player.ActorNumber == (int)propertiesThatChanged?[Constants.PlayerSendingMessage]);

                lastPlayer = playerSending;

                // The way the challenge works is it needs to be done before the next hand of play (move)
                // The game loop executes from this RPC and we can consider this a negation of the uno queue
                // Either someone challenged the player or they did not.
                if (playerUno.Count > 0)
                {
                    // Do nothing here but clear the queue
                    playerUno.Clear();
                }

                if (playerSending != null)
                {
                    var cardString = (int)propertiesThatChanged?[Constants.CardToPlay];
                    string cardColor = (string)propertiesThatChanged?[Constants.CardColor];
                    string cardWildColor = (string)propertiesThatChanged?[Constants.CardWildColor];
                    string cardValue = (string)propertiesThatChanged?[Constants.CardValue];
                    Debug.Log($"Found player {playerSending.Name}");
                    var cardToPlay = playerSending.Hand.Find(card => card.CardRandom == (int)propertiesThatChanged?[Constants.CardToPlay]);

                    if (cardToPlay == null)
                    {
                        Debug.Log($"Card was NOT found in player's hand");
                        // If the remote player says they have a card we need to see if it's in the deal deck and give it to them.
                        if (dealDeck.PeekTopCard().CardRandom == (int)propertiesThatChanged?[Constants.CardToPlay])
                        {
                            cardToPlay = TakeFromDealPile();
                            Debug.Log($"Card was NOT found in player's hand but was found in the deal deck. Giving {cardToPlay} with Id {cardToPlay.CardRandom} to {playerSending.Name}");
                            AnimateCardFromDealDeckToPlayer(cardToPlay, playerSending, true);
                            await BlockOnCardFlight(cardToPlay);
                            playerSending.AddCard(cardToPlay);
                        }
                    }

                    if (cardToPlay.Color == Card.CardColor.Wild)
                    {
                        // When we're here we need to make sure we honor the player's wild color choice
                        Debug.Log($"Set {cardToPlay} to wild color {cardWildColor}");
                        cardToPlay.SetWildColor(cardWildColor);
                    }

                    Debug.Log($"Playing card {cardToPlay} with Id {cardToPlay.CardRandom}");
                    cardToPlay = playerSending.PlayCard(cardToPlay, discardDeck.PeekTopCard(), false);
                    GameLoop(cardToPlay, playerSending, true);

                    // If the player has taken the last card the the discard deck is swapped
                    // we will need to check and pull a new card.
                    if (dealDeck.Count == 0)
                    {
                        Debug.Log($"Discard deck is empty.");
                        discardDeck.AddCardToDeck(TakeFromDealPile(), true);
                    }
                }
            }
            Debug.Log("");
            base.OnRoomPropertiesUpdate(propertiesThatChanged);
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
        }

    }

    [PunRPC]
    void RestartGame(Hashtable propertiesThatChanged)
    {
        try
        {
            Destroy(winnerBannerPrefabToDestroy);
            UpdateLog(string.Empty);
            ResetDecks();
            StartGame();
            Debug.Log("Starting a new game!");
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
        }

    }

    [PunRPC]
    void CallUno(Player playerToCallUno)
    {
        try
        {
            var unoCaller = players.FindPlayerByNetworkPlayer(playerToCallUno);
            if (unoCaller.CanCallUno(discardDeck.PeekTopCard()))
            {
                // If we need to do something here we can
            }
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
        }

    }

    [PunRPC]
    void ChallengePlay(Player challengePlayer)
    {
        try
        {
            playerChallenge = true;
            var localChallengePlayer = players.FindPlayerByNetworkPlayer(challengePlayer);
            var playerToGetTwoCards = lastPlayer;

            if (playerToGetTwoCards.CanBeChallengedForUno())
            {
                string message = $"{localChallengePlayer.Name} challenged {playerToGetTwoCards.Name} and won! {playerToGetTwoCards.Name} draws two cards!";

                // If the player remembered to click the uno button on the next play the the challenger gets the cards
                if (playerToGetTwoCards.CalledUno)
                {
                    message = $"{localChallengePlayer.Name} challenged {playerToGetTwoCards.Name} and lost! {localChallengePlayer.Name} draws two cards!";
                    playerToGetTwoCards = localChallengePlayer;
                }
                // Someone is getting two cards...
                for (int i = 0; i < 2; i++)
                {
                    playerToGetTwoCards.AddCard(TakeFromDealPile());
                }

                UpdateLog(message);
            }
            playerChallenge = false;

        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
        }

    }

    /// <summary>
    /// Performs the primary game mechanic of checking the cards and the player that is performing the action.
    /// </summary>
    /// <param name="c"></param>
    /// <param name="player"></param>
    public async void GameLoop(Card c, LocalPlayerBase<Player> player, bool calledRemotely = false)
    {
        if (c != Card.Empty && c != Card.FirstPlay && player == players.Current() && !stopGame)
        {
            Debug.Log($"Card {c} is in the GameLoop");
            AnimateCardFromPlayerToDiscardDeck(c, player);
            await BlockOnCardFlight(c);
            player.FixupCardPositions();
            PerformGameAction(c, false);
        }
        else if (c != Card.FirstPlay)
        {
            UpdateLog($"{player.Name} did not draw a playable card! {players.PeekNext().Name} is next!");
        }

        // Every time we run a GameLoop we're confident that we need to check a winner or we need to move to the next player.
        if (player.CheckWin(players.Count))
        {
            ShowWin(player);
        }
        else
        {
            AdvanceNextPlayer();
        }
    }

    private void AdvanceNextPlayer()
    {
        // Un dim the computer player so it gives us an indication that they are playing
        var nextPlayer = players.Next();
        foreach (var item in players)
        {
            if (item != players.Current())
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
        foreach (var item in players)
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

    private void CallPUNRpc(Card c, LocalPlayer player)
    {
        var s = Guid.NewGuid().ToString();
        Debug.Log($"Calling UpdateRoomProperties with {s}");
        var hashTable = new Hashtable
        {
            [Constants.PlayerSendingMessage] = player.Player.ActorNumber,
            [Constants.CardToPlay] = c.CardRandom,
            [Constants.CardColor] = c.Color.ToString(),
            [Constants.CardWildColor] = c.WildColor.ToString(),
            [Constants.CardValue] = c.Value.ToString(),
            [Constants.UpdateGuid] = s
        };
        photonView.RPC("SendMoveToAllPlayers", RpcTarget.AllViaServer, hashTable);
    }

    /// <summary>
    /// The business end of the game mechanics. Will apply the actions to the correct players
    /// </summary>
    /// <param name="c"></param>
    /// <param name="firstPlay"></param>
    public void PerformGameAction(Card c, bool firstPlay = false)
    {
        var player = players.Current();
        var nextPlayer = firstPlay ? players.Current() : players.PeekNext();
        // If it is not a player's turn we should just skip

        // Take the player's card and put it on the discard pile. 
        GameAction ga = PutCardOnDiscardPile(c, false, false);
        Debug.Log($"Card {c} is in the Discard Pile");

        if (c.Color == Card.CardColor.Wild && !firstPlay)
        {
            Debug.Log($"Card {c} is a wild card");
            c.SetProps(c.CardRandom, c.Value, c.WildColor);
        }

        var nextSound = rand.NextUInt(0, (uint)cardPlaySounds.Count);
        AudioClip clipToPlay = cardPlaySounds[(int)nextSound];

        switch (ga)
        {
            case GameAction.Reverse:
                players.SwapDirections();

                if (players.Count == 2)
                {
                    UpdateLog($"{player.Name} played a {c}! Skipping {nextPlayer}!");
                    players.Next();
                }

                if (firstPlay)
                {
                    UpdateLog($"Starting in reverse direction. {players.PeekNext().Name} is first!");
                }
                else
                {
                    UpdateLog($"{player.Name} reversed play, {players.PeekNext().Name} is next!");
                }
                break;

            case GameAction.Skip:
                if (firstPlay)
                {
                    UpdateLog($"{player.Name} was skipped. {players.PeekNext().Name} is first!");
                }
                else
                {
                    UpdateLog($"{player.Name} skipped {nextPlayer.Name}!");
                    players.Next();
                }
                break;

            case GameAction.DrawTwo:
                for (int i = 0; i < 2; i++)
                {
                    nextPlayer.AddCard(TakeFromDealPile());
                }

                if (firstPlay)
                {
                    UpdateLog($"Draw Two on first play! {nextPlayer.Name} takes 2 cards! {players.PeekNext().Name} starts!");
                }
                else
                {
                    UpdateLog($"{nextPlayer.Name} must Draw Two! Skipping {nextPlayer.Name}!");
                    players.Next();
                }
                break;
            case GameAction.DrawFour:
                for (int i = 0; i < 4; i++)
                {
                    nextPlayer.AddCard(TakeFromDealPile());
                }
                UpdateLog($"{nextPlayer.Name} must Draw Four! Skipping {nextPlayer.Name}!");
                players.Next();
                break;
            case GameAction.DrawAndSkip:
                UpdateLog($"{player.Name} did not draw a playable card! {players.PeekNext().Name} is next!");
                player.AddCard(TakeFromDealPile());
                break;
            case GameAction.DrawAndPlayOnce:
                Debug.Log($"{player.Name} chose DrawAndPlayOnce!");
                player.AddCard(TakeFromDealPile());
                // Move the player cursor back to the previous player so this player can go again.
                players.Prev();
                break;
            case GameAction.Wild:
                if (!firstPlay)
                {
                    UpdateLog($"{player.Name} played a {c.WildColor} Wild!");
                }
                break;
            case GameAction.NextPlayer:
            // In this case we just let it slide to the next player by using the loop.
            default:
                UpdateLog($"{player.Name} played a {c}");
                break;
        }

        audioSource.clip = clipToPlay;
        audioSource.Play();

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
                var s = Guid.NewGuid().ToString();
                Debug.Log($"Calling UpdateRoomProperties with {s}");
                var hashTable = new Hashtable
                {
                    [Constants.PlayerSendingMessage] = PhotonNetwork.LocalPlayer.ActorNumber,
                    [Constants.RestartGameAfterWin] = true,
                };
                photonView.RPC("RestartGame", RpcTarget.AllViaServer, hashTable);
            }
        }
    }
}
