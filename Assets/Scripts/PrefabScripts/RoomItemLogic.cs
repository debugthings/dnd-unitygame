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

      void JoinGameButton()
    {
        // Try to join the room in the default lobby.
        PhotonNetwork.JoinRoom(roomNameText.ToLower());
    }
}
