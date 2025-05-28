using Fusion;
using System.Linq;
using UnityEngine;

public class GameLogicOctopus : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, Player> Players => default;

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);
    }

    public override void FixedUpdateNetwork()
    {
        if (Players.Count < 1)
        {
            return;
        }

        if (!Runner.IsResimulation && UIManager.Singleton.LeaderboardScreen.activeSelf)
        {
            UIManager.Singleton.UpdateLeaderboard(Players.ToArray());
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
        {
            return;
        }

        if (Players.TryGet(player, out Player playerBehaviour))
        {
            Players.Remove(player);
            Runner.Despawn(playerBehaviour.Object);
        }
    }
}
