using Assets.Scripts;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreateGameLogic : MonoBehaviourPunCallbacks
{
    //Make sure to attach these Buttons in the Inspector
    public Button createGameButton;
    public TMPro.TMP_InputField gameRoomName, userName;
    public TMPro.TMP_Dropdown numberOfPlayers;

    private byte playerNumbers = 2;
    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();
    void Start()
    {
        //Calls the TaskOnClick/TaskWithParameters/ButtonClicked method when you click the Button
        createGameButton.onClick.AddListener(JoinGameButton);
        numberOfPlayers.onValueChanged.AddListener(DropwDownNumberChanged);
        rand.InitState();
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("PUN Basics Tutorial/Launcher: OnConnectedToMaster() was called by PUN");
        var roomOptions = new Photon.Realtime.RoomOptions()
        {
            MaxPlayers = playerNumbers,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable()
            {
                [Constants.SeedKeyName] = rand.NextInt(0, int.MaxValue),
            }
        };
        // Try to create the room in the default lobby.
        PhotonNetwork.NickName = userName.text;
        PhotonNetwork.JoinOrCreateRoom(gameRoomName.text, roomOptions, TypedLobby.Default);
        PhotonNetwork.LoadLevel("GameRoom");
    }

    public override void OnConnected()
    {
        base.OnConnected();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarningFormat("PUN Basics Tutorial/Launcher: OnDisconnected() was called by PUN with reason {0}", cause);
    }

    public override void OnCreatedRoom()
    {
        base.OnCreatedRoom();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties != null)
        {
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("seed", out object value);
            int seed = (int)value;
        }
        base.OnJoinedRoom();
    }
    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
    }
    void DropwDownNumberChanged(int newNumber)
    {
        playerNumbers = (byte)(newNumber + 2);
    }

    void JoinGameButton()
    {
        PhotonNetwork.ConnectUsingSettings();
    }

}
