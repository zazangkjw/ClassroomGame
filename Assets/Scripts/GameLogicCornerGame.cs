using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class GameLogicCornerGame : NetworkBehaviour
{
    public NetworkPrefabRef PlayerPrefab;
    public NetworkPrefabRef NPC;

    [Networked] public bool IsStarted { get; set; }

    [SerializeField] private Corner[] corners;

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

                // 네브메쉬 각 코너로 이동. 0번은 3번코너, 1번은 2번코너, 2번은 1번코너, 3번은 0번코너
                for (int i = 0; i < npcs.Count; i++)
                {
                    npcs[i].SetDestination(corners[3 - i].transform.position);
                }
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
        foreach (var corner in corners)
        {
            if (corner.ThisCornerPlayers.Count == 0)
            {
                return;
            }
        }

        if (corners[0].ThisCornerPlayers.Count >= 2)
        {
            Debug.Log("게임 시작");
            IsStarted = true;
            if (!corners[0].ThisCornerPlayers[0].NPC)
            {
                corners[0].ThisCornerPlayers[0].Goal = 1;
                corners[0].ThisCornerPlayers[0].IsTagger = true;
            }
            else
            {
                corners[0].ThisCornerPlayers[1].Goal = 1;
                corners[0].ThisCornerPlayers[1].IsTagger = true;
            }

            foreach (var corner in corners)
            {
                corner.CornerText.gameObject.SetActive(false);
            }

            RPC_HideCornerText();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    private void RPC_HideCornerText()
    {
        foreach (var corner in corners)
        {
            corner.CornerText.gameObject.SetActive(false);
        }
    }
}
