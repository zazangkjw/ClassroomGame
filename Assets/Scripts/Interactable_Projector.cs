using Fusion.Addons.KCC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable_Projector : Interactable
{
    public override string interactableName => "Select Video";

    public override void InteractLocal(Player player)
    {
        if (player.HasInputAuthority)
        {
            UIManager.Singleton.PlayOnClickSound();
            UIManager.Singleton.OpenProjector(true);
        }
    }
}
