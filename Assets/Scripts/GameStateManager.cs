using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameStateManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static GameStateManager Singleton;

    public NetworkPrefabRef PlayerPrefab;
    public List<ulong> AuthenticatedPlayers = new();
    public bool IsAllLoaded;
    public byte SceneLoadDoneCount;

    [Networked] public byte SelectedVideoIndex { get; set; }
    [Networked, Capacity(10)] public NetworkDictionary<PlayerRef, NetworkObject> Players => default;

    public override void Spawned()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }

        Singleton = this;
        DontDestroyOnLoad(gameObject);

        Runner.SetIsSimulated(Object, true);
    }

    public override void FixedUpdateNetwork()
    {
        if (Players.Count >= 1)
        {
            if (Runner.IsForward && UIManager.Singleton.LeaderboardScreen.activeSelf)
            {
                UIManager.Singleton.UpdateLeaderboard(Players.ToArray());
            }
        }
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (HasStateAuthority)
        {
            NetworkObject playerObject = Runner.Spawn(PlayerPrefab, Vector3.up * 2, Quaternion.identity, player);
            Players.Add(player, playerObject);
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (HasStateAuthority)
        {
            if (Players.TryGet(player, out NetworkObject playerObject))
            {
                Players.Remove(player);
                Runner.Despawn(playerObject);
            }

            if (player != Runner.LocalPlayer)
            {
                Debug.Log("플레이어 나가서 로비로 복귀");
                UIManager.Singleton._MenuConnection.CurrentLobby.SetPublic();
                Runner.LoadScene("Lobby");
            }
        }
    }

    public void SpawnCharacter(NetworkPrefabRef prefabRef)
    {
        if (HasStateAuthority)
        {
            foreach (KeyValuePair<PlayerRef, NetworkObject> i in Players)
            {
                if (i.Value == null)
                {
                    NetworkObject playerObject = Runner.Spawn(prefabRef, Vector3.up * 2, Quaternion.identity, i.Key);
                    Players.Set(i.Key, playerObject);
                }
            }
        }
    }

    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority)]
    public void RPC_SceneLoadDone()
    {
        SceneLoadDoneCount++;
        if (SceneLoadDoneCount >= Runner.ActivePlayers.Count())
        {
            IsAllLoaded = true;
            Debug.Log("모두 로딩 완료");
        }
    }
}
