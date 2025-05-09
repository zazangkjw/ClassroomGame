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
    [SerializeField] private AudioSource source;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private Vector3 jumpImpulse = new(0f, 10f, 0f);
    [SerializeField] private float interactionRange = 5f;
    [SerializeField] private List<Item> inventory = new();
    [SerializeField] private byte inventorySize = 12;
    [SerializeField] private Transform hand;

    public KCC Kcc;
    public Transform CamTarget;
    public Queue<byte> DropItemIndexQueue = new();
    public bool IsReady;

    [Networked] public string Name { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync { get; set; }
    [Networked, OnChangedRender(nameof(Hide))] public bool IsHideCollider { get; set; }
    [Networked] public bool IsBlockMovement { get; set; }

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
                renderer.enabled = false;
                //renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            inputManager = Runner.GetComponent<InputManager>();
            Name = UIManager.Singleton.Name;
            RPC_PlayerName(Name);
            CameraFollow.Singleton.SetTarget(CamTarget, this);
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
        // ȣ��Ʈ, �Է� ���� ���� Ŭ���̾�Ʈ �� �� �����
        if (GetInput(out NetInput input))
        {
            if (!IsBlockMovement)
            {
                CheckJump(input);
                SetInputDirection(input);
            }
            else
            {
                Kcc.SetInputDirection(Vector3.zero); // �̵� ���߱�

                // ���� ���̸� ������ �����ϰ� �����
                if (!Kcc.FixedData.IsGrounded && Kcc.FixedData.DynamicVelocity.y > 0f)
                {
                    Vector3 velocity = Kcc.FixedData.DynamicVelocity;
                    velocity.y = 0f;
                    Kcc.SetDynamicVelocity(velocity);
                }
            }

            Kcc.AddLookRotation(input.LookDelta * lookSensitivity, -maxPitch, maxPitch);
            UpdateCamTarget();
            Vector3 lookDirection = CamTarget.forward;
            CheckInteraction(input, lookDirection);
            CheckCurrentQuickSlot(input);

            PreviousButtons = input.Buttons;
            baseLookRotation = Kcc.GetLookRotation();
        }

        if (HasInputAuthority && Runner.IsForward)
        {
            CheckDropItem();
        }
    }

    public override void Render()
    {
        if (Kcc.IsPredictingLookRotation)
        {
            Vector2 predictedLookRotation = baseLookRotation + inputManager.AccumulatedMouseDelta * lookSensitivity;
            Kcc.SetLookRotation(predictedLookRotation, -maxPitch, maxPitch);
        }
        UpdateCamTarget();

        if (HasInputAuthority)
        {
            CheckInteractionLocal(CamTarget.forward);
        }
    }

    private void CheckJump(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Jump))
        {
            if (Kcc.FixedData.IsGrounded)
            {
                Kcc.Jump(jumpImpulse);
                JumpSync++;
            }
        }
    }

    private void SetInputDirection(NetInput input)
    {
        Vector3 worldDirection;
        worldDirection = Kcc.FixedData.TransformRotation * input.Direction.X0Y();
        Kcc.SetInputDirection(worldDirection);
    }

    private void UpdateCamTarget()
    {
        CamTarget.localRotation = Quaternion.Euler(Kcc.GetLookRotation().x, 0f, 0f);
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

        if (Physics.Raycast(CamTarget.position, lookDirection, out RaycastHit hitInfo, interactionRange))
        {
            // ������ �ݱ�
            if (hitInfo.collider.TryGetComponent(out Item item))
            {
                GetItem(item);
            }
            // ��ȣ�ۿ� ������Ʈ
            else if (hitInfo.collider.TryGetComponent(out Interactable interactable))
            {
                interactable.InteractServer(this);
            }
        }
    }

    private void CheckInteractionLocal(Vector3 lookDirection)
    {
        if (Physics.Raycast(CamTarget.position, lookDirection, out RaycastHit hitInfo, interactionRange))
        {
            // UI�� ���� ������ �ؽ�Ʈ�� �ʱ�ȭ�ϰ� ����
            if (UIManager.Singleton.UIStack != 0)
            {
                UIManager.Singleton.MouseText.text = "";
                return;
            }

            // ������
            if (hitInfo.collider.TryGetComponent(out Item item))
            {
                UIManager.Singleton.MouseText.text = item.itemName;

                if (Input.GetKeyDown(KeyCode.E))
                {
                    // �κ��丮 ���� �޽���
                }
            }
            // ��ȣ�ۿ� ������Ʈ
            else if (hitInfo.collider.TryGetComponent(out Interactable interactable))
            {
                UIManager.Singleton.MouseText.text = interactable.interactableName;

                if (Input.GetKeyDown(KeyCode.E))
                {
                    interactable.InteractLocal(this);
                }
            }
            else
            {
                UIManager.Singleton.MouseText.text = "";
            }
        }
        else
        {
            UIManager.Singleton.MouseText.text = "";
        }
    }

    // ������ ȹ��
    private void GetItem(Item item)
    {
        if (HasStateAuthority)
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
        }
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
        {
            inventory[index] = null;
        }
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

        if (equipItemFlag)
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

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayerSendMessage(string message)
    {
        UIManager.Singleton.UpdateChat($"{Name}: {message}");
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    public void RPC_KickPlayer(PlayerRef playerRef)
    {
        if (Runner.LocalPlayer == playerRef) { UIManager.Singleton._MenuConnection.LeaveSession(); }
    }

    public void Teleport(Vector3 position, Quaternion rotation, bool preservePitch = false, bool preserveYaw = false)
    {
        if (Runner.IsForward)
        {
            Kcc.SetPosition(position);
            Kcc.SetLookRotation(rotation, preservePitch, preserveYaw);
        }
    }

    public void SelectVideo(byte index)
    {
        if (HasStateAuthority)
        {
            GameStateManager.Singleton.SelectedVideoIndex = index;
        }
        else if (HasInputAuthority)
        {
            RPC_SelectVideo(index);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SelectVideo(byte index)
    {
        SelectVideo(index);
    }

    public void Hide()
    {
        Kcc.Collider.enabled = !IsHideCollider;
    }
}
