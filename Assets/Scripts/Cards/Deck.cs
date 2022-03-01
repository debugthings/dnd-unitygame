using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common;
using UnityEngine;


public class Deck<T> : MonoBehaviour, IEnumerable<T> where T : Card
{
    private Stack<T> deck = new Stack<T>();
    private const float cardStackZOrderOffset = 0.01f;
    private const float maxJitterTranslation = 0.06f;
    private const float maxJitterRotation = 2.0f;
    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();
    public float MaxStackDepth { get; set; }
    public uint RandomSeed = uint.MaxValue; // value to be set by server or by rand

    void Awake()
    {

    }
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SetRandomSeed(uint seed)
    {
        rand.InitState(seed);
    }

    public int Count => deck.Count;

    public bool StackGrowsDown { get; set; } = true;

    public float StackZOrderDirection => this.StackGrowsDown ? cardStackZOrderOffset : -cardStackZOrderOffset;
    public void AddCardToDeck(T c, bool showCardFace)
    {
        if (c != Card.Empty)
        {
            deck.Push(c);
            c.SetCardFaceUp(showCardFace);
            c.transform.SetParent(this.transform);
            CardPositionJitter(c, Count + 1);
        }
    }

    public T FindCardAndTakeIt(int cardRandomId)
    {
        var stack = new Stack<Card>();
        Card c = deck.Pop();
        while (c.CardRandom != cardRandomId)
        {
            stack.Push(c);
            c = deck.Pop();
        }
        foreach (var item in stack)
        {
            deck.Push(item as T);
        }
        return c as T;
    }

    public T FindCardAndTakeIt(Card.CardColor color, Card.CardValue value)
    {
        var stack = new Stack<Card>();
        Card c = deck.Pop();
        while (c.Color != color || c.Value != value)
        {
            stack.Push(c);
            c = deck.Pop();
        }
        foreach (var item in stack)
        {
            deck.Push(item as T);
        }
        return c as T;
    }
    private void CardPositionJitter(Card card, int count)
    {
        var v3 = this.transform.position;
        card.transform.SetPositionAndRotation(new Vector3(v3.x + rand.NextFloat(-maxJitterTranslation, maxJitterTranslation), v3.y + rand.NextFloat(-maxJitterTranslation, maxJitterTranslation), (v3.z + MaxStackDepth) - (count + 1) * StackZOrderDirection), Quaternion.identity);
        card.transform.eulerAngles += Vector3.forward * rand.NextFloat(-maxJitterRotation, maxJitterRotation);
    }

    public T TakeTopCard()
    {
        T cardToTake = deck.Pop();
        CustomLogger.Log(cardToTake.ToString());
        return cardToTake;
    }

    public T PeekTopCard()
    {
        T cardToTake = deck.Peek();
        CustomLogger.Log(cardToTake.ToString());
        return cardToTake;
    }

    public T PeekNthCard(int howMany)
    {
        if (howMany > 0)
        {
            var tempDeck = new Stack<T>();

            for (int i = 0; i < howMany; i++)
            {
                tempDeck.Push(deck.Pop());
            }
            
            T cardToTake = tempDeck.Peek();

            for (int i = 0; i < howMany; i++)
            {
                deck.Push(tempDeck.Pop());
            }
            CustomLogger.Log(cardToTake.ToString());
            return cardToTake;
        }
        return default(T);

    }

    public void Shuffle()
    {
        // https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
        var listToShuffle = TakeAllCards().ToArray();
        int lengthOfArray = listToShuffle.Length;

        for (int i = 0; i < (lengthOfArray - 1); i++)
        {
            int r = i + rand.NextInt(lengthOfArray - i);
            T t = listToShuffle[r];
            listToShuffle[r] = listToShuffle[i];
            listToShuffle[i] = t;
        }
        deck = new Stack<T>(listToShuffle);
        foreach (var item in deck)
        {
            CustomLogger.Log($"Rand: {item.CardRandom} Card: {item}");

        }
        RepositionAllCards();
    }

    private void RepositionAllCards()
    {
        int counter = 1;
        foreach (var item in deck)
        {
            CardPositionJitter(item, counter++);
        }
    }

    private void SetParentOfAllCards()
    {
        foreach (var c in deck)
        {
            c.transform.SetParent(this.transform);
        }
    }

    private void AllCardsFaceDown()
    {
        foreach (var item in deck)
        {
            item.SetCardFaceUp(false);
        }
    }

    public void AddAllCards(IEnumerable<T> cards)
    {
        foreach (var item in cards)
        {
            deck.Push(item);
        }
    }

    public IEnumerable<T> TakeAllCards()
    {
        var cardsToReturn = deck.ToArray();
        deck.Clear();
        return cardsToReturn;
    }

    public void SwapCardsFromOtherDeck(Deck<T> otherDeck)
    {
        this.StackGrowsDown = !this.StackGrowsDown;
        otherDeck.StackGrowsDown = !otherDeck.StackGrowsDown;
        var reverseCards = otherDeck.TakeAllCards().Reverse();
        deck = new Stack<T>(reverseCards);
        SetParentOfAllCards();
        AllCardsFaceDown();
        RepositionAllCards();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)deck).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)deck).GetEnumerator();
    }

    /// <summary>
    /// Puts the card back in a pseudo random spot that is at least 2n numbers of players into the middle up to n number of players from the bottom. 
    /// </summary>
    public void PutCardBackInDeckInRandomPoisiton(T c, int distanceFromTop, int distanceFromBottom)
    {
        Debug.Log($"Putting card {c} back in random position.");
        // Since we want to preserve the order of the deck we need to pop the required number of cards off the pile
        var unshift = rand.NextInt(distanceFromTop, distanceFromBottom);
        var cards = new Stack<T>();
        // First take the cards off the top of the stack and put them on another
        for (int i = 0; i < unshift; i++)
        {
            cards.Push(TakeTopCard());
        }
        // Push the offending card from the discard pile on the new "cut" stack.
        AddCardToDeck(c, false);
        // Push all cards back onto the deal pile
        for (int i = 0; i < unshift; i++)
        {
            AddCardToDeck(cards.Pop(), false);
        }
    }

    public void ClearDeck()
    {
        foreach (var item in deck)
        {
            Destroy(item.gameObject);
        }
        deck.Clear();
    }
}
