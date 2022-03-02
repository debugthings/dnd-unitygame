using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Common;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class RoomItemLogic : MonoBehaviourPunCallbacks
{
    public TMPro.TMP_Text roomName;

    public Button joinRoomButton;

    private string roomNameText = string.Empty;

    // Start is called before the first frame update
    void Start()
    {
        joinRoomButton.onClick.AddListener(JoinGameButton);
        joinRoomButton.interactable = true;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SetRoomRame(string roomNameToSet, int currentPlayers, int maxPlayers)
    {
        roomNameText = roomNameToSet;
        roomName.text = $"{roomNameToSet} ({currentPlayers}/{maxPlayers})";
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        CustomLogger.Log($"Unable to join room {roomNameText.ToLower()}\r\n{message}");
        var msgText = $@"Unable to join room {roomNameText.ToLower()}.

{message}";
        // var msg = errorMessage.GetComponent<TMPro.TMP_Text>();
        // msg.text = msgText;
        // errorMessage.SetActive(true);
        base.OnJoinRoomFailed(returnCode, msgText);
    }

    public override void OnJoinedRoom()
    {
        CustomLogger.Log($"Joined game room {roomNameText.ToLower()}.");
        JoinOrCreateAction();
        base.OnJoinedRoom();
    }

    private void JoinOrCreateAction()
    {
        PhotonNetwork.LoadLevel("GameRoom");
    }

    void JoinGameButton()
    {
        // Try to join the room in the default lobby.
        PhotonNetwork.JoinRoom(roomNameText.ToLower());
    }
}
