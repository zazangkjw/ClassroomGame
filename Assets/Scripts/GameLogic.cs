using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameLogic : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, Player> Players => default;

    private WaitForSeconds oneSeconds = new WaitForSeconds(1);
    private Coroutine countDownRoutine;

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
        }
    }

    private IEnumerator CountDownRoutine()
    {
        Debug.Log("5");
        yield return oneSeconds;
        Debug.Log("4");
        yield return oneSeconds;
        Debug.Log("3");
        yield return oneSeconds;
        Debug.Log("2");
        yield return oneSeconds;
        Debug.Log("1");
        yield return oneSeconds;
        Debug.Log("0");
        // 컷씬(호스트 씬의 모든 의자 collider를 비활성화 해서 계속 앉아 있게 하기)
        // 씬 전환
    }
}
