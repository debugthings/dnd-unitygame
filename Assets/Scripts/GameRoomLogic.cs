using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class GameRoomLogic : MonoBehaviourPunCallbacks
{
    public TMPro.TMP_Text playerList;
    public Button startGame; 
    // Start is called before the first frame update
    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        startGame.enabled = false;
        startGame.interactable = false;
        startGame.onClick.AddListener(() =>
        {
            // When the master client starts the game we should make the room maxed out so nobody can join
            PhotonNetwork.CurrentRoom.MaxPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
            PhotonNetwork.LoadLevel("LocalGame");
        });
    }

    public override void OnConnected()
    {
        base.OnConnected();
    }

    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();
    }

    public override void OnJoinedRoom()
    {
        startGame.enabled = PhotonNetwork.IsMasterClient;
        startGame.interactable = PhotonNetwork.IsMasterClient;
        UpdatePlayerList();
        base.OnJoinedRoom();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        UpdatePlayerList();
        base.OnPlayerEnteredRoom(newPlayer);
    }

    private void UpdatePlayerList()
    {
        var playersInGame = new StringBuilder();
        var playerCollection = PhotonNetwork.CurrentRoom.Players;
        for (int i = 1; i < playerCollection.Count; i++)
        {
            playersInGame.AppendLine(playerCollection[i].NickName);
        }
        playerList.text = playersInGame.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
