using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class Card : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private const string spriteRoot = "Assets/Card Sprites/Uno_";
    private const string spriteBack = "Assets/Card Sprites/Uno_Back.png";

    private const string fileExtension = ".png";
    private const string CardBack = "back";
    private const string CardFront = "front";

    private Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    private bool showCardFront;

    private float? _width = null;
    public float Width
    {
        get
        {
            _width = _width ?? spriteRenderer.bounds.size.x;
            return (float)_width.Value;
        }
    }
    private void LoadCardBackSpriteToClass(AsyncOperationHandle<Sprite[]> obj)
    {
        sprites[CardBack] = obj.Result.First();
        spriteRenderer.sprite = sprites[CardBack];
    }

    private void LoadCardFrontSpriteToClass(AsyncOperationHandle<Sprite[]> obj)
    {
        sprites[CardFront] = obj.Result.First();
    }

    public void Dim(bool dim)
    {
        var dimColor = dim ? UnityEngine.Color.gray : UnityEngine.Color.white;
        spriteRenderer.color = dimColor;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Unhide()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Generate the remainder of the file name to generate the sprites
    /// </summary>
    /// <returns></returns>
    private string GenerateFileName()
    {
        string value = string.Empty;

        string color;
        switch (Color)
        {
            case Card.CardColor.Red:
                color = "R";
                break;
            case Card.CardColor.Green:
                color = "G";
                break;
            case Card.CardColor.Blue:
                color = "B";
                break;
            case Card.CardColor.Yellow:
                color = "Y";
                break;
            case Card.CardColor.Wild:
                color = "Wild";
                break;
            default:
                color = "R";
                break;
        }

        switch (Value)
        {
            case CardValue.Zero:
            case CardValue.One:
            case CardValue.Two:
            case CardValue.Three:
            case CardValue.Four:
            case CardValue.Five:
            case CardValue.Six:
            case CardValue.Seven:
            case CardValue.Eight:
            case CardValue.Nine:
                value = ((int)Value).ToString();
                break;
            case CardValue.DrawTwo:
                value = "D2";
                break;
            case CardValue.Skip:
                value = "SK";
                break;
            case CardValue.Reverse:
                value = "RV";
                break;
            case CardValue.DrawFour:
                value = "DrawFour";
                break;
            case CardValue.Wild:
                value = "Wild";
                break;
            case CardValue.Empty:
            case CardValue.DrawAndGoAgainOnce:
            case CardValue.DrawAndSkipTurn:
            default:
                break;
        }

        string valueAppend = string.IsNullOrEmpty(value) ? "" : $"_{ value}";
        return $"{color}{valueAppend}";
    }
    // Update is called once per frame
    void Update()
    {
        ShowCard();
    }

    /// <summary>
    /// Shows the front or back of the card depending on the flip state
    /// </summary>
    private void ShowCard()
    {
        if (sprites.ContainsKey(CardFront) && sprites.ContainsKey(CardBack))
        {
            if (showCardFront)
            {
                spriteRenderer.sprite = sprites[CardFront];
            }
            else
            {
                spriteRenderer.sprite = sprites[CardBack];
            }
        }
    }

    public static Card Empty = null;
    public static Card DrawOnce = null;
    public static Card DrawAndSkip = null;

    private bool customAction = false;
    private GameAction _Action = GameAction.NextPlayer;

    /// <summary>
    /// The color of the card to show
    /// </summary>
    public enum CardColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        Wild,
        Special,
    }

    /// <summary>
    /// The value of the card to show
    /// </summary>
    public enum CardValue
    {
        Zero,
        One,
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        DrawTwo,
        Skip,
        Reverse,
        DrawFour,
        Wild,
        Empty,
        DrawAndGoAgainOnce,
        DrawAndSkipTurn
    }

    /// <summary>
    /// Flips the card
    /// </summary>
    public void FlipCardOver()
    {
        showCardFront = !showCardFront;
    }
    /// <summary>
    /// Gets the value of the custom draw card amount
    /// </summary>
    public int CustomDrawAmount { get; private set; }

    /// <summary>
    /// Gets the card's value
    /// </summary>
    public CardValue Value { get; private set; }

    /// <summary>
    /// Gets the card's color
    /// </summary>
    public CardColor Color { get; private set; }

    /// <summary>
    /// Gets the color to be played for the wild card
    /// </summary>
    public CardColor WildColor { get; private set; }
   
    /// <summary>
    /// Gets the game action this card will perform
    /// </summary>
    public GameAction Action
    {
        get
        {
            if (customAction)
            {
                return _Action;
            }

            switch (this.Value)
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
        private set
        {
            _Action = value;
        }
    }

    /// <summary>
    /// Gets the message for the cusotom card
    /// </summary>
    public string CardMessage { get; private set; }

    /// <summary>
    /// Sets the card's value and color as well as sets the sprites
    /// </summary>
    /// <param name="value">The value this card should be</param>
    /// <param name="color">The color this card should be</param>
    public void SetProps(CardValue value, CardColor color)
    {
        this.Color = color;
        this.Value = value;
        spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        AsyncOperationHandle<Sprite[]> spriteHandleBack = Addressables.LoadAssetAsync<Sprite[]>(spriteBack);
        spriteHandleBack.Completed += LoadCardBackSpriteToClass;
        AsyncOperationHandle<Sprite[]> spriteHandle = Addressables.LoadAssetAsync<Sprite[]>($"{spriteRoot}{GenerateFileName()}{fileExtension}");
        spriteHandle.Completed += LoadCardFrontSpriteToClass;
    }

    /// <summary>
    /// Creates a custom action card
    /// </summary>
    /// <param name="value">The value of the card</param>
    /// <param name="color">The color of the card</param>
    /// <param name="action">The action of the custom card</param>
    /// <param name="customMessage">Add a message to this card.</param>
    /// <param name="drawNumber">If using <see cref="GameAction.DrawCustom"/> you must specify a number here.</param>
    public void SetProps(CardValue value, CardColor color, GameAction action, string customMessage, int drawNumber)
    {
        this.Color = color;
        this.WildColor = color;
        this.Value = value;
        this.Action = action;
        this.CardMessage = customMessage;
        if (action == GameAction.DrawCustom)
        {
            CustomDrawAmount = drawNumber > 0 ? drawNumber : throw new ArgumentOutOfRangeException("If you specify a custom draw card you must also specify a non-zerp number.");
        }
        customAction = true;
    }

    /// <summary>
    /// Set the wild color of the card.
    /// </summary>
    /// <param name="color">The color the wild card should be on the next turn</param>
    public void SetWildColor(CardColor color)
    {
        if (Color == CardColor.Wild)
        {
            WildColor = color;
        }
    }
    public override string ToString()
    {
        return $"{this.Color} {this.Value}";
    }

    /// <summary>
    /// Checks to see if the card can be played against another card.  
    /// </summary>
    /// <remarks>
    /// Smple rules are in effect here. We check to see if the number value is the same or if the color value is the same. We also check to see if the card we're playing is wild or the card we're playing aginst is wild.
    /// </remarks>
    /// <param name="other">The other card to check against.</param>
    /// <returns></returns>
    public bool CanPlay(Card other)
    {
        return this.Color.Equals(other.Color) | this.Value.Equals(other.Value) | other.Color.Equals(CardColor.Wild) | this.Color.Equals(CardColor.Wild);
    }
}
