using Fusion;
using Steamworks;
using Steamworks.Data;
using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using WebSocketSharp;

public class MenuConnection : MonoBehaviour
{
    public bool IsTest;
    public string SessionName;
    public Lobby[] LobbyList;
    public Lobby[] LobbyList2;
    public Lobby CurrentLobby;
    public AuthTicket Ticket;

    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private TMP_InputField hostRoomName;
    [SerializeField] private TMP_InputField joinRoomName;
    [SerializeField] private TextMeshProUGUI hostFailMessage;
    [SerializeField] private TextMeshProUGUI joinFailMessage;
    [SerializeField] private UnityEvent onJoinLobby;
    [SerializeField] private UnityEvent onSessionConnected;
    [SerializeField] private UnityEvent onSessionDisconnected;
    [SerializeField] private Transform roomListContent;

    private NetworkRunner runner;
    private int maxPlayers = 5;
    private bool isHost;

    private void Start()
    {
        TrySteamClientInit();
        SteamMatchmaking.OnLobbyEntered += SteamMatchMaking_OnLobbyEnter;
        Debug.Log("�̺�Ʈ �߰�");
    }

    private void OnApplicationQuit()
    {
        SteamMatchmaking.OnLobbyEntered -= SteamMatchMaking_OnLobbyEnter;
        SteamClient.Shutdown();
    }

