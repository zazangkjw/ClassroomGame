using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable: MonoBehaviour
{
    public virtual string interactableName => "E";

    public virtual void InteractServer(Player player) { }
    public virtual void InteractLocal(Player player) { }
}
