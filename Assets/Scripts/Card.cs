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

    public bool showCardFront;

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
    public enum CardColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        Wild,
        Special,
    }

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

    public void FlipCardOver()
    {
        showCardFront = !showCardFront;
    }
    public int CustomDrawAmount { get; private set; }
    public CardValue Value { get; private set; }
    public CardColor Color { get; private set; }
    public CardColor WildColor { get; private set; }
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
    public string CardMessage { get; private set; }
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

    public void WriteCard(bool useWildColor)
    {
        var fg = Console.ForegroundColor;
        if (useWildColor && Color == CardColor.Wild)
        {
            Color = WildColor;
        }
        switch (Color)
        {
            case Card.CardColor.Red:
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case Card.CardColor.Green:
                Console.ForegroundColor = ConsoleColor.Green;
                break;
            case Card.CardColor.Blue:
                Console.ForegroundColor = ConsoleColor.Blue;
                break;
            case Card.CardColor.Yellow:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case Card.CardColor.Wild:
                Console.ForegroundColor = ConsoleColor.Magenta;
                break;
            default:
                break;
        }
        Console.Write($"{this}");
        Console.ForegroundColor = fg;
    }

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
    public bool CanPlay(Card other)
    {
        return this.Color.Equals(other.Color) | this.Value.Equals(other.Value) | other.Color.Equals(CardColor.Wild) | this.Color.Equals(CardColor.Wild);
    }
}
