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
        // 호스트, 입력 권한 가진 클라이언트 둘 다 실행됨
        if (GetInput(out NetInput input))
        {
            if (!IsBlockMovement)
            {
                CheckJump(input);
                SetInputDirection(input);
            }
            else
            {
                Kcc.SetInputDirection(Vector3.zero); // 이동 멈추기

                // 점프 중이면 강제로 낙하하게 만들기
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

    // 상호작용
    private void CheckInteraction(NetInput input, Vector3 lookDirection)
    {
        if (!HasStateAuthority || !Runner.IsForward || !input.Buttons.WasPressed(PreviousButtons, InputButton.Interaction))
        {
            return;
        }

        if (Physics.Raycast(CamTarget.position, lookDirection, out RaycastHit hitInfo, interactionRange))
        {
            // 아이템 줍기
            if (hitInfo.collider.TryGetComponent(out Item item))
            {
                GetItem(item);
            }
            // 상호작용 오브젝트
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
            // UI가 열려 있으면 텍스트만 초기화하고 끝냄
            if (UIManager.Singleton.UIStack != 0)
            {
                UIManager.Singleton.MouseText.text = "";
                return;
            }

            // 아이템
            if (hitInfo.collider.TryGetComponent(out Item item))
            {
                UIManager.Singleton.MouseText.text = item.itemName;

                if (Input.GetKeyDown(KeyCode.E))
                {
                    // 인벤토리 꽉참 메시지
                }
            }
            // 상호작용 오브젝트
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

    // 아이템 획득
    private void GetItem(Item item)
    {
        if (HasStateAuthority)
        {
            // 현재 선택된 퀵슬롯이 비어있으면 해당 퀵슬롯에 아이템 획득
            if (inventory[currentQuickSlotIndex] == null)
            {
                RPC_GetItem(currentQuickSlotIndex, item.GetComponent<NetworkObject>());
                return;
            }

            // 인벤토리에 아이템 획득
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

    // 아이템 슬롯 바꾸기
    public void SwitchItem(byte index1, byte index2)
    {
        RPC_SwitchSlot(index1, index2);
    }

    // 아이템 버리기
    public void DropItem(byte index)
    {
        DropItemIndexQueue.Enqueue(index);
    }

    // 버릴 아이템 대기열
    private void CheckDropItem()
    {
        while (DropItemIndexQueue.Count > 0)
        {
            RPC_DropItem(DropItemIndexQueue.Dequeue());
        }
    }

    // 아이템 획득 RPC
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    private void RPC_GetItem(byte index, NetworkObject item)
    {
        inventory[index] = item.GetComponent<Item>();

        if (HasStateAuthority)
        {
            inventory[index].IsHideVisual = true;
            inventory[index].IsHideCollider = true;
            inventory[index].Hide(); // 호스트는 틱 단계에서 아이템을 즉시 숨김 
            StartCoroutine(AttachOnParent(inventory[index], hand, hand));
            if (index == currentQuickSlotIndex)
                equipItemFlag = true;
        }

        if (HasInputAuthority)
        {
            UIManager.Singleton.UpdateItemSlot(index, inventory[index].itemImage);
        }
    }

    // 아이템 슬롯 바꾸기 RPC
    [Rpc(RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.InputAuthority | RpcTargets.StateAuthority)]
    private void RPC_SwitchSlot(byte index1, byte index2)
    {
        (inventory[index1], inventory[index2]) = (inventory[index2], inventory[index1]);
        if (HasStateAuthority && (index1 == currentQuickSlotIndex || index2 == currentQuickSlotIndex))
        {
            equipItemFlag = true;
        }
    }

    // 아이템 버리기 RPC
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

    // 부모 변경 후, 1프레임 대기하고 위치 변경
    private IEnumerator AttachOnParent(Item item, Transform parent, Transform point)
    {
        item.transform.SetParent(parent); // Network Transform 컴포넌트의 Sync Parent 체크
        yield return null;
        item.GetComponent<NetworkTransform>().Teleport(point.position, point.rotation); // 월드 포지션. 이거 안 하면 위치 조금 어긋남
    }

    // equipItemFlag가 true면 퀵슬롯 아이템 표시
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

    // 아이템 장착
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
