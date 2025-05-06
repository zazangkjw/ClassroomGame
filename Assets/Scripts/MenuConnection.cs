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
    public List<SessionInfo> SessionList = new List<SessionInfo>();
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
        runner = Instantiate(runnerPrefab);

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
        hostFailMessage.text = "";
        joinFailMessage.text = "";

        if (runner == null)
        {
            runner = Instantiate(runnerPrefab);
            runner.GetComponent<InputManager>().menuConnection = this;
        }

        DoRefresh = true;
        var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
        onJoinLobby.Invoke();
    }

    // 방 목록 새로고침
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
                room.SetRoomInfo(session.Name, session.PlayerCount, session.MaxPlayers);
            }
        }
    }

    public async void StartHost()
    {
        SessionName = hostRoomName.text;

        if (SessionList.Exists(s => s.Name == SessionName))
        {
            hostFailMessage.text = $"Already Exists";
            return;
        }

        if (!SessionName.IsNullOrEmpty())
        {
            //var sceneInfo = new NetworkSceneInfo();
            //sceneInfo.AddSceneRef(SceneRef.FromIndex(1), LoadSceneMode.Single);
            hostFailMessage.text = $"Wait...";

            await runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = this.SessionName,
                SceneManager = runner.GetComponent<NetworkSceneManagerDefault>(),
                //Scene = sceneInfo
            });

            await runner.LoadScene("Lobby");
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
            //var sceneInfo = new NetworkSceneInfo(); // 씬 정보를 저장하는 구조체로, 최대 8개의 씬을 저장 가능(Addictive)
            //sceneInfo.AddSceneRef(SceneRef.FromIndex(1), LoadSceneMode.Single); // 단일씬 추가
            joinFailMessage.text = $"Wait...";

            await runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = this.SessionName,
                SceneManager = runner.GetComponent<INetworkSceneManager>(),
                //Scene = sceneInfo
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
    }
}
