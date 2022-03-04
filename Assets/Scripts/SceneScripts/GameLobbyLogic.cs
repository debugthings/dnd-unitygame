using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Common;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameLobbyLogic : MonoBehaviourPunCallbacks
{
    public GameObject roomListObject;
    public Button createGameButton;
    public TMPro.TMP_InputField gameRoomName;
    private byte playerNumbers = 10;
    private Unity.Mathematics.Random rand = new Unity.Mathematics.Random();
    private int seedTicks = 0;

    private Dictionary<string, RoomInfo> activeRoomList = new Dictionary<string, RoomInfo>();

    public AssetReference roomItemListPrefabReference;

    private GameObject roomItemListPrefab;

    // Start is called before the first frame update

    void Start()
    {
        createGameButton.onClick.AddListener(CreateGameRoomButton);
        createGameButton.interactable = false;
        gameRoomName.onValueChanged.AddListener(GameRoomNameChanged);

        if (Application.isEditor)
        {
            CustomLogger.Log("Running expected random seed from editor.");
            seedTicks = 1851936439;
        }
        else
        {
            seedTicks = (new System.Random()).Next(0, int.MaxValue);
        }
        CustomLogger.Log($"Using seed {seedTicks}");
        rand.InitState(Convert.ToUInt32(seedTicks));

    }

    void GameRoomNameChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            createGameButton.interactable = true;
        }
        else
        {
            createGameButton.interactable = false;
        }

    }

    void CreateGameRoomButton()
    {
        var roomOptions = new Photon.Realtime.RoomOptions()
        {
            MaxPlayers = playerNumbers,
            PlayerTtl = 2000,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable()
            {
                [Constants.SeedKeyName] = seedTicks,
            }
        };
        // Try to create the room in the default lobby.
        PhotonNetwork.CreateRoom(gameRoomName.text.ToLower(), roomOptions);

    }

    public async override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (roomItemListPrefab == null)
        {
            var dimmablePrefabOperation = roomItemListPrefabReference.LoadAssetAsync<GameObject>();
            roomItemListPrefab = await dimmablePrefabOperation.Task;
        }
        // It'd be better to determine the delta of what is here and what is not so we could just remove or add
        // what ever we needed, but I don't think we'll see something that large (yet)

        // Destroy the old
        foreach (Transform child in roomListObject.transform)
        {
            GameObject.Destroy(child.gameObject, 0);
        }

        // Add the new
        foreach (var item in roomList)
        {
            if (item.IsVisible && item.IsOpen)
            {
                activeRoomList[item.Name] = item;
            }

            if (item.RemovedFromList)
            {
                activeRoomList.Remove(item.Name);
            }
        }

        // emove the old

        foreach (var item in activeRoomList.Values)
        {
            var itemObject = Instantiate(roomItemListPrefab, Vector3.zero, Quaternion.identity, roomListObject.transform);
            var itemObjectScript = itemObject.GetComponentInChildren<RoomItemLogic>();
            itemObjectScript.SetRoomRame(item.Name, item.PlayerCount, item.MaxPlayers);

        }

        base.OnRoomListUpdate(roomList);
    }

    public override void OnCreatedRoom()
    {
        CustomLogger.Log($"Room {gameRoomName.text.ToLower()} created. Waiting to join.");
        base.OnCreatedRoom();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        CustomLogger.Log($"Unable to create room {gameRoomName.text.ToLower()}\r\n{message}");
        var msgText = $@"Unable to create room {gameRoomName.text.ToLower()}.

{message}";
        // var msg = errorMessage.GetComponent<TMPro.TMP_Text>();
        // msg.text = msgText;
        // errorMessage.SetActive(true);
        base.OnCreateRoomFailed(returnCode, msgText);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        CustomLogger.Log($"Unable to join room {gameRoomName.text.ToLower()}\r\n{message}");
        var msgText = $@"Unable to join room {gameRoomName.text.ToLower()}.

{message}";
        // var msg = errorMessage.GetComponent<TMPro.TMP_Text>();
        // msg.text = msgText;
        // errorMessage.SetActive(true);
        base.OnJoinRoomFailed(returnCode, msgText);
    }

    public override void OnJoinedRoom()
    {
        CustomLogger.Log($"Joined game room {gameRoomName.text.ToLower()} from lobby.");
        JoinOrCreateAction();
        base.OnJoinedRoom();
    }

    private void JoinOrCreateAction()
    {
        PhotonNetwork.LoadLevel("GameRoom");
    }



    // Update is called once per frame
    void Update()
    {
        PingHelper.Ping(Time.deltaTime);
    }
}
