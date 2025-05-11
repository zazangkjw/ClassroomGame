using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class GameLogicLobby : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private Collider[] readyChairColliders;
    [SerializeField] private Light thunderLight;
    [SerializeField] private AudioSource thunderSfx;
    [SerializeField] private VideoPlayer countdownVideo;
    [SerializeField] private TextMeshProUGUI projectorText;

    [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, Player> Players => default;
    [Networked, OnChangedRender(nameof(Thunder))] private bool ThunderSync { get; set; }
    [Networked, OnChangedRender(nameof(CountDown))] private bool IsCountdown { get; set; }

    private Coroutine thunderRoutine;
    private WaitForSeconds oneSeconds = new WaitForSeconds(1);
    private WaitForSeconds thunderDelay1 = new WaitForSeconds(5f);
    private WaitForSeconds thunderDelay2 = new WaitForSeconds(0.1f);

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);
        countdownVideo.loopPointReached += OnCountdownEnd;

        if (HasStateAuthority)
        {
            StartCoroutine(WaitThunderRoutine());
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Players.Count >= 1)
        {
            if (Runner.IsForward && UIManager.Singleton.LeaderboardScreen.activeSelf)
            {
                UIManager.Singleton.UpdateLeaderboard(Players.ToArray());
            }

            if (HasStateAuthority && Runner.IsForward)
            {
                CheckReady();
            }

            UpdateProjectorText();
        }
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (HasStateAuthority)
        {
            NetworkObject playerObject = Runner.Spawn(playerPrefab, Vector3.up * 2, Quaternion.identity, player);
            Players.Add(player, playerObject.GetComponent<Player>());
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (HasStateAuthority)
        {
            if (Players.TryGet(player, out Player playerBehaviour))
            {
                Players.Remove(player);
                Runner.Despawn(playerBehaviour.Object);
            }
        }
    }

    public void CheckReady()
    {
        foreach (var player in Players)
        {
            if (!player.Value.IsReady)
            {
                if (IsCountdown)
                {
                    IsCountdown = false;
                    CountDown();
                }

                return;
            }
        }
        
        if (!IsCountdown)
        {
            IsCountdown = true;
            CountDown();
        }
    }

    private void CountDown()
    {
        if (IsCountdown)
        {
            projectorText.gameObject.SetActive(false);
            countdownVideo.gameObject.SetActive(true);
            countdownVideo.Play();
        }
        else
        {
            projectorText.gameObject.SetActive(true);
            countdownVideo.gameObject.SetActive(false);
            countdownVideo.Stop();
        }
    }

    private void OnCountdownEnd(VideoPlayer vp)
    {
        foreach (var col in readyChairColliders)
        {
            col.enabled = false;
        }

        // ¾À ÀüÈ¯
        if (HasStateAuthority)
        {
            Debug.Log(GameStateManager.Singleton.SelectedVideoIndex);
            //SceneManager.LoadScene(UIManager.Singleton.VideoListOriginal[GameStateManager.Singleton.SelectedVideoIndex]);
        }
    }

    private IEnumerator WaitThunderRoutine()
    {
        while (true)
        {
            yield return thunderDelay1;
            ThunderSync = !ThunderSync;
            Thunder();
        }
    }

    private void Thunder()
    {
        if (thunderRoutine != null)
        {
            StopCoroutine(thunderRoutine);
        }
        thunderRoutine = StartCoroutine(ThunderRoutine());
    }

    private IEnumerator ThunderRoutine()
    {
        thunderLight.enabled = true;
        yield return thunderDelay2;
        yield return thunderDelay2;
        yield return thunderDelay2;
        thunderLight.enabled = false;
        yield return thunderDelay2;
        thunderLight.enabled = true;
        yield return thunderDelay2;
        yield return thunderDelay2;
        thunderLight.enabled = false;

        yield return oneSeconds;
        thunderSfx.Play();
        thunderRoutine = null;
    }

    private void UpdateProjectorText()
    {
        if (projectorText.text != UIManager.Singleton.VideoList[GameStateManager.Singleton.SelectedVideoIndex])
        {
            projectorText.text = UIManager.Singleton.VideoList[GameStateManager.Singleton.SelectedVideoIndex];
        }
    }
}
