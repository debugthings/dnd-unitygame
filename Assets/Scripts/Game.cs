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
    private bool quit;

    private const float cardStackZOrderOffset = 0.01f;

    private CircularList<LocalPlayer> players = new CircularList<LocalPlayer>();

    private List<Card.CardValue> cardValuesArray = new List<Card.CardValue>((Card.CardValue[])Enum.GetValues(typeof(Card.CardValue)));
    private Card.CardColor[] cardColorArray = new Card.CardColor[] { Card.CardColor.Red, Card.CardColor.Green, Card.CardColor.Blue, Card.CardColor.Yellow };
    private Card.CardValue[] specials = new Card.CardValue[] { Card.CardValue.DrawFour };

    private GameOptions gameOptions;

    private int numOfPlayers = 4;
    private int numberOfDecks = 1;
    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();
    private IDictionary<Player, NetworkPlayer> networkPlayersList = new Dictionary<Player, NetworkPlayer>();


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

    // Start is called before the first frame update
    void Start()
    {
        // TODO Initialize a please wait here.
    }

    public override async void OnJoinedRoom()
    {
        await StartGame();
        base.OnJoinedRoom();
    }
    private async Task StartGame()
    {
        Debug.Log("Entered Start()");
        if (PhotonNetwork.CurrentRoom.CustomProperties != null)
        {
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Constants.SeedKeyName, out object value);
            uint roomSeed = (uint)value;
            rand.InitState(roomSeed);
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

        int totalPlayer = 1;
        Debug.Log("Build human players");
        totalPlayer = BuildLocalPlayer(totalPlayer);
        if (numOfPlayers > 1)
        {
            Debug.Log("Build computer players");
            totalPlayer = BuildNetworkPlayers(totalPlayer);
        }
        //if (gameOptions.ComputerPlayers > 0)
        //{
        //    Debug.Log("Build computer players");
        //    totalPlayer = BuildComputerPlayers(totalPlayer);
        //}


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
                    CreateCardOnDealDeck(rand.NextInt(0, totalCards), cardColorArray[colorIndex], cardValuesArray[cardValueIndex]);
                }
            }
        }

        Debug.Log("Build Zero Cards");

        // There is one set of Zero cards per color per deck
        for (int num = 0; num < numberOfDecks; num++)
        {
            for (int colorIndex = 0; colorIndex < cardColorArray.Length; colorIndex++)
            {
                CreateCardOnDealDeck(rand.NextInt(0, totalCards), cardColorArray[colorIndex], Card.CardValue.Zero);

            }
        }

        Debug.Log("Build Wild Cards");

        // Generate the correct number of wild cards 4 cards per number of decks...
        for (int i = 0; i < 4 * numberOfDecks; i++)
        {
            CreateCardOnDealDeck(rand.NextInt(0, totalCards), Card.CardColor.Wild, Card.CardValue.Wild);
            CreateCardOnDealDeck(rand.NextInt(0, totalCards), Card.CardColor.Wild, Card.CardValue.DrawFour);
        }

        // Use this to generate a proper z-order of the deck
        dealDeck.MaxStackDepth = dealDeck.Count() * cardStackZOrderOffset;
        discardDeck.MaxStackDepth = dealDeck.Count() * cardStackZOrderOffset;

        Debug.Log("Build deck and add jitter");
        // Push the cards in the random order to a Stack...
        dealDeck.Shuffle();

        // Assing a random id to each card so we can send that to the players
        dealDeck.AssignRandomId();

        Debug.Log("Deal cards to players");
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
            if (item is ComputerPlayer)
            {
                (item as ComputerPlayer).DimCards(true);
            }
        }

        Debug.Log("Play top card");
        PutCardOnDiscardPile(TakeFromDealPile(), true, true);
        // There are a couple of rules on the first play
        // If it's a wild the first player chooses the color.
        // If it's a wild draw four the card goes back into the pile.
        FirstPlay(CurrentPlayer);

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
        var firstCard = dealDeck.PeekTopCard();
        switch (firstCard.Value)
        {
            case Card.CardValue.Skip:
            case Card.CardValue.Reverse:
                // If these actions are first we need to start the game loop since the first player will be skipped
                PerformGameAction(firstCard, true);
                GameLoop(Card.Empty, player);
                break;
            case Card.CardValue.DrawTwo:
            case Card.CardValue.Wild:
                // These are all valid cards that will be played on the first player.
                PerformGameAction(firstCard, true);
                break;
            case Card.CardValue.DrawFour:
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
        if (PlayerCanMakeMove())
        {
            var cardDeck = cardObject.GetComponentInParent<CardDeck>();
            var player = cardObject.GetComponentInParent<LocalPlayer>();

            if (cardDeck is CardDeck && cardDeck.name == "DealDeck")
            {
                // If we're in play and the player decides to draw, either the card will be played or added to the hand.
                // When we're in networked mode we'll need to take into account that anyone can double click the deck so we'll need to make sure
                // the click originated from the player.
                var cardToPlay = TakeFromDealPile();
                if (cardToPlay.CanPlay(discardDeck.PeekTopCard()))
                {
                    cardToPlay.SetCardFaceUp(true);

                    if (cardToPlay.Color == Card.CardColor.Wild)
                    {
                        HandleWildCard(cardToPlay);
                    }
                    else
                    {
                        GameLoop(cardToPlay, LocalPlayer);
                    }
                }
                else
                {
                    LocalPlayer.AddCard(cardToPlay);
                    GameLoop(Card.Empty, LocalPlayer);
                }
            }
            else if (player is LocalPlayer)
            {
                // If we're here we've likely tried to play a card. We need to check to see the card is okay to play.
                var cardToPlay = LocalPlayer.PlayCard(cardObject, discardDeck.PeekTopCard(), false);
                if (cardToPlay != Card.Empty)
                {
                    if (cardToPlay.Color == Card.CardColor.Wild)
                    {
                        HandleWildCard(cardToPlay);
                    }
                    else
                    {
                        GameLoop(cardObject, LocalPlayer);
                    }
                }
            }
        }
    }

    private void HandleWildCard(Card cardToPlay)
    {
        var compButton = wildCardSelectPrefab.GetComponentInChildren<SelectWildButton>();
        var wildCardPrefab = Instantiate(wildCardSelectPrefab, transform);
        SelectWildButton.CardToChange = cardToPlay;
        SelectWildButton.ReturnCard = (card) =>
        {
            GameLoop(card, LocalPlayer);
            Destroy(wildCardPrefab);
        };
    }

    private int BuildComputerPlayers(int totalPlayer)
    {
        for (int i = 0; i < gameOptions.ComputerPlayers; i++)
        {
            var ply = PlaceInCircle<ComputerPlayer>(transform, computerPlayerPrefab, totalPlayer++ - 1, numOfPlayers, 0.5f);
            string s = $"Computer {i + 1}";
            ply.CurrentGame = this;
            ply.SetName(s);
            ply.name = s;
            ply.DimmableCardObject = dimmableCardPrefab;
            players.Add(ply);
        }

        return totalPlayer;
    }

    private int BuildLocalPlayer(int totalPlayer)
    {
        for (int i = 0; i < gameOptions.HumanPlayers; i++)
        {
            var ply = PlaceInCircle<LocalPlayer>(transform, playerPrefab, totalPlayer++ - 1, numOfPlayers, 0.5f);
            ply.CurrentGame = this;
            ply.SetName(PhotonNetwork.LocalPlayer.NickName);
            ply.Player = PhotonNetwork.LocalPlayer;
            players.Add(ply);
            LocalPlayer = ply;
        }

        return totalPlayer;
    }

    private int BuildNetworkPlayers(int totalPlayer)
    {
        var playersInRoom = PhotonNetwork.CurrentRoom.Players;

        foreach (var player in playersInRoom)
        {
            if (player.Value != PhotonNetwork.LocalPlayer)
            {
                var ply = PlaceInCircle<NetworkPlayer>(transform, playerPrefab, totalPlayer++ - 1, numOfPlayers, 0.5f);
                ply.SetNetworkPlayerObject(player.Value);
                ply.CurrentGame = this;
                ply.SetName(player.Value.NickName);
                players.Add(ply);
                networkPlayersList.Add(player.Value, ply);
            }

        }
        return totalPlayer;
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
    /// The game options that control how the game behaves
    /// </summary>
    private static T PlaceInCircle<T>(Transform centerOfScreen, GameObject prefab, int itemNumber, int totalObjects, float radius) where T : MonoBehaviour
    {
        float angle = itemNumber * Mathf.PI * 2 / totalObjects;
        GameObject playerInstantiate = null;
        // TODO Properly place the players at the correct locations.
        if (itemNumber == 0)
        {
            playerInstantiate = Instantiate(prefab, new Vector3(0, -5), Quaternion.identity, centerOfScreen);
        }
        else if (itemNumber == 1)
        {
            playerInstantiate = Instantiate(prefab, new Vector3(9, 0), Quaternion.identity, centerOfScreen);
        }
        else if (itemNumber == 2)
        {
            playerInstantiate = Instantiate(prefab, new Vector3(0, 5), Quaternion.identity, centerOfScreen);
        }
        else if (itemNumber == 3)
        {
            playerInstantiate = Instantiate(prefab, new Vector3(-9, 0), Quaternion.identity, centerOfScreen);
        }

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
        if (dealDeck.Count == 0)
        {
            dealDeck.SwapCardsFromOtherDeck(discardDeck);
        }
        return dealDeck.TakeTopCard();
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
            return cardAction;
        }

        // If we have a card that will play we need to continue on
        if (dontCheckEquals || discardDeck.PeekTopCard().CanPlay(card))
        {
            // Take the card that is in play and put it on top.
            discardDeck.AddCardToDeck(card, true);
            return cardAction;
        }

        return GameAction.NextPlayer;
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (true)
        {
            var playerSending = players.Find(player => player.Player.ActorNumber == (int)propertiesThatChanged[Constants.PlayerSendingMessage]);
            var cardToPlay = playerSending.Hand.Find(card => card.RandomId == (uint)propertiesThatChanged[Constants.CardToPlay]);
            GameLoop(cardToPlay, playerSending, true);
        }
        base.OnRoomPropertiesUpdate(propertiesThatChanged);
    }

    /// <summary>
    /// Performs the primary game mechanic of checking the cards and the player that is performing the action.
    /// </summary>
    /// <param name="c"></param>
    /// <param name="player"></param>
    public void GameLoop(Card c, LocalPlayer player, bool calledRemotely = false)
    {

        if (c != Card.Empty && player == players.Current() && !stopGame)
        {
            if (!calledRemotely)
            {
                UpdateRoomProperties(c, player);
            }
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
            if (nextPlayer is ComputerPlayer)
            {
                // Add a bogus delay so the game seems like somoene is playing with us.
                Invoke("CheckAndExecuteComputerPlayerAction", 3.0f);
            }
            else if (nextPlayer is NetworkPlayer)
            {
                // This ends the loop for this play and will wait for the network player to make a move
            }

        }
    }

    private static void UpdateRoomProperties(Card c, LocalPlayer player)
    {
        var hashTable = new ExitGames.Client.Photon.Hashtable
        {
            [Constants.SeedKeyName] = PhotonNetwork.CurrentRoom.CustomProperties[Constants.SeedKeyName],
            [Constants.PlayerSendingMessage] = player.Player.ActorNumber,
            [Constants.CardToPlay] = c.RandomId
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);
    }

    /// <summary>
    /// Drives the loop forward
    /// </summary>
    private void CheckAndExecuteComputerPlayerAction()
    {
        if (CurrentPlayer is ComputerPlayer && !stopGame)
        {
            var playerRef = CurrentPlayer as ComputerPlayer;
            try
            {
                var card = playerRef.PlayCard(null, discardDeck.PeekTopCard(), false);
                if (card == Card.Empty)
                {
                    playerRef.AddCard(TakeFromDealPile());
                    GameLoop(Card.Empty, playerRef);
                }
                else
                {
                    GameLoop(card, playerRef);

                }
                playerRef.DimCards(true);
            }
            catch (InvalidOperationException)
            {
                // Means the stack is empty
                TakeFromDealPile();
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }

        }
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
        if (c.Color == Card.CardColor.Wild)
        {
            c.SetProps(c.CardRandom, c.Value, c.WildColor);
        }
        switch (ga)
        {
            case GameAction.Reverse:
                players.Reverse();
                if (firstPlay)
                {
                    Console.WriteLine($"Reverse on draw moving in other direction, sorry {player.Name}, {players.PeekNext().Name} is first!");
                    players.Next();
                }
                else
                {
                    Console.WriteLine($"{player.Name} reversed play, {players.PeekNext().Name} is next!");
                }
                break;
            case GameAction.Skip:
                if (firstPlay)
                {
                    Console.WriteLine($"{player.Name} was skipped on the first turn!");
                }
                else
                {
                    Console.WriteLine($"{player.Name} skipped {nextPlayer.Name}!");
                }
                players.Next();
                break;
            case GameAction.DrawTwo:
                Console.WriteLine($"{nextPlayer.Name} must Draw Two!");
                for (int i = 0; i < 2; i++)
                {
                    nextPlayer.AddCard(TakeFromDealPile());
                }
                break;
            case GameAction.DrawFour:
                Console.WriteLine($"{nextPlayer.Name} must Draw Four!");
                for (int i = 0; i < 4; i++)
                {
                    nextPlayer.AddCard(TakeFromDealPile());
                }
                break;
            case GameAction.DrawAndSkip:
                player.AddCard(TakeFromDealPile());
                break;
            case GameAction.DrawAndPlayOnce:
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
