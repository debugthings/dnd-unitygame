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

            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);
            if (hit.collider != null)
            {
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

                    var cardObjectHitTest = hit.collider.gameObject.GetComponent<Card>();
                    if (cardObjectHitTest is Card)
                    {
                        var cardToCheck = (cardObjectHitTest as Card);
                        if (cardToCheck != null)
                        {
                            if (cardToCheck.name.Equals("DealDeck", StringComparison.InvariantCultureIgnoreCase))
                            {
                                // If we're in play and the player decides to draw, either the card will be played or added to the hand.
                                // When we're in networked mode we'll need to take into account that anyone can double click the deck so we'll need to make sure
                                // the click originated from the player.
                                var player = game.CurrentPlayer;
                                var cardToPlay = game.TakeFromDealPile();
                                if (cardToPlay.CanPlay(game.TopCardOnDiscard))
                                {
                                    cardToPlay.FlipCardOver();
                                    game.GameLoop(cardToPlay, player);

                                } else
                                {
                                    player.AddCard(cardToPlay);
                                }

                                game.HumanPlayer.FixupCardPositions();

                            }
                            else if (cardToCheck.gameObject.name.Equals("DiscardDeck", StringComparison.InvariantCultureIgnoreCase))
                            {
                                // Do nothing but let's keep this here so we know that it's doing nothing..
                            }
                            else
                            {
                                var player = cardObjectHitTest.GetComponentInParent<Player>();
                                player.PlayCard(cardObjectHitTest, false);
                                game.GameLoop(cardObjectHitTest, player);
                                game.HumanPlayer.FixupCardPositions();
                            }
                        }

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

    public void OnPointerDown(PointerEventData eventData)
    {
        throw new System.NotImplementedException();
    }
}
