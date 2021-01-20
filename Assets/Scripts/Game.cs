using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TMPro;
using System.Threading.Tasks;

public class Game : MonoBehaviour
{
    public class GameOptions
    {
        /// <summary>
        /// The number of human players for this game.
        /// </summary>
        /// <value>Default is 1</value>
        public int HumanPlayers { get; set; } = 1;

        /// <summary>
        /// The number of computer players for this game. 
        /// </summary>
        /// <value>Default is 3</value>
        public int ComputerPlayers { get; set; } = 3;

        /// <summary>
        /// The maximum number of decks for this game.
        /// </summary>
        /// <value>Default is 5</value>
        public int MaxDecks { get; set; } = 5;

        /// <summary>
        /// The minimum number of decks for this game.
        /// </summary>
        /// <value>Default is 1</value>
        public int BaseNumberOfDecks { get; set; } = 1;

        /// <summary>
        /// How many cards to deal for the initial hand.
        /// </summary>
        /// <value>Default is 5</value>
        public int NumberOfCardsToDeal { get; set; } = 5;

        /// <summary>
        /// How many players per deck.
        /// </summary>
        /// <remarks>
        /// What we want to do here is have a game where the number of decks is in some multiple of the number of players
        /// However in the event that we have n-(n/2) > 2 players that would trigger a new deck, we should opt to increase the number of decks.
        /// For example, if we have 4 players per deck and the game has 7 players, we should add another deck to be sure.
        /// </remarks>
        /// <value>Default is 4</value>
        public int PlayersPerDeck { get; set; } = 4;

        /// <summary>
        /// The maximum number of cards allowed when <see cref="AllowStacking"/> is enabled.
        /// </summary>
        public int MaxStackCards { get; set; } = 3;

        /// <summary>
        /// Allow a player to "stack" cards for play.
        /// </summary>
        /// <remarks>
        /// <para>This is a sure fire way to make everyone hate you. What this value does is allows a player to stack or chain cards.</para>
        /// <para>For example:</para><para>A player has in their hand: Red Three, Green Three, Wild, Yellow Five, Blue Five. A player is presented with a Red Zero; in normal play they can only play any Red card, any color Zero, or a wild.</para><para>In a stacked game, however, you could chain the cards to play up to the <see cref="MaxStackCards"/> or unitl you'd reach "Uno". Use this at your own risk.</para> 
        /// </remarks>
        /// <value>Default is false.</value>
        public bool AllowStacking { get; set; }

        /// <summary>
        /// Sets if the player has to call "Uno" when they reach their last card. Will use the <see cref="UnoTimeoutForgiveness"/>
        /// </summary>
        /// <value>Defualt is true</value>
        public bool PlayerHasToCallUno { get; set; } = true;

        /// <summary>
        /// The amount of time to let the "Uno" player have before they are forced to draw two.
        /// </summary>
        /// <remarks>In normal play the person with one card left MUST call "Uno" before the next play so they do not incur a two card penalty. In online play we will have a button to click. If the next player goes before the button is clicked the current "Uno" player will be afforded a time window to click the button.</remarks>
        /// <value>Default is 5 seconds.</value>
        public TimeSpan UnoTimeoutForgiveness { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Most actions are simple, draw 2, draw 4, reverse, etc.
        /// </summary>
        public bool AllowCustomActionCards { get; set; }
    }

    private const float cardStackZOrderOffset = 0.01f;
    private const float maxJitterTranslation = 0.06f;
    private const float maxJitterRotation = 2.0f;
    private SortedDictionary<Guid, Card> deck = new SortedDictionary<Guid, Card>();
    private CircularList<Player> players = new CircularList<Player>();

    private List<Card.CardValue> cardValuesArray = new List<Card.CardValue>((Card.CardValue[])Enum.GetValues(typeof(Card.CardValue)));
    private Card.CardColor[] cardColorArray = new Card.CardColor[] { Card.CardColor.Red, Card.CardColor.Green, Card.CardColor.Blue, Card.CardColor.Yellow };
    private Card.CardValue[] specials = new Card.CardValue[] { Card.CardValue.DrawFour };

    private GameOptions gameOptions;

    private int numOfPlayers = 4;
    private int numberOfDecks = 1;
    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();

    private float maxStackDepth = 0.0f;

    public Card dealDeck;
    public Card discardDeck;
    public Player player;
    public ComputerPlayer computerPlayer;
    
    public AssetReference cardReference;
    public AssetReference dimmableCardReference;


    private GameObject cardPrefab;
    private GameObject dimmableCardPrefab;


    private GameObject winnerBanner = null;


