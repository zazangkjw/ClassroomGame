using Fusion;
using Fusion.Addons.KCC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Player : NetworkBehaviour
{
    [SerializeField] private MeshRenderer[] modelParts;
    [SerializeField] private LayerMask lagCompLayers;
    [SerializeField] private KCC kcc;
    [SerializeField] private Transform camTarget;
    [SerializeField] private AudioSource source;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private Vector3 jumpImpulse = new(0f, 10f, 0f);
    [SerializeField] private float interactionRange = 5f;
    [SerializeField] private List<Item> inventory = new();
    [SerializeField] private byte inventorySize = 12;
    [SerializeField] private Transform hand;

    public Queue<byte> DropItemIndexQueue = new();

    [Networked] public string Name { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync { get; set; }

    private InputManager inputManager;
    private Vector2 baseLookRotation;
    private byte currentQuickSlotIndex;
    private bool equipItemFlag;
    private Transform itemCategory;
  
    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            itemCategory = GameObject.Find("Items").transform;
        }

        if (HasInputAuthority || HasStateAuthority)
        {
            inventory = Enumerable.Repeat<Item>(null, inventorySize).ToList();
        }

        if (HasInputAuthority)
        {
            foreach (MeshRenderer renderer in modelParts)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            inputManager = Runner.GetComponent<InputManager>();
            Name = UIManager.Singleton.Name;
            RPC_PlayerName(Name);
            CameraFollow.Singleton.SetTarget(camTarget, this);
            UIManager.Singleton.LocalPlayer = this;

            for (byte i = 0; i < inventorySize; i++)
            {
                if (inventory[i] != null)
                {
                    UIManager.Singleton.UpdateItemSlot(i, inventory[i].itemImage);
                }
                else
                {
                    UIManager.Singleton.UpdateItemSlot(i, null);
                }
            }  
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            CameraFollow.Singleton.SetTarget(null, this);
            UIManager.Singleton.LocalPlayer = null;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasInputAuthority)
        {
            CheckDropItem();
        }

        if (GetInput(out NetInput input))
        {
            CheckJump(input);
            kcc.AddLookRotation(input.LookDelta * lookSensitivity, -maxPitch, maxPitch);
            UpdateCamTarget();
            Vector3 lookDirection = camTarget.forward;
            CheckInteraction(input, lookDirection);
            CheckCurrentQuickSlot(input);

            SetInputDirection(input);
            PreviousButtons = input.Buttons;
            baseLookRotation = kcc.GetLookRotation();
        }
    }

    public override void Render()
    {
        if (kcc.IsPredictingLookRotation)
        {
            Vector2 predictedLookRotation = baseLookRotation + inputManager.AccumulatedMouseDelta * lookSensitivity;
            kcc.SetLookRotation(predictedLookRotation, -maxPitch, maxPitch);
        }
        UpdateCamTarget();
    }

    private void CheckJump(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Jump))
        {
            if (kcc.FixedData.IsGrounded)
            {
                kcc.Jump(jumpImpulse);
                JumpSync++;
            }
        }
    }

    private void SetInputDirection(NetInput input)
    {
        Vector3 worldDirection;
        worldDirection = kcc.FixedData.TransformRotation * input.Direction.X0Y();
        kcc.SetInputDirection(worldDirection);
    }

    private void UpdateCamTarget()
    {
        camTarget.localRotation = Quaternion.Euler(kcc.GetLookRotation().x, 0f, 0f);
    }

    private void Jumped()
    {
        source.Play();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PlayerName(string name)
    {
        Name = name;
    }

    // ��ȣ�ۿ�
    private void CheckInteraction(NetInput input, Vector3 lookDirection)
    {
        if (!HasStateAuthority || !Runner.IsForward || !input.Buttons.WasPressed(PreviousButtons, InputButton.Interaction))
        {
            return;
        }

        if (Physics.Raycast(camTarget.position, lookDirection, out RaycastHit hitInfo, interactionRange))
        {
            // ������ �ݱ�
            if (hitInfo.collider.TryGetComponent(out Item item))
            {
                GetItem(item);
            }
        }
    }

    // ������ ȹ��
    private void GetItem(Item item)
    {
        // ���� ���õ� �������� ��������� �ش� �����Կ� ������ ȹ��
        if (inventory[currentQuickSlotIndex] == null)
        {
            RPC_GetItem(currentQuickSlotIndex, item.GetComponent<NetworkObject>());
            return;
        }

        // �κ��丮�� ������ ȹ��
        for (byte i = 0; i < inventory.Count; i++)
        {
            if (inventory[i] == null)
            {
                RPC_GetItem(i, item.GetComponent<NetworkObject>());
                return;
            }
        }

        // �κ��丮 ���� �޽��� (RpcTargets.InputAuthority)
    }

    // ������ ���� �ٲٱ�
    public void SwitchItem(byte index1, byte index2)
    {
        RPC_SwitchSlot(index1, index2);
    }

    // ������ ������
    public void DropItem(byte index)
    {
        DropItemIndexQueue.Enqueue(index);
    }

    // ���� ������ ��⿭
    private void CheckDropItem()
    {
        if (!Runner.IsForward)
        {
            return;
        }

        while (DropItemIndexQueue.Count > 0)
        {
            RPC_DropItem(DropItemIndexQueue.Dequeue());
        }
    }

    // ������ ȹ�� RPC
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    private void RPC_GetItem(byte index, NetworkObject item)
    {
        inventory[index] = item.GetComponent<Item>();

        if (HasStateAuthority)
        {
            inventory[index].IsHideVisual = true;
            inventory[index].IsHideCollider = true;
            inventory[index].Hide(); // ȣ��Ʈ�� ƽ �ܰ迡�� �������� ��� ���� 
            StartCoroutine(AttachOnParent(inventory[index], hand, hand));
            if (index == currentQuickSlotIndex)
                equipItemFlag = true;
        }
        
        if (HasInputAuthority)
        {
            UIManager.Singleton.UpdateItemSlot(index, inventory[index].itemImage);
        }
    }

    // ������ ���� �ٲٱ� RPC
    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    private void RPC_SwitchSlot(byte index1, byte index2)
    {
        (inventory[index1], inventory[index2]) = (inventory[index2], inventory[index1]);
        if (HasStateAuthority && (index1 == currentQuickSlotIndex || index2 == currentQuickSlotIndex))
        {
            equipItemFlag = true;
        }
    }

    // ������ ������ RPC
    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    private void RPC_DropItem(byte index)
    {
        if (HasStateAuthority)
        {
            inventory[index].IsHideVisual = false;
            inventory[index].IsHideCollider = false;
            inventory[index].Hide();
            StartCoroutine(AttachOnParent(inventory[index], itemCategory, hand));
            inventory[index] = null;
            if (index == currentQuickSlotIndex)
            {
                equipItemFlag = true;
            }
        }
        else
            inventory[index] = null;
    }

    // �θ� ���� ��, 1������ ����ϰ� ��ġ ����
    private IEnumerator AttachOnParent(Item item, Transform parent, Transform point)
    {
        item.transform.SetParent(parent); // Network Transform ������Ʈ�� Sync Parent üũ
        yield return null;
        item.GetComponent<NetworkTransform>().Teleport(point.position, point.rotation); // ���� ������. �̰� �� �ϸ� ��ġ ���� ��߳�
    }

    // equipItemFlag�� true�� ������ ������ ǥ��
    private void CheckCurrentQuickSlot(NetInput input)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (input.CurrentQuickSlotIndex != currentQuickSlotIndex)
        {
            currentQuickSlotIndex = input.CurrentQuickSlotIndex;
            equipItemFlag = true;
        }

        if(equipItemFlag)
        {
            EquipItem(currentQuickSlotIndex);
            equipItemFlag = false;
        }
    }

    // ������ ����
    private void EquipItem(int index)
    {
        for (byte i = 0; i < inventorySize; i++)
        {
            if (inventory[i] != null)
            {
                inventory[i].IsHideVisual = true;
                inventory[i].Hide();
            }
        }

        if (inventory[index] != null)
        {
            inventory[index].IsHideVisual = false;
            inventory[index].Hide();
        }
    }

    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayerSendMessage(string message)
    {
        UIManager.Singleton.UpdateChat($"{Name}: {message}");
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    public void RPC_KickPlayer(PlayerRef playerRef)
    {
        if (Runner.LocalPlayer == playerRef) { UIManager.Singleton._MenuConnection.LeaveSession(); }
    }
}