    private async void Test()
    {
        SessionName = "Test";

        await UpdateLobbyListFromSteam2(SessionName);

        if (LobbyList2 != null && LobbyList2.Length > 0)
        {
            foreach (var lobby in LobbyList2)
            {
                if (lobby.GetData("alive") == "true")
                {
                    await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
                    return;
                }
            }
        }

        if (runner == null)
        {
            runner = Instantiate(runnerPrefab);
            runner.GetComponent<InputManager>().menuConnection = this;
        }

        isHost = true;

        string fusionSession = Guid.NewGuid().ToString(); // ���� �ĺ��� ����
        var createLobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        CurrentLobby = createLobby.Value;
        CurrentLobby.SetData("owner", "zazangkjw");
        CurrentLobby.SetData("fusion_session", fusionSession);
        CurrentLobby.SetData("name", SessionName); // UI ǥ�ÿ�
        CurrentLobby.SetData("alive", "true");
        if (true) // ���� üũ�Ǿ� ������ �κ� ����
        {
            CurrentLobby.SetPublic();
        }

        var sceneInfo = new NetworkSceneInfo(); // �� ������ �����ϴ� ����ü��, �ִ� 8���� ���� ���� ����(Addictive)
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1), LoadSceneMode.Single); // ���Ͼ� �߰�

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = fusionSession,
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
            PlayerCount = maxPlayers,
            Scene = sceneInfo
        });

        var init = new SteamServerInit("default", "zazangkjw Multiplayer Server")
        {
            GamePort = 27015,
            QueryPort = 27016,
            Secure = true,
            VersionString = "1.0.0"
        };
        //SteamServer.Init(480, init, true); // �׽�Ʈ �� ���� �ּ� ó��. �� �׷��� ���� �ٲ� ������ ������ ������ؾ� ��

        onSessionConnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        UIManager.Singleton.IsFirstJoin = true;
        UIManager.Singleton.CharacterIndex = 0;
    }

    public void TrySteamClientInit()
    {
        try
        {
            SteamClient.Init(480, true);
        }
        catch (System.Exception e)
        {
            UIManager.Singleton.OpenBlocking(true);
            UIManager.Singleton.OpenSteamPopUp(true);
            Debug.Log($"Steam Init Failed: {e.Message}");
            return;
        }

        UIManager.Singleton.OpenBlocking(false);

        if (IsTest)
        {
            IsTest = false;
            Test();
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void SteamMatchMaking_OnLobbyEnter(Lobby lobby)
    {
        if (!isHost)
        {
            if (lobby.GetData("alive") == "true")
            {
                CurrentLobby = lobby;
                StartClient();
            }
            else
            {
                lobby.Leave();
            }
        }
    }

    public void JoinLobby()
    {
        hostFailMessage.text = "";
        joinFailMessage.text = "";
        onJoinLobby.Invoke();
        RefreshLobby();
    }

    // �κ� ����Ʈ ������Ʈ
    public async Task UpdateLobbyListFromSteam()
    {
        LobbyList = await SteamMatchmaking.LobbyList.FilterDistanceWorldwide().WithKeyValue("owner", "zazangkjw").RequestAsync();
    }

    public async Task UpdateLobbyListFromSteam2(string name)
    {
        LobbyList2 = await SteamMatchmaking.LobbyList.FilterDistanceWorldwide().WithKeyValue("name", name).RequestAsync();
    }

    // �� ��� ���ΰ�ħ
    public async void RefreshLobby()
    {
        foreach (Transform child in roomListContent)
        {
            Destroy(child.gameObject);
        }

        await UpdateLobbyListFromSteam();

        if (LobbyList != null && LobbyList.Length > 0)
        {
            RoomInfo room;

            foreach (var lobby in LobbyList)
            {
                room = Instantiate(roomPrefab, roomListContent).GetComponent<RoomInfo>();
                room.SetRoomInfo(lobby.GetData("fusion_session"), lobby.GetData("name"), lobby.MemberCount, lobby.MaxMembers);
            }
        }
    }

    public void SelectRoom(string roomName)
    {
        joinRoomName.text = roomName;
    }

    public async void JoinByName()
    {
        SessionName = joinRoomName.text;

        if (SessionName.IsNullOrEmpty())
        {
            return;
        }

        await UpdateLobbyListFromSteam2(SessionName);

        if (LobbyList2 != null && LobbyList2.Length > 0)
        {
            foreach (var lobby in LobbyList2)
            {
                if (lobby.GetData("alive") == "true")
                {
                    await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
                    return;
                }
            }
        }

        joinFailMessage.text = $"Not Exists Name";
    }

    public async void StartHost()
    {
        SessionName = hostRoomName.text;

        if (SessionName.IsNullOrEmpty())
        {
            return;
        }

        await UpdateLobbyListFromSteam();

        if (LobbyList != null && LobbyList.Length > 0)
        {
            foreach (var lobby in LobbyList)
            {
                if (lobby.GetData("name") == SessionName)
                {
                    hostFailMessage.text = $"Already Exists";
                    return;
                }
            }
        }

        if (runner == null)
        {
            runner = Instantiate(runnerPrefab);
            runner.GetComponent<InputManager>().menuConnection = this;
        }

        hostFailMessage.text = $"Wait...";
        isHost = true;

        string fusionSession = Guid.NewGuid().ToString(); // ���� �ĺ��� ����
        var createLobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        CurrentLobby = createLobby.Value;
        CurrentLobby.SetData("owner", "zazangkjw");
        CurrentLobby.SetData("fusion_session", fusionSession);
        CurrentLobby.SetData("name", SessionName); // UI ǥ�ÿ�
        CurrentLobby.SetData("alive", "true");
        if (true) // ���� üũ�Ǿ� ������ �κ� ����
        {
            CurrentLobby.SetPublic();
        }

        var sceneInfo = new NetworkSceneInfo(); // �� ������ �����ϴ� ����ü��, �ִ� 8���� ���� ���� ����(Addictive)
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1), LoadSceneMode.Single); // ���Ͼ� �߰�

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = fusionSession,
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
            PlayerCount = maxPlayers,
            Scene = sceneInfo
        });

        var init = new SteamServerInit("default", "zazangkjw Multiplayer Server")
        {
            GamePort = 27015,
            QueryPort = 27016,
            Secure = true,
            VersionString = "1.0.0"
        };
        //SteamServer.Init(480, init, true); // �׽�Ʈ �� ���� �ּ� ó��. �� �׷��� ���� �ٲ� ������ ������ ������ؾ� ��

        onSessionConnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        UIManager.Singleton.IsFirstJoin = true;
        UIManager.Singleton.CharacterIndex = 0;
    }

    public async void StartClient()
    {
        if (runner == null)
        {
            runner = Instantiate(runnerPrefab);
            runner.GetComponent<InputManager>().menuConnection = this;
        }

        joinFailMessage.text = $"Wait...";

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = CurrentLobby.GetData("fusion_session"),
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
        });

        onSessionConnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        UIManager.Singleton.IsFirstJoin = true;
        UIManager.Singleton.CharacterIndex = 0;
    }

    public async void LeaveSession()
    {
        if (CurrentLobby.Owner.Id != SteamClient.SteamId)
        {
            CurrentLobby.Leave();
        }
        else if (isHost)
        {
            isHost = false;
            CurrentLobby.SetData("alive", "false");
            CurrentLobby.Leave();
            SteamServer.Shutdown();
        }
        Ticket?.Cancel();
        Ticket = null;
        await runner.Shutdown();
        runner = null;
        SceneManager.LoadScene("Main");
        onSessionDisconnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        UIManager.Singleton.MouseText.text = "";
        if (GameStateManager.Singleton != null)
        {
            Destroy(GameStateManager.Singleton);
        }
    }

    public async void KickedFromSession()
    {
        if (CurrentLobby.Owner.Id != SteamClient.SteamId)
        {
            CurrentLobby.Leave();
        }
        Ticket?.Cancel();
        Ticket = null;
        await runner.Shutdown();
        runner = null;
        SceneManager.LoadScene("Main");
        onSessionDisconnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        UIManager.Singleton.MouseText.text = "";
        UIManager.Singleton.SetKickedPopupMessage("Kicked by host");
        UIManager.Singleton.OpenKickedPopUp(true);
        UIManager.Singleton.OpenBlocking(true);
        if (GameStateManager.Singleton != null)
        {
            Destroy(GameStateManager.Singleton);
        }
    }

    public async void SteamAuthFail()
    {
        if (CurrentLobby.Owner.Id != SteamClient.SteamId)
        {
            CurrentLobby.Leave();
        }
        Ticket?.Cancel();
        Ticket = null;
        await runner.Shutdown();
        runner = null;
        SceneManager.LoadScene("Main");
        onSessionDisconnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        UIManager.Singleton.MouseText.text = "";
        UIManager.Singleton.SetKickedPopupMessage("Steam auth failed");
        UIManager.Singleton.OpenKickedPopUp(true);
        UIManager.Singleton.OpenBlocking(true);
        if (GameStateManager.Singleton != null)
        {
            Destroy(GameStateManager.Singleton);
        }
    }
}