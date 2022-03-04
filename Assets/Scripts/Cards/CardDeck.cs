using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Common;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDeck : Deck<Card>
{
    private float DoubleClickSpeed = 0.5f;
    private volatile float startTime = 0;
    private volatile bool clicked = false;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if ((Time.time - startTime) > DoubleClickSpeed)
        {
            clicked = false;
        }
    }

    void OnMouseUp()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;

        if (((Time.time - startTime) <= DoubleClickSpeed) && this.name == "DealDeck")
        {
            if (clicked)
            {
                // Clickable is parented by the "GameBoard" object which is an instance of Game
                var game = this.GetComponentInParent<Game>();
                // If the player clicked on a card let's do the work to make the play
                game.PullCardFromDealDeck();
                clicked = false;
            }
        }
        clicked = true;
        startTime = Time.time;
    }
}
