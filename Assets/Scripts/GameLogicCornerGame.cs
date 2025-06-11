using Fusion;
using System.Linq;
using UnityEngine;

public class GameLogicCornerGame : NetworkBehaviour
{
    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);

        if (Runner.IsServer)
        {
            GameStateManager.Singleton.SpawnCharacter();
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 로딩 다 될 때까지 대기 후 연출 시작
        if (Runner.IsServer && Runner.IsForward && GameStateManager.Singleton.IsAllLoaded)
        {
            GameStateManager.Singleton.IsAllLoaded = false;
            // rpc로 연출 시작 신호
        }
    }
}
