using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Clickable : MonoBehaviour
{
    private float DoubleClickSpeed = 0.5f;
    private GameObject lastClicked = null;
    private volatile float startTime = -1;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);

            // We will raycast the board and look for either the player's cards
            // or we'll find the "deal pile".
            // All cards have a collider which enables this behavior
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);
            if (hit.collider != null)
            {
                // Simple doubleclick behavior.
                // Check to see if we've clicked the last thing again
                // if not we reset.
                if (lastClicked != null && lastClicked != hit.collider.gameObject)
                {
                    ResetClick();
                }
                else if (startTime == -1)
                {
                    startTime = Time.time;
                    this.lastClicked = hit.collider.gameObject;
                }
                else if ((Time.time - startTime) < DoubleClickSpeed)
                {
                    // Clickable is parented by the "GameBoard" object which is an instance of Game
                    var game = gameObject.GetComponentInParent<Game>();
                    var cardObject = hit.collider.gameObject.GetComponent<Card>();

                    if (cardObject is Card)
                    {
                        // If the player clicked on a card let's do the work to make the play
                        game.PlayClickedCard(cardObject);
                    }
                    ResetClick();
                }
            }
        }
        if ((Time.time - startTime) > DoubleClickSpeed)
        {
            ResetClick();
        }
    }

    private void ResetClick()
    {
        startTime = -1.0f;
        this.lastClicked = null;
    }
}
