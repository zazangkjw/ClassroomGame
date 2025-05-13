using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RoomInfo : MonoBehaviour
{
    public string FusionSession;
    public string RoomName { get { return roomName.text; } set { roomName.text = value; } }
    public int PlayerCount;
    public int MaxPlayers;

    [SerializeField] private TextMeshProUGUI roomName;
    [SerializeField] private TextMeshProUGUI currentPlayers;

    public void SelectSession()
    {
        UIManager.Singleton._MenuConnection.SelectRoom(RoomName);
    }

    public void SetRoomInfo(string fusionSession, string roomName, int playerCount, int maxPlayers)
    {
        FusionSession = fusionSession;
        RoomName = roomName;
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
        currentPlayers.text = $"{PlayerCount}/{MaxPlayers}";
    }
}
