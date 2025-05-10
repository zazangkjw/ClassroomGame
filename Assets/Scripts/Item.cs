using Fusion;
using System;
using UnityEngine;

public class Item : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(Hide))] public bool IsHideVisual { get; set; }
    [Networked, OnChangedRender(nameof(Hide))] public bool IsHideCollider { get; set; }

    [SerializeField] private MeshRenderer[] visuals;
    [SerializeField] private Collider[] colliders;

    public Sprite ItemImage;
    public virtual string ItemName => "E";

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

    public virtual void InteractServer(Player player)
    {
        if (player.HasStateAuthority)
        {
            int index = -1;

            // ���� ���õ� �������� ��������� �ش� �����Կ� ������ ȹ��
            if (player.Inventory[player.CurrentQuickSlotIndex] == null)
            {
                index = player.CurrentQuickSlotIndex;
            }
            // �κ��丮�� ������ ȹ��
            else
            {
                for (byte i = 0; i < player.Inventory.Count; i++)
                {
                    if (player.Inventory[i] == null)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index == -1)
            {
                if (player.HasInputAuthority)
                {
                    // "�κ��丮�� ���� á���ϴ�."
                }
                else
                {
                    player.RPC_InventoryFull();
                }
                return;
            }

            if (player.HasInputAuthority)
            {
                InteractLocal(player, index);
            }
            else
            {
                player.RPC_PickUpItem((byte)index, GetComponent<NetworkObject>());
            }

            player.Inventory[index] = this;
            IsHideVisual = true;
            IsHideCollider = true;
            Hide();
            StartCoroutine(player.AttachOnParent(this, player.Hand, player.Hand));
            player.EquipItemFlag = true;
        }
    }

    public virtual void InteractLocal(Player player, int index)
    {
        if (player.HasInputAuthority)
        {
            UIManager.Singleton.UpdateItemSlot(index, ItemImage);
        }
    }
}
