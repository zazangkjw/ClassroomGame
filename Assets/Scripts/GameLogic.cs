using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class GameLogic : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private Collider[] readyChairColliders;
    [SerializeField] private Light thunderLight;

    [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, Player> Players => default;

    private WaitForSeconds oneSeconds = new WaitForSeconds(1);
    private WaitForSeconds thunderDelay1 = new WaitForSeconds(5f);
    private WaitForSeconds thunderDelay2 = new WaitForSeconds(0.1f);
    private Coroutine countDownRoutine;
    private TextMeshProUGUI countdownText;

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);
        countdownText = UIManager.Singleton.CountdownText;
        StartCoroutine(ThunderRoutine());
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
                if(countDownRoutine != null)
                {
                    RPC_EndCountDown();
                }

                return;
            }
        }

        if (countDownRoutine == null)
        {
            RPC_StartCountDown();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority | RpcTargets.Proxies)]
    private void RPC_StartCountDown()
    {
        if (countDownRoutine != null)
        {
            StopCoroutine(countDownRoutine);
            countDownRoutine = null;
        }
        countDownRoutine = StartCoroutine(CountDownRoutine());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority | RpcTargets.Proxies)]
    private void RPC_EndCountDown()
    {
        if (countDownRoutine != null)
        {
            StopCoroutine(countDownRoutine);
            countDownRoutine = null;
            countdownText.text = "";
        }
    }

    private IEnumerator CountDownRoutine()
    {
        for(int i = 5; i >= 0; i--)
        {
            countdownText.text = i.ToString();
            yield return oneSeconds;
        }

        countdownText.text = "";

        // ÄÆ¾À
        foreach (var col in readyChairColliders)
        {
            col.enabled = false;
        }

        // ¾À ÀüÈ¯
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
        }
    }
}
