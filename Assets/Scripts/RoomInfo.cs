using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RoomInfo : MonoBehaviour
{
    public string RoomName { get { return roomName.text; } set { roomName.text = value; } }
    public int PlayerCount;
    public int MaxPlayers;

    [SerializeField] private TextMeshProUGUI roomName;
    [SerializeField] private TextMeshProUGUI currentPlayers;

    public void SelectSession()
    {
        transform.parent.GetComponent<RoomList>().menuConnection.SelectRoom(RoomName);
    }

    public void SetRoomInfo(string sessionName, int playerCount, int maxPlayers)
    {
        RoomName = sessionName;
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
        currentPlayers.text = $"{PlayerCount}/{MaxPlayers}";
    }
}
