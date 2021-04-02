using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts
{
    public class GameOptions
    {
        /// <summary>
        /// The number of human players for this game.
        /// </summary>
        /// <value>Default is 1</value>
        public int HumanPlayers { get; set; } = 1;

        /// <summary>
        /// The number of computer players for this game. 
        /// </summary>
        /// <value>Default is 3</value>
        public int ComputerPlayers { get; set; } = 3;

        /// <summary>
        /// The maximum number of decks for this game.
        /// </summary>
        /// <value>Default is 5</value>
        public int MaxDecks { get; set; } = 2;

        /// <summary>
        /// The minimum number of decks for this game.
        /// </summary>
        /// <value>Default is 1</value>
        public int BaseNumberOfDecks { get; set; } = 1;

        /// <summary>
        /// How many cards to deal for the initial hand.
        /// </summary>
        /// <value>Default is 5</value>
        public int NumberOfCardsToDeal { get; set; } = 5;

        /// <summary>
        /// How many players per deck.
        /// </summary>
        /// <remarks>
        /// What we want to do here is have a game where the number of decks is in some multiple of the number of players
        /// However in the event that we have n-(n/2) > 2 players that would trigger a new deck, we should opt to increase the number of decks.
        /// For example, if we have 4 players per deck and the game has 7 players, we should add another deck to be sure.
        /// </remarks>
        /// <value>Default is 4</value>
        public int PlayersPerDeck { get; set; } = 6;

        /// <summary>
        /// The maximum number of cards allowed when <see cref="AllowStacking"/> is enabled.
        /// </summary>
        public int MaxStackCards { get; set; } = 3;

        /// <summary>
        /// Allow a player to "stack" cards for play.
        /// </summary>
        /// <remarks>
        /// <para>This is a sure fire way to make everyone hate you. What this value does is allows a player to stack or chain cards.</para>
        /// <para>For example:</para><para>A player has in their hand: Red Three, Green Three, Wild, Yellow Five, Blue Five. A player is presented with a Red Zero; in normal play they can only play any Red card, any color Zero, or a wild.</para><para>In a stacked game, however, you could chain the cards to play up to the <see cref="MaxStackCards"/> or unitl you'd reach "Uno". Use this at your own risk.</para> 
        /// </remarks>
        /// <value>Default is false.</value>
        public bool AllowStacking { get; set; }

        /// <summary>
        /// Sets if the player has to call "Uno" when they reach their last card. Will use the <see cref="UnoTimeoutForgiveness"/>
        /// </summary>
        /// <value>Defualt is true</value>
        public bool PlayerHasToCallUno { get; set; } = true;

        /// <summary>
        /// The amount of time to let the "Uno" player have before they are forced to draw two.
        /// </summary>
        /// <remarks>In normal play the person with one card left MUST call "Uno" before the next play so they do not incur a two card penalty. In online play we will have a button to click. If the next player goes before the button is clicked the current "Uno" player will be afforded a time window to click the button.</remarks>
        /// <value>Default is 5 seconds.</value>
        public TimeSpan UnoTimeoutForgiveness { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Most actions are simple, draw 2, draw 4, reverse, etc.
        /// </summary>
        public bool AllowCustomActionCards { get; set; }
    }
}
