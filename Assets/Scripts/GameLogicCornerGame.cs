using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class GameLogicCornerGame : NetworkBehaviour
{
    public NetworkPrefabRef PlayerPrefab;
    public NetworkPrefabRef NPC;
    public Corner[] Corners;

    [Networked] public bool IsStarted { get; set; }

    [SerializeField] private List<NavMeshAgent> npcs = new();

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);

        if (Runner.IsServer && Runner.IsForward)
        {
            GameStateManager.Singleton.SpawnCharacter(PlayerPrefab);

            for (int i = 0; i < 5 - Runner.ActivePlayers.Count(); i++)
            {
                NetworkObject npc = Runner.Spawn(NPC, Vector3.up * 2, Quaternion.identity);
                npcs.Add(npc.GetComponent<NavMeshAgent>());
                npc.GetComponent<CornerGamePlayer>()._GameLogicCornerGame = this;
                npc.GetComponent<CornerGamePlayer>().Goal = 3 - i;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner.IsServer && Runner.IsForward)
        {
            CheckAllLoaded();

            if (!IsStarted)
            {
                CheckCornerIsFull();
            }
        }
    }

    private void CheckAllLoaded()
    {
        // 로딩 다 될 때까지 대기 후 연출 시작
        if (GameStateManager.Singleton.IsAllLoaded)
        {
            GameStateManager.Singleton.IsAllLoaded = false;
        }
    }

    private void CheckCornerIsFull()
    {
        foreach (var corner in Corners)
        {
            if (corner.ThisCornerPlayers.Count == 0)
            {
                return;
            }
        }

        if (Corners[0].ThisCornerPlayers.Count >= 2)
        {
            Debug.Log("게임 시작");
            IsStarted = true;
            if (!Corners[0].ThisCornerPlayers[0].NPC)
            {
                Corners[0].ThisCornerPlayers[0].Goal = 1;
                Corners[0].ThisCornerPlayers[0].IsTagger = true;
            }
            else
            {
                Corners[0].ThisCornerPlayers[1].Goal = 1;
                Corners[0].ThisCornerPlayers[1].IsTagger = true;
            }

            foreach (var corner in Corners)
            {
                corner.CornerText.gameObject.SetActive(false);
            }

            RPC_HideCornerText();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_HideCornerText()
    {
        foreach (var corner in Corners)
        {
            corner.CornerText.gameObject.SetActive(false);
        }
    }
}
