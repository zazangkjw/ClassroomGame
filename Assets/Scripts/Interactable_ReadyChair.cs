using Fusion.Addons.KCC;
using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Interactable_ReadyChair : Interactable
{
    [SerializeField] Transform target;
    [SerializeField] Transform unreadyPoint;

    private Player owner;

    public override string interactableName => "Sit"; // UIManager가 읽어온 언어 파일을 통해 현재 언어에 맞게 받아오기
    
    public override void InteractServer(Player player)
    {
        if (player.HasStateAuthority)
        {
            if (owner == null && !player.IsReady)
            {
                owner = player;
                player.IsReady = true;
                player.IsBlockMovement = true;
                player.IsHideCollider = true;
                player.Hide();
                player.TeleportQueue.Enqueue((transform.position.OnlyXZ(), unreadyPoint.rotation, false, false));
            }
            else if (owner == player && player.IsReady)
            {
                owner = null;
                player.IsReady = false;
                player.IsBlockMovement = false;
                player.IsHideCollider = false;
                player.Hide();
                player.TeleportQueue.Enqueue((unreadyPoint.position, unreadyPoint.rotation, false, false));
            }
        }
    }

    public override void InteractLocal(Player player)
    {
        if (player.HasInputAuthority)
        {
            UIManager.Singleton.PlayOnClickSound();
        }
    }
}
