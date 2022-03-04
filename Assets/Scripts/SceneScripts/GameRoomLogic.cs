using System.Linq;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Common;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameRoomLogic : MonoBehaviourPunCallbacks
{
    public TMPro.TMP_Text playerList, roomName;
    public Button leaveButton, startGame;
    // Start is called before the first frame update

    private bool startGameWhenAllAreReady = false;


    void Start()
    {
        CustomLogger.Log($"Game room logic started.");
        StartupRoom();


        leaveButton.onClick.AddListener(() =>
        {
            CustomLogger.Log($"Leaving Room {PhotonNetwork.CurrentRoom.Name}");
            PhotonNetwork.LeaveRoom(true);
            PhotonNetwork.SendAllOutgoingCommands();
        });

        if (PhotonNetwork.IsMasterClient)
        {
            CustomLogger.Log($"Client is master client, setting button to be a start button.");
        }
        else
        {
            CustomLogger.Log($"Client is not the master client, setting button to be a leave button.");
            var text = startGame.GetComponentInChildren<TMPro.TMP_Text>();
            text.text = "Ready";
        }

        startGame.onClick.AddListener(() =>
            {
                PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [Constants.PlayerReady] = true });
                
                // Disable the button so we don't keep clicking it.
                startGame.interactable = false;
                if (PhotonNetwork.IsMasterClient)
                {

                    
                    startGameWhenAllAreReady = true;
                }

            });
    }



    public override void OnMasterClientSwitched(Player newPlayer)
    {
        if (newPlayer == PhotonNetwork.LocalPlayer)
        {
            CustomLogger.Log($"This client is the new master client.");
            var text = startGame.GetComponentInChildren<TMPro.TMP_Text>();
            text.text = "Start";
        }
        else
        {
            CustomLogger.Log($"This client is the new master client.");
            var text = startGame.GetComponentInChildren<TMPro.TMP_Text>();
            text.text = "Ready";
        }
        startGameWhenAllAreReady = false;
    }

    public override void OnConnectedToMaster()
    {
        CustomLogger.Log($"Back on master so we can do matchmaking.");
        PhotonNetwork.JoinLobby();
        base.OnConnectedToMaster();
    }

    public override void OnLeftRoom()
    {
        CustomLogger.Log($"We have left the room.");
        base.OnLeftRoom();
    }

    public override void OnJoinedLobby()
    {
        PhotonNetwork.LoadLevel("GameLobby");
        base.OnJoinedLobby();
    }

    private static bool CheckAllPlayersAreReady()
    {
        bool allPlayersReady = true;
        if (PhotonNetwork.CurrentRoom.Players.Count <= 1)
            return false;

        foreach (var item in PhotonNetwork.CurrentRoom.Players)
        {
            if (item.Value.CustomProperties.ContainsKey(Constants.PlayerReady))
            {
                allPlayersReady &= true;
            }
            else
            {
                allPlayersReady &= false;
            }
        }

        return allPlayersReady;
    }

    private static void StartGame()
    {
        PhotonNetwork.CurrentRoom.IsOpen = false;
        CustomLogger.Log($"Starting game from master client.");
        var hashTable = new ExitGames.Client.Photon.Hashtable
        {
            [Constants.GameStarted] = true
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);
        PhotonNetwork.CurrentRoom.PlayerTtl = 60000;
        // When the master client starts the game we should make the room maxed out so nobody can join
        PhotonNetwork.LoadLevel("LocalGame");
        // Turn off scene sync so a newly joining player won't be presented with a game board.
    }

    private void StartupRoom()
    {
        roomName.text = PhotonNetwork.CurrentRoom.Name;
        UpdatePlayerList();
    }

    public override void OnJoinedRoom()
    {
        CustomLogger.Log($"On joined room called");
        StartupRoom();
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(Constants.GameStarted))
        {
            CustomLogger.Log($"Game started cannot join.");
        }
        base.OnJoinedRoom();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
    }


    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        CustomLogger.Log($"{newPlayer} joined room {PhotonNetwork.CurrentRoom.Name}");
        UpdatePlayerList();
        base.OnPlayerEnteredRoom(newPlayer);
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player newPlayer)
    {
        CustomLogger.Log($"{newPlayer} left room {PhotonNetwork.CurrentRoom.Name}");

        UpdatePlayerList();
        base.OnPlayerLeftRoom(newPlayer);
    }

    private void UpdatePlayerList()
    {
        // Simple text list ordered by actor number
        var playersInGame = new StringBuilder();
        var playerCollection = PhotonNetwork.CurrentRoom.Players.OrderBy(player => player.Value.ActorNumber);

        foreach (var item in playerCollection)
        {
            string playerName = item.Value.NickName;
            if (item.Value.CustomProperties.ContainsKey(Constants.PlayerReady))
            {
                playerName += $" (ready)";
            }
            playersInGame.AppendLine(playerName);
        }
        playerList.text = playersInGame.ToString();

        // Check to see if the master client has clicked the start button.
        if (!startGameWhenAllAreReady)
        {
            // Only allow us to start a game with more than one player
            if (PhotonNetwork.CurrentRoom.PlayerCount >= 2 && PhotonNetwork.IsMasterClient)
            {
                startGame.interactable = true;
            }
            else if (PhotonNetwork.CurrentRoom.PlayerCount < 2 && PhotonNetwork.IsMasterClient)
            {
                startGame.interactable = false;
            }
        }
        else
        {
            CheckAndStartGame();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);
        UpdatePlayerList();
    }

    private void CheckAndStartGame()
    {
        if (startGameWhenAllAreReady)
        {
            if (CheckAllPlayersAreReady())
            {
                StartGame();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        PingHelper.Ping(Time.deltaTime);
    }
}
