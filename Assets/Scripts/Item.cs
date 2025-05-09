using Fusion;
using UnityEngine;

public class Item : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(Hide))] public bool IsHideVisual { get; set; }
    [Networked, OnChangedRender(nameof(Hide))] public bool IsHideCollider { get; set; }

    [SerializeField] private MeshRenderer[] visuals;
    [SerializeField] private Collider[] colliders;

    public Sprite itemImage;
    public virtual string itemName => "E";

    public override void Spawned()
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
