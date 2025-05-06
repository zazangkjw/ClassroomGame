using Fusion.Addons.KCC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReadyChair : MonoBehaviour, IInteractable
{
    [SerializeField] Transform target;

    private Player owner;

    public void Interact(Player player)
    {
        if (owner == null && !player.IsReady)
        {
            owner = player;
            player.IsReady = true;
            player.SetBlockMovement(true);
            player.Teleport(transform.position.OnlyXZ(), Quaternion.LookRotation(target.position.OnlyXZ() - player.CamTarget.position.OnlyXZ()));
        }
        else if (owner == player)
        {
            owner = null;
            player.IsReady = false;
            player.SetBlockMovement(false);
            player.Teleport(transform.position.OnlyXZ() - transform.forward * 0.5f, transform.rotation);
        }
    }
}
