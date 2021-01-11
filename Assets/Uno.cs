using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Uno : MonoBehaviour
{
    private string[] colors = new string[] { "R", "Y", "G", "B"}; // List all of the colors for each card
    private string[] colorSpecials = new string[] { "D2", "SK", "RV" }; // List all of the specials for each color
    private string[] singleSpecials = new string[] { "D4", "WL" }; // Special cards not tied to a color
    private string[] numbers = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" }; // Integers of the common cardsaa
    private int numberOfSingleSpecials = 4; // How many of each single special do we create
    private int numberOfColorRuns = 2; // How many times should we do a color run. (2 (runs) * 4 (colors) * 9 (integers)) + 8 (specials) + 4 (zeros) = 84 cards

    public List<string> Deck { get; set; }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
