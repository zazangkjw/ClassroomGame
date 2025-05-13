#region Photon ����
using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using WebSocketSharp;

public class MenuConnection : MonoBehaviour
{
    public string TestSceneName;
    public string SessionName;
    public List<SessionInfo> SessionList = new(); // Photon ����
    public bool DoRefresh;

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

    private void Start()
    {
        if (!TestSceneName.IsNullOrEmpty())
        {
            onSessionConnected.Invoke();
            StartTest();
        }
    }

    public async void StartTest()
    {
        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "Test",
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
        });

        if (runner.GameMode == GameMode.Host)
        {
            await runner.LoadScene(TestSceneName);
        }

        onSessionConnected.Invoke();
        UIManager.Singleton.UIStack = 0;
    }

    public void SelectRoom(string roomName)
    {
        joinRoomName.text = roomName;
    }

    public async void JoinLobby()
    {
        if (runner == null)
        {
            runner = Instantiate(runnerPrefab);
            runner.GetComponent<InputManager>().menuConnection = this;
        }

        hostFailMessage.text = "";
        joinFailMessage.text = "";

        DoRefresh = true;
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        onJoinLobby.Invoke();
    }

    // �� ��� ���ΰ�ħ
    public void RefreshLobby()
    {
        foreach (Transform child in roomListContent)
        {
            Destroy(child.gameObject);
        }

        if (SessionList.Count > 0)
        {
            RoomInfo room;

            foreach (var session in SessionList)
            {
                room = Instantiate(roomPrefab, roomListContent).GetComponent<RoomInfo>();
                room.SetRoomInfo("", session.Name, session.PlayerCount, session.MaxPlayers);
            }
        }
    }

    public async void StartHost()
    {
        SessionName = hostRoomName.text;

        if (SessionName.IsNullOrEmpty())
        {
            return;
        }

        if (SessionList.Exists(s => s.Name == SessionName))
        {
            hostFailMessage.text = $"Already Exists";
            return;
        }

        if (!SessionName.IsNullOrEmpty())
        {
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(1), LoadSceneMode.Single);
            hostFailMessage.text = $"Wait...";

            await runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = this.SessionName,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                PlayerCount = maxPlayers,
                Scene = sceneInfo
            });

            //await runner.LoadScene("Lobby");
            onSessionConnected.Invoke();
            UIManager.Singleton.UIStack = 0;
        }
    }

    public async void StartClient()
    {
        SessionName = joinRoomName.text;

        if (!SessionList.Exists(s => s.Name == SessionName))
        {
            joinFailMessage.text = $"Not Exists Name";
            return;
        }

        if (!SessionName.IsNullOrEmpty())
        {
            joinFailMessage.text = $"Wait...";

            await runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = this.SessionName,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
            });

            onSessionConnected.Invoke();
            UIManager.Singleton.UIStack = 0;
        }
    }

    public async void LeaveSession()
    {
        await runner.Shutdown();
        SceneManager.LoadScene("Main");
        onSessionDisconnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        Destroy(GameStateManager.Singleton);
    }

    public async void KickedFromSession()
    {
        await runner.Shutdown();
        SceneManager.LoadScene("Main");
        onSessionDisconnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        UIManager.Singleton.OpenKickedPopup(true);
        Destroy(GameStateManager.Singleton);
    }
}
#endregion



/*#region steamworks ����
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
    public string TestSceneName;
    public string SessionName;
    public Lobby[] LobbyList; // steamworks ����

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

    private void Awake()
    {
        try
        {
            Steamworks.SteamClient.Init(252490, true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Steam Init Failed: {e.Message}");
        }
    }

    private void Update()
    {
        Steamworks.SteamClient.RunCallbacks();
    }

    private void OnEnable()
    {
        SteamMatchmaking.OnLobbyEntered += SteamMatchMaking_OnLobbyEnter;
    }

    private void OnApplicationQuit()
    {
        SteamMatchmaking.OnLobbyEntered -= SteamMatchMaking_OnLobbyEnter;
        Steamworks.SteamClient.Shutdown();
    }

    public void SteamMatchMaking_OnLobbyEnter(Lobby lobby)
    {
        StartClient(lobby);
    }

    public void JoinLobby()
    {
        if (runner == null)
        {
            runner = Instantiate(runnerPrefab);
            runner.GetComponent<InputManager>().menuConnection = this;
        }

        hostFailMessage.text = "";
        joinFailMessage.text = "";
        onJoinLobby.Invoke();
        RefreshLobby();
    }

    // �κ� ����Ʈ ������Ʈ
    public async Task UpdateLobbyListFromSteam()
    {
        LobbyList = await SteamMatchmaking.LobbyList.FilterDistanceWorldwide().RequestAsync();
    }

    // �� ��� ���ΰ�ħ
    public async void RefreshLobby()
    {
        foreach (Transform child in roomListContent)
        {
            Destroy(child.gameObject);
        }

        await UpdateLobbyListFromSteam();

        if (LobbyList.Length > 0)
        {
            RoomInfo room;

            foreach (var session in LobbyList)
            {
                room = Instantiate(roomPrefab, roomListContent).GetComponent<RoomInfo>();
                room.SetRoomInfo(session.GetData("fusion_session"), session.GetData("name"), session.MemberCount, session.MaxMembers);
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

        await UpdateLobbyListFromSteam();

        foreach (var lobby in LobbyList)
        {
            if (lobby.GetData("name") == SessionName)
            {
                await SteamMatchmaking.JoinLobbyAsync(lobby.Id);
                return;
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

        foreach (var lobby in LobbyList)
        {
            if (lobby.GetData("name") == SessionName)
            {
                hostFailMessage.text = $"Already Exists";
                return;
            }
        }

        hostFailMessage.text = $"Wait...";

        var createLobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        string fusionSession = Guid.NewGuid().ToString(); // ���� �ĺ��� ����
        createLobby.Value.SetData("fusion_session", fusionSession);
        createLobby.Value.SetData("name", SessionName); // UI ǥ�ÿ�

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1), LoadSceneMode.Single);

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = fusionSession,
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
            PlayerCount = maxPlayers,
            Scene = sceneInfo
        });

        //await runner.LoadScene("Lobby");
        onSessionConnected.Invoke();
        UIManager.Singleton.UIStack = 0;
    }

    public async void StartClient(Lobby lobby)
    {
        if (runner == null)
        {
            runner = Instantiate(runnerPrefab);
            runner.GetComponent<InputManager>().menuConnection = this;
        }

        //var sceneInfo = new NetworkSceneInfo(); // �� ������ �����ϴ� ����ü��, �ִ� 8���� ���� ���� ����(Addictive)
        //sceneInfo.AddSceneRef(SceneRef.FromIndex(1), LoadSceneMode.Single); // ���Ͼ� �߰�
        joinFailMessage.text = $"Wait...";

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = lobby.GetData("fusion_session"),
            SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
        });

        onSessionConnected.Invoke();
        UIManager.Singleton.UIStack = 0;
    }

    public async void LeaveSession()
    {
        await runner.Shutdown();
        SceneManager.LoadScene("Main");
        onSessionDisconnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        if (GameStateManager.Singleton != null)
        {
            Destroy(GameStateManager.Singleton);
        }
    }

    public async void KickedFromSession()
    {
        await runner.Shutdown();
        SceneManager.LoadScene("Main");
        onSessionDisconnected.Invoke();
        UIManager.Singleton.UIStack = 0;
        UIManager.Singleton.OpenKickedPopup(true);
        Destroy(GameStateManager.Singleton);
    }
}
#endregion*/