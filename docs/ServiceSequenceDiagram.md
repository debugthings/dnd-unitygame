# Commands
To facilitate the sequences below we will have the following commands

## Host
Commands that the host will use to broadcast messages to each client.

  * SendCards - Can be 1 or more cards for the player to accept as part of their hand
  * SendAction - Will tell a player the last action played to update the game status (started, playing, skipped, won, score)
  * UpdatePlayers - Will send the correct number of cards for each player
  * SendDiscard - Will send the current discard along with the number of cards on the stack
  * SendDeal - Will send the current number of cards on the deal deck so the player will see a proper representation on their screen

## Client
Messages from the client to the host

  * SendCards - Can be 1 or more cards for the host to validate against the discard pile
  * DrawCard - Can be a special card used in the previous method to tell the host to give a card

# Lobby / Game Creation

The game creation will work on a model where the host tells the game service it's ready to host a game. The service will keep a reference to the host and list the game publicly or privately so any number of guests up to n can join.

```mermaid
sequenceDiagram
    participant h as Host (Game)
    participant s as Service (Azure)
    participant g as Guest 1..n (Game)
    h->>s: Create a game
    s-->>h: Your game id is A1
    loop Ping
        h->>s: Ping
        s-->>h: Ack
    end
    g->>s: Hello, I am Gn
    s-->>g: Hello G(1..n) here is a list of games
    g->>s: I want to join A1
    s->>h: G(1..n) wants to join A1
    h-->>s: Accept
    s-->>g: A1 Accepted
    g->>s: Ack for A1
    s->>h: Ack for G(1..n)
    loop Ping
        g->>s: Ping
        s-->>g: Ack
    end
    h->>s: Start Game A1
    s->>g: Game A1 Started
    g-->>s: Ack
    s-->>h: G(1..n) Ack'd Start
```

# Start Game Play Sequence

Once the players are connected the host will shuffle and deal the deck to the players. The lobby portion of the host will pass along the connection information to the Game scene.

```mermaid
sequenceDiagram
    participant h as Host (Game)
    participant s as Service (Azure)
    participant g as All Players (Game)
    h->>h: Create NetworkPlayer references
    h->>h: Create LocalPlayer reference
    h->>h: Shuffle Cards
    h->>h: Deal Cards to LocalPlayer references
    h->>h: Deal Cards to NetworkPlayer references
    h->>s: Send hand to Player
    s->>g: Here is your hand
    g-->>s: Ack Hand
    s-->>h: Guest Ack'd Hand
    h->>s: Send discard card to Guest
    s->>g: Send discard card to Guest
    g-->>s: Ack Discard
    s-->>h: Guest Ack'd Discard
    h->>s: Notify First Player
    s->>g: It's your turn
    g-->>s: Ack
    s-->>h: Player is Ready
    loop Wait for First Player
        h->>s: Ping
        s-->>h: Ack
    end
    loop Wait for Player
        g->>s: Ping
        s-->>g: Ack
    end
```

# Player Makes Simple Move

After the game has started we will continuously ping the server as a keep alive. We will also accept messages from the player. The game state is completely managed on the host so we need to be ready for any player to try and make a move at any time. This is why we need to have Send/Ack messages to guarantee no one gets out of sync. This is the sequence for a card leaving a player's hand with no other interactions.

```mermaid
sequenceDiagram
    participant h as Host (Game)
    participant s as Service (Azure)
    participant n as Active Player (Game)
    participant g as All Players (Game)
    loop Main Game Loop
        h->>s: Notify Player it's their turn
        s->>n: It's your turn
        activate n
        n->>s: Ack
        s->>h: Ack
        loop Wait for Player
            h->>s: Ping
            s->>h: Ack
        end
        loop Wait for Human
            n->>s: Ping
            s->>n: Ack
        end
        n->>s: Make play with valid card
        s->>h: Here is Player n's move
        h->>h: Validate move
        h->>s: Ack
        s->>n: Ack
        deactivate n
        h->>h: Play card against discard
        h->>h: Put card on discard pile
        rect rgba(0, 0, 255, .1)
        Note over h,g: Can be sent as one message
            loop Move is Valid
                par Discard
                    h->>s: Card X is on Discard
                    s->>g: Card X is on Discard
                    activate g
                    g->>s: Ack
                    deactivate g
                    s->>h: Player Ack'd
                and Update
                    h->>s: Player N has x cards left
                    s->>g: Player N has x cards left
                    activate g
                    g->>s: Ack
                    deactivate g
                    s->>h: Player Ack'd
                end
            end
        end
        h->>h: All Players Ack'd
        h->>h: Advance to next player
    end
```

# Player Makes Move that affects other players

This is a game with a winner after all. If the player plays something like a Draw 2 it will cause the other player adjacent to them to have to get two new cards. This is similar to when a card is dealt.

```mermaid
sequenceDiagram
    participant h as Host (Game)
    participant s as Service (Azure)
    participant g as Active Player (Game)
    participant n as Next Player (Game)
    participant a as All Players (Game)
    loop Main Game Loop
            h->>s: Notify Player it's their turn
            s->>g: It's your turn
            activate g
            g->>s: Ack
            s->>h: Ack
            loop Wait for Player
                h->>s: Ping
                s->>h: Ack
            end
            loop Wait for Human
                g->>s: Ping
                s->>g: Ack
            end
            g->>s: Make play with valid card
            s->>h: Here is Player n's move
        h->>h: Validate move
        h->>s: Ack
        s->>g: Ack
        deactivate g
        h->>h: Play card against discard
        h->>h: Put card on discard pile
        Note right of h: Card is a Draw 2
        h->>h: Pull two cards from deal pile
        h->>s: Player n gets two new cards
        s->>n: Here are two new cards
        activate n
        n->>s: Ack
        deactivate n
        s->>h: Player n Ack'd new cards
        rect rgba(0, 0, 255, .1)
        Note over h,a: Can be sent as one message
            loop Move is Valid
                par Discard
                    h->>s: Card X is on Discard
                    s->>a: Card X is on Discard
                    activate a
                    a->>s: Ack
                    deactivate a
                    s->>h: Player Ack'd
                and Update Player 1..n
                    h->>s: Here are the current player card numbers
                    s->>a: Here are the current player card numbers
                    activate a
                    a->>s: Ack
                    deactivate a
                    s->>h: Player Ack'd
                end
            end
        end
        h->>h: All Players Ack'd
        h->>h: Advance to next player
    end
```