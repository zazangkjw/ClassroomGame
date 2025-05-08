using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Singleton;

    [Networked] public byte SelectedVideoIndex { get; set; }

    public override void Spawned()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }

        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }
}
