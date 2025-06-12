using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable_LightSwitch : Interactable
{
    public override string interactableName => "";

    public override void InteractLocal(Player player)
    {
        if (player.HasInputAuthority && !player.IsReady)
        {
            UIManager.Singleton.PlayOnClickSound();
            UIManager.Singleton.OpenProjector(true);
        }
    }
}
