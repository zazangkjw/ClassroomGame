using Fusion;
using UnityEngine;

public class GameLogicCornerGame : NetworkBehaviour
{
    public NetworkPrefabRef PlayerPrefab;

    [Networked] public bool IsStarted { get; set; }

    [SerializeField] private Corner[] corners;

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);

        if (Runner.IsServer && Runner.IsForward)
        {
            GameStateManager.Singleton.SpawnCharacter(PlayerPrefab);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner.IsServer && Runner.IsForward)
        {
            // 로딩 다 될 때까지 대기 후 연출 시작
            if (GameStateManager.Singleton.IsAllLoaded)
            {
                GameStateManager.Singleton.IsAllLoaded = false;
                // rpc로 연출 시작 신호
            }

            if (!IsStarted)
            {
                CheckCornerIsFull();
            }
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
            corners[0].ThisCornerPlayers[0].Goal = 1;
            corners[0].ThisCornerPlayers[0].IsTagger = true;

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
