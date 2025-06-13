using Fusion;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Windows;

public class CornerGamePlayer : NetworkBehaviour
{
    public bool IsTagger;
    public int Goal;
    public bool NPC;

    [Networked] private bool IsWalking { get; set; }

    [SerializeField] private NavMeshAgent nav;
    [SerializeField] private Animator anim;

    public override void Spawned()
    {
        if (NPC)
        {
            Runner.SetIsSimulated(Object, true);
            if (!Runner.IsServer)
            {
                nav.updatePosition = false;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (NPC)
        {
            if (Runner.IsServer)
            {
                //Vector3 direction = transform.InverseTransformDirection(nav.velocity); // Vector3 direction = Quaternion.Inverse(transform.rotation) * direction;
                IsWalking = nav.velocity == Vector3.zero ? false : true;
            }

            anim.SetInteger("Direction", IsWalking ? 1 : 0);
        }
    }
}
