using Fusion.Addons.KCC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projector : MonoBehaviour, IInteractable
{
    public void Interact(Player player)
    {
        player.OpenProjector();
    }
}
