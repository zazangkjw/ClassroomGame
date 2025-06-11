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
        // �ε� �� �� ������ ��� �� ���� ����
        if (Runner.IsServer && Runner.IsForward && GameStateManager.Singleton.IsAllLoaded)
        {
            GameStateManager.Singleton.IsAllLoaded = false;
            // rpc�� ���� ���� ��ȣ
        }
    }
}
