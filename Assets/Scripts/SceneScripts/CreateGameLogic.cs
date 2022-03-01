using System;
using Assets.Scripts;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class CreateGameLogic : MonoBehaviourPunCallbacks
{
    //Make sure to attach these Buttons in the Inspector
    public Button createGameButton;
    public TMPro.TMP_InputField userName;
    public GameObject errorMessage, connectedMessage;

    

    void Start()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        //Calls the TaskOnClick/TaskWithParameters/ButtonClicked method when you click the Button
        createGameButton.onClick.AddListener(JoinLobbyButton);
        createGameButton.interactable = false;

        userName.interactable = false;
        userName.onValueChanged.AddListener(GameRoomNameChanged);


        PhotonNetwork.ConnectUsingSettings();
    }

    void GameRoomNameChanged(string value)
    {
        
        if (!string.IsNullOrEmpty(value) && PhotonNetwork.IsConnectedAndReady)
        {
            createGameButton.interactable = true;
        }
        else
        {
            createGameButton.interactable = false;
        }

    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"Connected to master {PhotonNetwork.ServerAddress} ({PhotonNetwork.CloudRegion})");
        PhotonNetwork.AutomaticallySyncScene = true;
        var msgText = $"Connected: {PhotonNetwork.ServerAddress} ({PhotonNetwork.CloudRegion})";
        var msg = connectedMessage.GetComponent<TMPro.TMP_Text>();
        msg.text = string.Format(msgText, msgText);
        userName.interactable = true;
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

    public override void OnJoinedLobby()
    {
        PhotonNetwork.LoadLevel("GameLobby");
        base.OnJoinedLobby();
    }

    void JoinLobbyButton()
    {
        if (string.IsNullOrEmpty(userName.text))
        {
            var msg = errorMessage.GetComponent<TMPro.TMP_Text>();
            var msgText = $"User name must not be empty.";
            msg.text = msgText;
            errorMessage.SetActive(true);
            Debug.Log(msgText);
            return;
        }
        // Try to create the room in the default lobby.
        PhotonNetwork.NickName = userName.text;
        PhotonNetwork.JoinLobby();

    }

    void Update()
    {
        PingHelper.Ping(Time.deltaTime);
    }
}
