using Fusion;
using UnityEngine;

public class Item : NetworkBehaviour
{
    [Networked] public bool IsHideVisual { get; set; }
    [Networked] public bool IsHideCollider { get; set; }

    [SerializeField] private MeshRenderer[] visuals;
    [SerializeField] private Collider[] colliders;

    public Sprite itemImage;

    public override void Render()
    {
        Hide();
    }

    public void Hide()
    {
        foreach (var visual in visuals)
            visual.enabled = !IsHideVisual;

        foreach (var collider in colliders)
            collider.enabled = !IsHideCollider;
    }
}
