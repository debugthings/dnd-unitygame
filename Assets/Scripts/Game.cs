using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TMPro;
using System.Threading.Tasks;
using System.Threading;
using Photon.Realtime;
using Photon.Pun;
using ExitGames.Client.Photon;
using Assets.Scripts;

public class Game : MonoBehaviourPunCallbacks, IConnectionCallbacks
{
    private readonly LoadBalancingClient client = new LoadBalancingClient();

    private const float cardStackZOrderOffset = 0.01f;

    private CircularList<LocalPlayer> players = new CircularList<LocalPlayer>();

    private List<Card.CardValue> cardValuesArray = new List<Card.CardValue>((Card.CardValue[])Enum.GetValues(typeof(Card.CardValue)));
    private Card.CardColor[] cardColorArray = new Card.CardColor[] { Card.CardColor.Red, Card.CardColor.Green, Card.CardColor.Blue, Card.CardColor.Yellow };
    private Card.CardValue[] specials = new Card.CardValue[] { Card.CardValue.DrawFour };

    private GameOptions gameOptions;

    private int numOfPlayers = 4;
    private int numberOfDecks = 1;
    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();
    private IDictionary<Player, LocalPlayer> networkPlayersList = new Dictionary<Player, LocalPlayer>();


    private GameObject cardPrefab;
    private GameObject dimmableCardPrefab;
    private GameObject playerPrefab;
    private GameObject computerPlayerPrefab;
    private GameObject winnerBannerPrefab;
    private GameObject wildCardSelectPrefab;


    public CardDeck dealDeck;
    public CardDeck discardDeck;

    // These are objects that are created on the fly so we want to load the asset from the asset store
    public AssetReference playerReference;
    public AssetReference computerPlayerReference;
    public AssetReference cardReference;
    public AssetReference dimmableCardReference;
    public AssetReference winnerBanner;
    public AssetReference wildCardSelect;


    public LocalPlayer CurrentPlayer => players.Current();

    /// <summary>
    /// Gets the singular local player
    /// </summary>
    public LocalPlayer LocalPlayer { get; private set; }

    private bool stopGame;

    private bool gameStarted = false;

    // Start is called before the first frame update
    async void Start()
    {
        // TODO Initialize a please wait here.
        if (PhotonNetwork.IsConnectedAndReady)
        {
            await StartGame();
        }

    }

