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
    private const float cardStackZOrderOffset = 0.01f;

    private readonly CircularList<LocalPlayerBase<Player>, Player> playerRotation = new CircularList<LocalPlayerBase<Player>, Player>();
    private readonly List<Card.CardValue> cardValuesArray = new List<Card.CardValue>((Card.CardValue[])Enum.GetValues(typeof(Card.CardValue)));
    private readonly Card.CardColor[] cardColorArray = new Card.CardColor[] { Card.CardColor.Red, Card.CardColor.Green, Card.CardColor.Blue, Card.CardColor.Yellow };
    private readonly List<AudioClip> cardPlaySounds = new List<AudioClip>();
    private readonly IDictionary<LocalPlayerBase<Player>, int> playerScore = new Dictionary<LocalPlayerBase<Player>, int>();
    
    private GameOptions gameOptions;

    private Toggle playerToggle;
    private Button unoButton;
    private Button challengeButton;
    private Button leaveButton;

    private string lastUpdateGuid = string.Empty;

    private int numOfPlayers = 4;
    private int numberOfDecks = 1;

    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();

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

    public LocalPlayerBase<Player> CurrentPlayer => playerRotation.Current();

    /// <summary>
    /// Gets the singular local player
    /// </summary>
    public LocalPlayer LocalPlayerReference { get; private set; }

    private bool stopGame;

    private bool gameStarted = false;

    public float cardDealSpeed;

    // Start is called before the first frame update
    async void Start()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            await InitializeAssetsAndPlayers();
            StartGame();
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
    }

}