    public Card TopCardOnDiscard => this.DiscardPile.Peek();
    public Card TopCardOnDeal => this.DealPile.Peek();
    public Player CurrentPlayer => this.players.Current();

    public Player HumanPlayer = null;
    private bool stopGame;


    // Start is called before the first frame update
    async void Start()
    {

        Debug.Log("Entered Start()");
        rand.InitState();
        this.gameOptions = gameOptions ?? new GameOptions();

        Debug.Log("Loading Card Prefab");
        var cardPre = cardReference.LoadAssetAsync<GameObject>();
        cardPrefab = await cardPre.Task;

        Debug.Log("Loading Dimmable Card Prefab");
        var dimmablePrefabOperation = dimmableCardReference.LoadAssetAsync<GameObject>();
        dimmableCardPrefab = await dimmablePrefabOperation.Task;

        Debug.Log("Card Prefab Loaded");
        if (this.gameOptions.HumanPlayers < 1)
        {
            throw new ArgumentOutOfRangeException("You must have 1 or more human players for this game!");
        }
        this.numOfPlayers = this.gameOptions.HumanPlayers + this.gameOptions.ComputerPlayers;


        int totalPlayer = 1;
        Debug.Log("Build human players");
        totalPlayer = BuildHumanPlayers(totalPlayer);
        Debug.Log("Build computer players");
        totalPlayer = BuildComputerPlayers(totalPlayer);

        // What we want to do here is have a game where the number of decks is in some multiple of the number of players
        // However in the event that we have n-(n/2) > 2 players that would trigger a new deck, we should opt to increase the number of decks.
        // For example, if we have 4 players per deck and the game has 7 players, we should add another deck to be sure.
        int baseNumber = (numOfPlayers - (numOfPlayers % this.gameOptions.PlayersPerDeck)) / this.gameOptions.PlayersPerDeck;
        if ((numOfPlayers % this.gameOptions.PlayersPerDeck) > this.gameOptions.PlayersPerDeck / 2)
        {
            baseNumber++;
        }

        // We should always have 1 deck but a max of max decks (5)
        numberOfDecks = MathExtension.Clamp(baseNumber, this.gameOptions.BaseNumberOfDecks, this.gameOptions.MaxDecks);

        // Remove cards so we can just use the loops below
        FixupCardsPerColor();

        Debug.Log("Build Number Cards");
        // Build the deck and create a random placement
        // Shuffling idea taken from https://blog.codinghorror.com/shuffling/
        // There are two sets of numbers and action cards per color per deck
        for (int num = 0; num < (this.numberOfDecks * 2); num++)
        {
            for (int colorIndex = 0; colorIndex < cardColorArray.Length; colorIndex++)
            {
                for (int cardValueIndex = 0; cardValueIndex < cardValuesArray.Count; cardValueIndex++)
                {

                    GameObject instantiatedCardObject = Instantiate(cardPrefab, dealDeck.transform);
                    var instantiatedCard = instantiatedCardObject.GetComponent<Card>();
                    instantiatedCard.SetProps(cardValuesArray[cardValueIndex], cardColorArray[colorIndex]);
                    instantiatedCard.name = instantiatedCard.ToString();
                    Debug.Log($"Built {instantiatedCard.name}");
                    deck.Add(Guid.NewGuid(), instantiatedCard);
                }
            }
        }

        Debug.Log("Build Zero Cards");

        // There is one set of Zero cards per color per deck
        for (int num = 0; num < this.numberOfDecks; num++)
        {
            for (int colorIndex = 0; colorIndex < cardColorArray.Length; colorIndex++)
            {
                GameObject instantiatedCardObject = Instantiate(cardPrefab, dealDeck.transform);
                var instantiatedCard = instantiatedCardObject.GetComponent<Card>();
                instantiatedCard.SetProps(Card.CardValue.Zero, cardColorArray[colorIndex]);
                instantiatedCard.name = instantiatedCard.ToString();
                Debug.Log($"Built {instantiatedCard.name}");
                deck.Add(Guid.NewGuid(), instantiatedCard);
            }
        }

        Debug.Log("Build Wild Cards");

        // Generate the correct number of wild cards 4 cards per number of decks...
        for (int i = 0; i < 4 * numberOfDecks; i++)
        {
            GameObject instantiatedWildCardObject = Instantiate(cardPrefab, dealDeck.transform);
            var instantiatedWildCard = instantiatedWildCardObject.GetComponent<Card>();
            instantiatedWildCard.SetProps(Card.CardValue.Wild, Card.CardColor.Wild);
            instantiatedWildCard.name = instantiatedWildCard.ToString();
            Debug.Log($"Built {instantiatedWildCard.name}");


            GameObject instantiatedDrawFourCardObject = Instantiate(cardPrefab, dealDeck.transform);
            var instantiatedDrawFourCard = instantiatedDrawFourCardObject.GetComponent<Card>();
            instantiatedDrawFourCard.SetProps(Card.CardValue.DrawFour, Card.CardColor.Wild);
            instantiatedDrawFourCard.name = instantiatedDrawFourCard.ToString();
            Debug.Log($"Built {instantiatedDrawFourCard.name}");

            // Add these to the deck
            deck.Add(Guid.NewGuid(), instantiatedWildCard);
            deck.Add(Guid.NewGuid(), instantiatedDrawFourCard);
        }

        // Use this to generate a proper zorder of the deck
        maxStackDepth = deck.Count() * cardStackZOrderOffset;

        Debug.Log("Build deck and add jitter");
        // Push the cards in the random order to a Stack...
        foreach (var card in deck)
        {
            CardPositionJitter(card.Value, DealPile.Count);
            DealPile.Push(card.Value);
        }

        // Make sure there are no references to these cards anywhere else.
        deck.Clear();

        Debug.Log("Deal cards to pla");
        // Deal out the players 
        for (int i = 0; i < this.gameOptions.NumberOfCardsToDeal; i++)
        {
            for (int j = 0; j < numOfPlayers; j++)
            {
                // The circular list allows us to start dealing from the "first" position
                players.Current().AddCard(DealPile.Pop());
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

    private void FirstPlay(Player player)
    {
        var firstCard = DiscardPile.Peek();
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
                PutCardBackInDeckInRandomPoisiton();
                break;
            default:
                break;
        }
    }

    private int BuildComputerPlayers(int totalPlayer)
    {
        for (int i = 0; i < this.gameOptions.ComputerPlayers; i++)
        {
            var ply = PlaceInCircle(transform, computerPlayer, totalPlayer++ - 1, numOfPlayers, 0.5f);
            string s = $"Computer {i + 1}";
            ply.CurrentGame = this;
            ply.SetName(s);
            ply.name = s;
            ply.DimmableCardObject = dimmableCardPrefab;
            this.players.Add(ply);
        }

        return totalPlayer;
    }

    private int BuildHumanPlayers(int totalPlayer)
    {
        for (int i = 0; i < this.gameOptions.HumanPlayers; i++)
        {
            var ply = PlaceInCircle(transform, player, totalPlayer++ - 1, numOfPlayers, 0.5f);
            var s = $"Human Player";
            ply.CurrentGame = this;
            ply.SetName(s);
            ply.name = s;
            this.players.Add(ply);
            HumanPlayer = ply;
        }

        return totalPlayer;
    }

    private void FixupCardsPerColor()
    {
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

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// The game options that control how the game behaves
    /// </summary>
    private static T PlaceInCircle<T>(Transform centerOfScreen, T prefab, int itemNumber, int totalObjects, float radius) where T : MonoBehaviour
    {
        float angle = itemNumber * Mathf.PI * 2 / totalObjects;
        T player = default(T);
        // TODO Properly place the players at the correct locations.
        if (itemNumber == 0)
        {
            player = Instantiate(prefab, new Vector3(0, -5), Quaternion.identity, centerOfScreen);
        }
        else if (itemNumber == 1)
        {
            player = Instantiate(prefab, new Vector3(9, 0), Quaternion.identity, centerOfScreen);
        }
        else if (itemNumber == 2)
        {
            player = Instantiate(prefab, new Vector3(0, 5), Quaternion.identity, centerOfScreen);
        }
        else if (itemNumber == 3)
        {
            player = Instantiate(prefab, new Vector3(-9, 0), Quaternion.identity, centerOfScreen);
        }
        player.transform.eulerAngles = Vector3.forward * Mathf.Rad2Deg * angle;

        return player;
    }

    /// <summary>
    /// The pile where the cards are dealt from
    /// </summary>
    public Stack<Card> DealPile { get; set; } = new Stack<Card>();

    /// <summary>
    /// THe pile where the cards are discarded to
    /// </summary>
    public Stack<Card> DiscardPile { get; set; } = new Stack<Card>();

    /// <summary>
    /// Pulls a card from the deal pile and will automatically swap the discard pile if needed
    /// </summary>
    /// <returns></returns>
    public Card TakeFromDealPile()
    {
        if (this.DealPile.Count == 0)
        {
            // Since we've flipped the items over we would visually expect the deck to be flipped each time.
            while (DiscardPile.Count > 0)
            {
                var item = DiscardPile.Pop();
                item.FlipCardOver();
                item.transform.SetParent(dealDeck.transform, false);
                item.transform.SetPositionAndRotation(dealDeck.transform.position, Quaternion.identity);
                CardPositionJitter(item, DealPile.Count);
                DealPile.Push(item);
            }
        }
        return this.DealPile.Pop();
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
        // Some special cases where we don't push the card to the pile since it's a 
        if (card.Action == GameAction.DrawAndPlayOnce || card.Action == GameAction.DrawAndSkip)
        {
            return card.Action;
        }

        // If we have a card that will play we need to continue on
        if (dontCheckEquals || this.DiscardPile.Peek().CanPlay(card))
        {
            // Take the card that is in play and put it on top.
            this.DiscardPile.Push(card);
            if (flipCard)
            {
                card.FlipCardOver();
            }
            card.transform.SetParent(discardDeck.transform);
            card.transform.SetPositionAndRotation(discardDeck.transform.position, Quaternion.identity);
            CardPositionJitter(card, DiscardPile.Count);
            return card.Action;
        }

        return GameAction.NextPlayer;
    }

    /// <summary>
    /// Applies a random XY translation and a random rotation
    /// </summary>
    /// <param name="card"></param>
    /// <param name="count"></param>
    private void CardPositionJitter(Card card, float count)
    {
        var v3 = card.transform.position;
        rand.NextFloat(-maxJitterTranslation, maxJitterTranslation);
        card.transform.SetPositionAndRotation(new Vector3(v3.x + rand.NextFloat(0.02f, maxJitterTranslation), v3.y + rand.NextFloat(0.02f, maxJitterTranslation), (v3.z + maxStackDepth) - (count + 1) * cardStackZOrderOffset), Quaternion.identity);
        card.transform.eulerAngles += Vector3.forward * rand.NextFloat(-maxJitterRotation, maxJitterRotation);
    }

    /// <summary>
    /// Performs the primary game mechanic of checking the cards and the player that is performing the action.
    /// </summary>
    /// <param name="c"></param>
    /// <param name="player"></param>
    public async void GameLoop(Card c, Player player)
    {

        if (c != Card.Empty && player == players.Current() && !stopGame)
        {
            PerformGameAction(c, false);
        }

        if (player.CheckWin())
        {
            AsyncOperationHandle<GameObject> winnerPrefabLoad = Addressables.LoadAssetAsync<GameObject>("Assets/Prefabs/WinBanner.prefab");
            winnerBanner = await winnerPrefabLoad.Task;

            var textMeshProObjects = winnerBanner.GetComponentsInChildren<TextMeshPro>();
            foreach (var item in textMeshProObjects)
            {
                item.text = $"{player.name} WINS!";

            }
            Instantiate(winnerBanner, this.transform);
            stopGame = true;
        } else
        {
            // Un dim the computer player so it gives us an indication that they are playing
            var compPlayer = players.Next();
            if (compPlayer is ComputerPlayer)
            {
                var playerRef = compPlayer as ComputerPlayer;
                playerRef.DimCards(false);
            }
            // Add a bogus delay so the game seems like somoene is playing with us.
            Invoke("CheckAndExecuteComputerPlayerAction", 3.0f);
        }
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
                var card = playerRef.PlayCard(TopCardOnDiscard, false);
                if (card == null)
                {
                    playerRef.AddCard(TakeFromDealPile());
                    GameLoop(Card.Empty, playerRef);
                }
                else
                {
                    GameLoop(card, playerRef);
                    if (card.Color == Card.CardColor.Wild)
                    {
                        card.SetProps(card.Value, card.WildColor);
                    }
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
    /// Puts the card back in a pseudo random spot that is at least 2n numbers of players into the middle up to n number of players from the bottom. 
    /// </summary>
    private void PutCardBackInDeckInRandomPoisiton()
    {
        // Since we want to preserve the order of the deck we need to pop the required number of cards off the pile
        var unshift = (new System.Random()).Next(numOfPlayers * (new System.Random()).Next(2, 4), DealPile.Count - numOfPlayers);
        var cards = new Stack<Card>();
        // First take the cards off the top of the stack and put them on another
        for (int i = 0; i < unshift; i++)
        {
            cards.Push(DealPile.Pop());
        }
        // Push the offending card from the discard pile on the new "cut" stack.
        DealPile.Push(DiscardPile.Pop());
        // Push all cards back onto the deal pile
        for (int i = 0; i < unshift; i++)
        {
            DealPile.Push(cards.Pop());
        }
        // Take the next card from the top of the deal pile and put it on the discard pile.
        DiscardPile.Push(TakeFromDealPile());
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