    public override async void OnJoinedRoom()
    {
        if (!gameStarted)
        {
            await StartGame();
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


    private async Task StartGame()
    {
        gameStarted = true;
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

        await LoadAllPrefabs();

        if (gameOptions.HumanPlayers < 1)
        {
            throw new ArgumentOutOfRangeException("You must have 1 or more human players for this game!");
        }

        numOfPlayers = PhotonNetwork.CurrentRoom.PlayerCount;

        Debug.Log("Build players");
        BuildGamePlayers();

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

        int totalNumberCards = (numberOfDecks * 2) * cardColorArray.Length * cardValuesArray.Count;
        int totalZeroCards = (numberOfDecks * 2) * cardColorArray.Length;
        int totalWildCards = (numberOfDecks * 4) * 2;

        int totalCards = totalNumberCards + totalZeroCards + totalWildCards;

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
        Player dealer = default;

        // For each player in the player list find the one that is next to the dealer.
        foreach (var item in PhotonNetwork.PlayerList)
        {
            if (item.IsMasterClient)
            {
                nextToDealer = item.GetNext();
                break;
            }
        }

        // Set the circular list to the correct player grouping for dealer
        players.SetPlayer(networkPlayersList[nextToDealer]);

        // Deal out the players 
        for (int i = 0; i < gameOptions.NumberOfCardsToDeal; i++)
        {
            for (int j = 0; j < numOfPlayers; j++)
            {
                // The circular list allows us to start dealing from the "first" position
                players.Current().AddCard(TakeFromDealPile());
                players.Next();
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
        
        // There are a couple of rules on the first play
        // If it's a wild the first player chooses the color.
        // If it's a wild draw four the card goes back into the pile.
        // We will pretend this card is dealt by the dealer so gameplay will behave as expected.
        players.SetPlayer(networkPlayersList[dealer]);
        FirstPlay(players.Current());

        Debug.Log("Leaving Start()");
    }

    private void CreateCardOnDealDeck(int randomValue, Card.CardColor cardColor, Card.CardValue cardValue)
    {
        GameObject instantiatedCardObject = Instantiate(cardPrefab, dealDeck.transform);
        var instantiatedCard = instantiatedCardObject.GetComponent<Card>();
        instantiatedCard.SetProps(randomValue, cardValue, cardColor);
        instantiatedCard.name = instantiatedCard.ToString();
        Debug.Log($"Built {instantiatedCard.name}");
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

    private void FirstPlay(LocalPlayer player)
    {
        var firstCard = discardDeck.PeekTopCard();
        switch (firstCard.Value)
        {
            case Card.CardValue.Skip:
            case Card.CardValue.Reverse:
                Debug.Log($"Reverse on first play");
                // If these actions are first we need to start the game loop since the first player will be skipped
                PerformGameAction(firstCard, true);
                GameLoop(Card.Empty, player);
                break;
            case Card.CardValue.DrawTwo:
            case Card.CardValue.Wild:
                Debug.Log($"Wild on first play");
                // These are all valid cards that will be played on the first player.
                PerformGameAction(firstCard, true);
                break;
            case Card.CardValue.DrawFour:
                Debug.Log($"Draw four on first play");
                // When we have a draw four we need to put it back into the deck.
                dealDeck.PutCardBackInDeckInRandomPoisiton(discardDeck.TakeTopCard(), 3, 50);
                discardDeck.AddCardToDeck(dealDeck.TakeTopCard(), true);
                break;
            default:
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
    public void PlayClickedCard(Card cardObject)
    {
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
                    UpdateRoomProperties(cardToPlay, LocalPlayer);
                }
            }
        }
        Debug.Log("Left PlayClickedCard");
        Debug.Log("");
    }

    private void HandleWildCard(Card cardToPlay)
    {
        var compButton = wildCardSelectPrefab.GetComponentInChildren<SelectWildButton>();
        var wildCardPrefab = Instantiate(wildCardSelectPrefab, transform);
        SelectWildButton.CardToChange = cardToPlay;
        SelectWildButton.ReturnCard = (card) =>
        {
            UpdateRoomProperties(card, LocalPlayer);
            //GameLoop(card, LocalPlayer);
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

        var camera = Camera.main;
        var circleRadius = camera.orthographicSize;
        int playerCounter = 1;
        foreach (var player in playersInRoom)
        {
            // Determin the position of the player in the circle
            int baseNumber = (maxPlayers - myNumber + playerCounter++);
            int position = baseNumber - maxPlayers <= 0 ? baseNumber : baseNumber - maxPlayers;

            // Determine the eccentricity of the screen
            Camera cam = Camera.main;
            float minorAxis = cam.orthographicSize;
            float majorAxis = minorAxis * cam.aspect;

            // Scale down when over a specific size
            var scale = numOfPlayers >= 6 ? 0.75 : 1;
            var maxNumberOfCards = numOfPlayers >= 6 ? 8 : 10;

            Debug.Log($"Creating player {player.Value.NickName} with actor number {player.Value.ActorNumber}");
            if (player.Value != PhotonNetwork.LocalPlayer)
            {
                var ply = PlaceInCircle<NetworkPlayer>(transform, computerPlayerPrefab, position, numOfPlayers, minorAxis, majorAxis);
                ply.Player = player.Value;
                ply.CurrentGame = this;
                ply.SetName(player.Value.NickName);
                ply.DimmableCardObject = dimmableCardPrefab;
                ply.MaxNumberOfCardsInRow = maxNumberOfCards;
                ply.transform.localScale = 0.5f * Vector3.one;
                networkPlayersList.Add(player.Value, ply);
                players.Add(ply);
            }
            else
            {
                // The local player is always at the 1 position.
                var ply = PlaceInCircle<LocalPlayer>(transform, playerPrefab, position, numOfPlayers, minorAxis, majorAxis);
                ply.Player = player.Value;
                ply.CurrentGame = this;
                ply.SetName(player.Value.NickName);
                LocalPlayer = ply;
                networkPlayersList.Add(player.Value, ply);
                players.Add(ply);
            }
        }
    }

    private void FixupCardsPerColor()
    {
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

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// Plkace the player around the unit cirlce at the specified section
    /// </summary>
    private static T PlaceInCircle<T>(Transform centerOfScreen, GameObject prefab, int itemNumber, int totalObjects, float minorAxis, float majorAxis) where T : MonoBehaviour
    {
        // The local player will always be at 3pi/2 (270 degrees) on the circle
        // This is the bottom of the player screen. All others will be situated around the circle

        float degrees270 = (Mathf.PI * 3) / 2;
        float angle = ((itemNumber - 1) * Mathf.PI * 2) / totalObjects;
        float angleOncircle = degrees270 + angle;
        float x = majorAxis * Mathf.Cos(angleOncircle);
        float y = minorAxis * Mathf.Sin(angleOncircle);
        Debug.Log($"Player (x,y) coordinate = ({x},{y})");
        Debug.Log($"Player angle on circle = {angleOncircle * Mathf.Rad2Deg}");
        GameObject playerInstantiate = Instantiate(prefab, new Vector3(x, y), Quaternion.LookRotation(Vector3.zero, Vector3.up), centerOfScreen);
        var player = playerInstantiate.GetComponent<T>();
        player.transform.eulerAngles = Vector3.forward * Mathf.Rad2Deg * angle;
        return player;
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

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
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
                        playerSending.AddCard(cardToPlay);
                    }

                    //TODO In the evnt this client is out of sync we need to resync
                }

                if (cardToPlay.Color == Card.CardColor.Wild)
                {
                    // When we're here we need to make sure we honor the player's wild color choice
                    Debug.Log($"Set {cardToPlay} to wild color {cardWildColor}");
                    cardToPlay.SetWildColor(cardWildColor);
                }

                Debug.Log($"Playing card {cardToPlay} with Id {cardToPlay.CardRandom}");
                cardToPlay = playerSending.PlayCard(cardToPlay, discardDeck.PeekTopCard(), false);
                GameLoop(cardToPlay, playerSending);

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

    /// <summary>
    /// Performs the primary game mechanic of checking the cards and the player that is performing the action.
    /// </summary>
    /// <param name="c"></param>
    /// <param name="player"></param>
    public void GameLoop(Card c, LocalPlayer player, bool firstPlay = false)
    {

        if (c != Card.Empty && player == players.Current() && !stopGame)
        {
            Debug.Log($"Card {c} is in the GameLoop");
            PerformGameAction(c, false);
        }

        if (player.CheckWin())
        {
            var textMeshProObjects = winnerBannerPrefab.GetComponentsInChildren<TextMeshPro>();
            foreach (var item in textMeshProObjects)
            {
                item.text = $"{player.name} WINS!";
            }
            Instantiate(winnerBannerPrefab, transform);
            stopGame = true;
        }
        else
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

            if (nextPlayer is NetworkPlayer)
            {
                // This ends the loop for this play and will wait for the network player to make a move
            }

        }
    }

    private static void UpdateRoomProperties(Card c, LocalPlayer player)
    {
        var s = Guid.NewGuid().ToString();
        Debug.Log($"Calling UpdateRoomProperties with {s}");
        var hashTable = new ExitGames.Client.Photon.Hashtable
        {
            [Constants.PlayerSendingMessage] = player.Player.ActorNumber,
            [Constants.CardToPlay] = c.CardRandom,
            [Constants.CardColor] = c.Color.ToString(),
            [Constants.CardWildColor] = c.WildColor.ToString(),
            [Constants.CardValue] = c.Value.ToString(),
            [Constants.UpdateGuid] = s
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);
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
        GameAction ga;
        if (firstPlay)
        {
            ga = ConvertCardToAction(c);
        }
        else
        {
            ga = PutCardOnDiscardPile(c, false, false);
        }

        Debug.Log($"Card {c} is in the Discard Pile");
        if (c.Color == Card.CardColor.Wild)
        {
            Debug.Log($"Card {c} is a wild card");
            c.SetProps(c.CardRandom, c.Value, c.WildColor);
        }
        switch (ga)
        {
            case GameAction.Reverse:
                players.Reverse();
                if (players.Count == 2)
                {
                    Debug.Log($"Two player game skipping {nextPlayer}!");
                    players.Next();
                }
                if (firstPlay)
                {
                    Debug.Log($"Reverse on draw moving in other direction, sorry {player.Name}, {players.PeekNext().Name} is first!");
                    players.Next();
                }
                else
                {
                    Debug.Log($"{player.Name} reversed play, {players.PeekNext().Name} is next!");
                }
                break;
            case GameAction.Skip:
                if (firstPlay)
                {
                    Debug.Log($"{player.Name} was skipped on the first turn!");
                }
                else
                {
                    Debug.Log($"{player.Name} skipped {nextPlayer.Name}!");
                }
                players.Next();
                break;
            case GameAction.DrawTwo:
                Debug.Log($"{nextPlayer.Name} must Draw Two!");
                for (int i = 0; i < 2; i++)
                {
                    nextPlayer.AddCard(TakeFromDealPile());
                }
                Debug.Log($"Skipping {nextPlayer.Name}!");
                players.Next();
                break;
            case GameAction.DrawFour:
                Debug.Log($"{nextPlayer.Name} must Draw Four!");
                for (int i = 0; i < 4; i++)
                {
                    nextPlayer.AddCard(TakeFromDealPile());
                }
                Debug.Log($"Skipping {nextPlayer.Name}!");
                players.Next();
                break;
            case GameAction.DrawAndSkip:
                Debug.Log($"{player.Name} chose DrawAndSkip!");
                player.AddCard(TakeFromDealPile());
                break;
            case GameAction.DrawAndPlayOnce:
                Debug.Log($"{player.Name} chose DrawAndPlayOnce!");
                player.AddCard(TakeFromDealPile());
                // Move the player cursor back to the previous player so this player can go again.
                players.Prev();
                break;
            case GameAction.Wild:
                break;
            case GameAction.NextPlayer:
            // In this case we just let it slide to the next player by using the loop.
            default:
                break;
        }
    }
}
