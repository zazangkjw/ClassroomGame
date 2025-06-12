using Fusion;
using Unity.VisualScripting;
using UnityEngine;

public class GameLogicCornerGame : NetworkBehaviour
{
    [Networked] public bool IsStarted { get; set; }

    [SerializeField] private Corner[] corners;

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);

        if (Runner.IsServer)
        {
            GameStateManager.Singleton.SpawnCharacter();

            // 플레이어에게 컴포넌트 추가
            foreach(var player in GameStateManager.Singleton.Players)
            {
                player.Value.AddComponent<CornerGamePlayer>();
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner.IsServer)
        {
            // 로딩 다 될 때까지 대기 후 연출 시작
            if (Runner.IsForward && GameStateManager.Singleton.IsAllLoaded)
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
        }
    }
}
