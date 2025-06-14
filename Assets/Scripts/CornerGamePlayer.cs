using Fusion;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Windows;

public class CornerGamePlayer : NetworkBehaviour
{
    public bool IsTagger;
    public int Goal;
    public bool NPC;
    public GameLogicCornerGame _GameLogicCornerGame;

    [Networked] private bool IsWalking { get; set; }

    [SerializeField] private NavMeshAgent nav;
    [SerializeField] private Animator anim;

    public override void Spawned()
    {
        if (NPC)
        {
            Runner.SetIsSimulated(Object, true);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (NPC)
        {
            if (Runner.IsServer)
            {
                if (!_GameLogicCornerGame.IsStarted)
                {
                    // �׺�޽� �� �ڳʷ� �̵�. 0���� 3���ڳ�, 1���� 2���ڳ�, 2���� 1���ڳ�, 3���� 0���ڳ�
                    nav.SetDestination(_GameLogicCornerGame.Corners[Goal].transform.position);
                }
                else
                {
                    if (IsTagger)
                    {
                        nav.SetDestination(_GameLogicCornerGame.Corners[Goal].transform.position);
                    }
                }

                //Vector3 direction = transform.InverseTransformDirection(nav.velocity); // Vector3 direction = Quaternion.Inverse(transform.rotation) * direction;
                IsWalking = nav.velocity == Vector3.zero ? false : true;
            }

            anim.SetInteger("Direction", IsWalking ? 1 : 0);
        }
    }
}
