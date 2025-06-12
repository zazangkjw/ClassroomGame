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

            // �÷��̾�� ������Ʈ �߰�
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
            // �ε� �� �� ������ ��� �� ���� ����
            if (Runner.IsForward && GameStateManager.Singleton.IsAllLoaded)
            {
                GameStateManager.Singleton.IsAllLoaded = false;
                // rpc�� ���� ���� ��ȣ
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
            Debug.Log("���� ����");
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
