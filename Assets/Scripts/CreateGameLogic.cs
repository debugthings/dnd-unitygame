using Assets.Scripts;
using Photon.Pun;
using Photon.Realtime;
using System;
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
    public GameObject errorMessage, connectedMessage;

    private byte playerNumbers = 10;
    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();
    private int seedTicks = 0;
    void Start()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        //Calls the TaskOnClick/TaskWithParameters/ButtonClicked method when you click the Button
        createGameButton.onClick.AddListener(JoinGameButton);
        createGameButton.interactable = false;
        if (Application.isEditor)
        {
            Debug.Log("Running expected random seed from editor.");
            seedTicks = 1851936439;
        }
        else 
        {
            seedTicks = (new System.Random()).Next(0, int.MaxValue);
        }
        Debug.Log($"Using seed {seedTicks}");
        rand.InitState(Convert.ToUInt32(seedTicks));
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"Connected to master {PhotonNetwork.ServerAddress} ({PhotonNetwork.CloudRegion})");
        PhotonNetwork.AutomaticallySyncScene = true;
        var msgText = $"Connected: {PhotonNetwork.ServerAddress} ({PhotonNetwork.CloudRegion})";
        var msg = connectedMessage.GetComponent<TMPro.TMP_Text>();
        msg.text = string.Format(msgText, msgText);
        createGameButton.interactable = true;
        base.OnConnected();
        
    }

    public override void OnConnected()
    {
        Debug.Log($"Connected to {PhotonNetwork.ServerAddress} ({PhotonNetwork.CloudRegion})");
        base.OnConnected();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        createGameButton.interactable = false;
        var msg = errorMessage.GetComponent<TMPro.TMP_Text>();
        var msgText = $"Disconnected from {PhotonNetwork.ServerAddress} ({PhotonNetwork.CloudRegion})\r\n{cause}";
        msg.text = msgText;
        errorMessage.SetActive(true);
        Debug.Log(msgText);
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"Room {gameRoomName.text.ToLower()} created. Waiting to join.");
        base.OnCreatedRoom();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log($"Unable to create room {gameRoomName.text.ToLower()}\r\n{message}");
        var msgText = $@"Unable to create room {gameRoomName.text.ToLower()}.

{message}";
        var msg = errorMessage.GetComponent<TMPro.TMP_Text>();
        msg.text = msgText;
        errorMessage.SetActive(true);
        base.OnCreateRoomFailed(returnCode, msgText);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log($"Unable to join room {gameRoomName.text.ToLower()}\r\n{message}");
        var msgText = $@"Unable to join room {gameRoomName.text.ToLower()}.

{message}";
        var msg = errorMessage.GetComponent<TMPro.TMP_Text>();
        msg.text = msgText;
        errorMessage.SetActive(true);
        base.OnJoinRoomFailed(returnCode, msgText);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room {gameRoomName.text.ToLower()} created. Waiting to join.");
        JoinOrCreateAction();
        base.OnJoinedRoom();
    }

    private void JoinOrCreateAction()
    {
        PhotonNetwork.LoadLevel("GameRoom");
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
    }

    void JoinGameButton()
    {
        var roomOptions = new Photon.Realtime.RoomOptions()
        {
            MaxPlayers = playerNumbers,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable()
            {
                [Constants.SeedKeyName] = seedTicks,
            }
        };
        // Try to create the room in the default lobby.
        PhotonNetwork.NickName = userName.text;
        PhotonNetwork.JoinOrCreateRoom(gameRoomName.text.ToLower(), roomOptions, TypedLobby.Default);
    }
}
