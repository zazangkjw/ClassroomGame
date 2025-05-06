using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

public class LobbyGameLogic : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private Collider[] readyChairColliders;
    [SerializeField] private Light thunderLight;
    [SerializeField] private AudioSource thunderSfx;
    [SerializeField] private VideoPlayer countdownVideo;

    [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, Player> Players => default;

    private WaitForSeconds oneSeconds = new WaitForSeconds(1);
    private WaitForSeconds thunderDelay1 = new WaitForSeconds(5f);
    private WaitForSeconds thunderDelay2 = new WaitForSeconds(0.1f);
    private bool isCountdown;

    private void Start()
    {
        StartCoroutine(ThunderRoutine());
        countdownVideo.loopPointReached += OnCountdownEnd;
    }

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);
    }

    public override void FixedUpdateNetwork()
    {
        if (Players.Count < 1)
            return;

        if (!Runner.IsResimulation && UIManager.Singleton.LeaderboardScreen.activeSelf)
            UIManager.Singleton.UpdateLeaderboard(Players.ToArray());

        if (HasStateAuthority && Runner.IsForward)
        {
            CheckReady();
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
        if (!HasStateAuthority)
            return;

        if (Players.TryGet(player, out Player playerBehaviour))
        {
            Players.Remove(player);
            Runner.Despawn(playerBehaviour.Object);
        }
    }

    public void CheckReady()
    {
        foreach (var player in Players)
        {
            if (!player.Value.IsReady)
            {
                if(isCountdown)
                {
                    isCountdown = false;
                    RPC_EndCountDown();
                }

                return;
            }
        }

        if (!isCountdown)
        {
            isCountdown = true;
            RPC_StartCountDown();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority | RpcTargets.Proxies)]
    private void RPC_StartCountDown()
    {
        countdownVideo.gameObject.SetActive(true);
        countdownVideo.Play();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority | RpcTargets.Proxies)]
    private void RPC_EndCountDown()
    {
        countdownVideo.gameObject.SetActive(false);
        countdownVideo.Stop();
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
            Debug.Log("¾À ÀüÈ¯");
        }
    }

    private IEnumerator ThunderRoutine()
    {
        while (true)
        {
            yield return thunderDelay1;
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
        }
    }
}
