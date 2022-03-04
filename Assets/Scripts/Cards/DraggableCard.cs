using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableCard : Card
{
    // The amount of seconds for the double click speed to be registered
    private float DoubleClickSpeed = 0.5f;

    // How many seconds before we bring the card up to drag
    private float dragDelay = 0.75f;

    // Start of double click time
    private volatile float startTime = 0;

    // Start of time for drag
    private volatile float dragTime = 0;

    // Clicked flag for double click
    private volatile bool clicked = false; // Clicked flag for double click

    // Amount of pixels before drag starts
    private float dragStartThreshold = 5.0f;

    // Mouse position at first Mouse Down event
    private Vector3 mousePosition;

    protected override void Start()
    {
        base.Start();
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();

        if ((Time.time - startTime) > DoubleClickSpeed)
        {
            clicked = false;
        }
    }

    void OnMouseDrag()
    {
        // This is a guard t make sure we don't do something while a UI component is active
        if (EventSystem.current.IsPointerOverGameObject()) return;
        var dragPosition = Input.mousePosition;
        if ((Time.time - dragTime) >= dragDelay || Mathf.Abs((mousePosition - dragPosition).magnitude) > dragStartThreshold)
        {
            var cardPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            cardPosition.z = 0;
            this.transform.position = cardPosition;
            Overlay(!cardIsDimmed && MousePointerIsOverDiscardDeck(cardPosition));
        }
    }

    void OnMouseDown()
    {
        // This is a guard t make sure we don't do something while a UI component is active
        if (EventSystem.current.IsPointerOverGameObject()) return;
        dragTime = Time.time;
        mousePosition = Input.mousePosition;
    }


    void OnMouseUp()
    {
        // This is a guard t make sure we don't do something while a UI component is active
        if (EventSystem.current.IsPointerOverGameObject()) return;

        // When we've let up on the mouse we need to either allow the dragging card to return to the deck
        // Or if we've placed a card or double clicked we need to attempt to play it
        var cardPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        if (MousePointerIsOverDiscardDeck(cardPosition) || ((Time.time - startTime) <= DoubleClickSpeed && clicked))
        {
            PlayThisCard();
        }
        else
        {
            // Reset the card and variables
            AnimateToPosition(originalPosition, originalRotation);
            dragTime = 0;
            clicked = true;
            startTime = Time.time;
        }

        // Always remove the overlay
        Overlay(false);

    }

    void PlayThisCard()
    {
        // Clickable is parented by the "GameBoard" object which is an instance of Game
        var game = this.GetComponentInParent<Game>();
        // If the player clicked on a card let's do the work to make the play
        game.PlayClickedCard(this);
        clicked = false;
    }

    bool MousePointerIsOverDiscardDeck(Vector3 pointerPosition)
    {
        // Discard Deck box collider is Vector2(1.2, 0) and is 2w 3h
        Vector3 cardSize = new Vector3(1f, 1.5f, 0);
        Vector3 topLeft = discardDeckPosition - cardSize;
        Rect discardbox = new Rect(topLeft.x, topLeft.y, 2, 3);
        return discardbox.Contains(new Vector2(pointerPosition.x, pointerPosition.y));
    }
}
