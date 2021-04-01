using Assets.Scripts;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameRoomLogic : MonoBehaviourPunCallbacks
{
    public TMPro.TMP_Text playerList, roomName;
    public GameObject startGame;
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log($"Game room logic started.");
        StartupRoom();
        var button = startGame.GetComponent<Button>();
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log($"Client is master client, setting button to be a start button.");
            button.onClick.AddListener(() =>
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
                Debug.Log($"Starting game from master client.");
                var hashTable = new ExitGames.Client.Photon.Hashtable
                {
                    [Constants.GameStarted] = true
                };
                PhotonNetwork.CurrentRoom.SetCustomProperties(hashTable);
                // When the master client starts the game we should make the room maxed out so nobody can join
                PhotonNetwork.LoadLevel("LocalGame");
                // Turn off scene sync so a newly joining player won't be presented with a game board.
            });
        }
        else
        {
            Debug.Log($"Client is not the master client, setting button to be a leave button.");
            var text = button.GetComponentInChildren<TMPro.TMP_Text>();
            text.text = "Leave";
            button.onClick.AddListener(() =>
            {
                PhotonNetwork.LeaveRoom();
                SceneManager.LoadScene("CreateGame");
            });
        }

    }

    private void StartupRoom()
    {
        roomName.text = PhotonNetwork.CurrentRoom.Name;
        UpdatePlayerList();
    }

    public override void OnConnected()
    {
        Debug.Log($"On connected called");
        base.OnConnected();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"On connected to master called");
        base.OnConnectedToMaster();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"On joined room called");
        StartupRoom();
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(Constants.GameStarted))
        {
            Debug.Log($"Game started cannot join.");
        }
        base.OnJoinedRoom();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Debug.Log($"{newPlayer} joined room {PhotonNetwork.CurrentRoom.Name}");
        UpdatePlayerList();
        base.OnPlayerEnteredRoom(newPlayer);
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player newPlayer)
    {
        Debug.Log($"{newPlayer} left room {PhotonNetwork.CurrentRoom.Name}");
        UpdatePlayerList();
        base.OnPlayerEnteredRoom(newPlayer);
    }

    private void UpdatePlayerList()
    {
        // Simple text list ordered by actor number
        var playersInGame = new StringBuilder();
        var playerCollection = PhotonNetwork.CurrentRoom.Players.OrderBy(player => player.Value.ActorNumber);

        foreach (var item in playerCollection)
        {
            playersInGame.AppendLine(item.Value.NickName);
        }
        playerList.text = playersInGame.ToString();

        // Only allow us to start a game with more than one player
        if (PhotonNetwork.CurrentRoom.PlayerCount >= 2 && PhotonNetwork.IsMasterClient)
        {
            var button = startGame.GetComponent<Button>();
            button.interactable = true;
        }
        else if (PhotonNetwork.CurrentRoom.PlayerCount < 2 && PhotonNetwork.IsMasterClient)
        {
            var button = startGame.GetComponent<Button>();
            button.interactable = false;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
