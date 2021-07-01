using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class Card : MonoBehaviour, IComparable<Card>
{
    private SpriteRenderer spriteRenderer;
    private const string spriteRoot = "Assets/Card Sprites/Uno_";
    private const string spriteBack = "Assets/Card Sprites/Uno_Back.png";

    private const string fileExtension = ".png";
    private const string CardBack = "back";
    private const string CardFront = "front";

    private static object lockObject = new object();
    private static GameObject drawCardGameObject;
    private static GameObject emptyCardGameObject;
    private static GameObject firstplayCardGameObject;
    

    private Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public bool IsInFlight { get; set; } = false;

    private bool showCarFace;

    private float? _width = null;
    public float Width
    {
        get
        {
            _width = _width ?? spriteRenderer.bounds.size.x;
            return (float)_width.Value;
        }
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
    /// Gets the message for the cusotom card
    /// </summary>
    public string CardMessage { get; private set; }

    public string WildColorNetworkMessage { get; set; }

    public int CardRandom { get; set; }

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
        if (drawCardGameObject == null)
        {
            lock (lockObject)
            {
                if (drawCardGameObject == null)
                {
                    drawCardGameObject = new GameObject("DrawAndSkip", typeof(Card));
                    DrawAndSkip = drawCardGameObject.GetComponent<Card>();
                    DrawAndSkip.SetProps(int.MinValue,CardValue.DrawAndSkipTurn, CardColor.Special);
                }
            }
        }

        if (emptyCardGameObject == null)
        {
            lock (lockObject)
            {
                if (emptyCardGameObject == null)
                {
                    emptyCardGameObject = new GameObject("Empty", typeof(Card));
                    Empty = emptyCardGameObject.GetComponent<Card>();
                    Empty.SetProps(int.MinValue + 1, CardValue.Empty, CardColor.Special);
                }
            }
        }

        if (firstplayCardGameObject == null)
        {
            lock (lockObject)
            {
                if (emptyCardGameObject == null)
                {
                    emptyCardGameObject = new GameObject("FirstPlay", typeof(Card));
                    Empty = emptyCardGameObject.GetComponent<Card>();
                    Empty.SetProps(int.MinValue + 1, CardValue.FirstPlay, CardColor.Special);
                }
            }
        }
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
            if (showCarFace)
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
    public static Card FirstPlay = null;
    public static Card DrawAndSkip = null;

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
        DrawAndSkipTurn,
        Custom,
        FirstPlay
    }

    /// <summary>
    /// Flips the card
    /// </summary>
    public void FlipCardOver()
    {
        showCarFace = !showCarFace;
    }

    public void SetCardFaceUp(bool faceUp)
    {
        showCarFace = faceUp;
    }


    /// <summary>
    /// Sets the card's value and color as well as sets the sprites
    /// </summary>
    /// <param name="cardRandom">The random value of the card set by the game engine</param>
    /// <param name="value">The value this card should be</param>
    /// <param name="color">The color this card should be</param>
    public void SetProps(int cardRandom, CardValue value, CardColor color)
    {
        this.CardRandom = cardRandom;
        this.Color = color;
        this.Value = value;
        // Debug.Log($"SetProps CardRandom = {CardRandom}\tColor = {Color}\tValue = {Value}");
        if (this.Color != CardColor.Special)
        {
            spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            AsyncOperationHandle<Sprite[]> spriteHandleBack = Addressables.LoadAssetAsync<Sprite[]>(spriteBack);
            spriteHandleBack.Completed += LoadCardBackSpriteToClass;
            AsyncOperationHandle<Sprite[]> spriteHandle = Addressables.LoadAssetAsync<Sprite[]>($"{spriteRoot}{GenerateFileName()}{fileExtension}");
            spriteHandle.Completed += LoadCardFrontSpriteToClass;
            // Debug.Log($"SetProps CardFace = {spriteRoot}{GenerateFileName()}{fileExtension}");
        }
    }

    /// <summary>
    /// Sets the card's value and color as well as sets the sprites
    /// </summary>
    /// <param name="cardRandom">The random value of the card set by the game engine</param>
    /// <param name="value">The value this card should be</param>
    /// <param name="color">The color this card should be</param>
    public void SetProps(int cardRandom, CardValue value, string color)
    {
        var newColor = (CardColor)Enum.Parse(typeof(CardColor), color);
        SetProps(cardRandom, value, newColor);
    }

    /// <summary>
    /// Set the wild color of the card.
    /// </summary>
    /// <param name="color">The color the wild card should be on the next turn</param>
    public void SetWildColor(CardColor color)
    {
        if (Color == CardColor.Wild)
        {
            // Debug.Log($"Set wild color value of {this} {CardRandom} to {color}");
            WildColor = color;
        }
    }

    public void SetWildColor(string color)
    {
        if (Color == CardColor.Wild)
        {
            var newColor = (CardColor)Enum.Parse(typeof(CardColor), color);
            SetWildColor(newColor);
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
        return this.Color.Equals(other?.Color) | this.Value.Equals(other?.Value) | this.Color.Equals(Card.CardColor.Wild) | other.Color.Equals(Card.CardColor.Wild);
    }

    /// <summary>
    /// Compare the color and card value to determine hand order.
    /// </summary>
    /// <param name="other">The card to compare against</param>
    /// <returns></returns>
    public int CompareTo(Card other)
    {
        int comparedValue = Value - other.Value;
        int comparedColor = Color - other.Color;
        if (comparedColor != 0)
        {
            return comparedColor;
        }
        return comparedValue;
    }
}
