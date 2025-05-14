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
    [SerializeField] private byte inventorySize = 12;

    public KCC Kcc;
    public Transform CamTarget;
    public Transform Hand;
    public List<Item> Inventory = new();
    public byte CurrentQuickSlotIndex;
    public Queue<(byte, byte)> SwitchItemIndexQueue = new();
    public Queue<byte> DropItemIndexQueue = new();
    public Queue<PlayerRef> KickPlayerQueue = new();
    public bool IsReady;
    public bool EquipItemFlag;

    [Networked] public string Name { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync { get; set; }
    [Networked, OnChangedRender(nameof(Hide))] public bool IsHideCollider { get; set; }
    [Networked] public bool IsBlockMovement { get; set; }

    private InputManager inputManager;
    private Vector2 baseLookRotation;
    private Transform itemCategory;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            itemCategory = GameObject.Find("Items").transform;

            if (HasInputAuthority)
            {
                Name = UIManager.Singleton.Name;
            }
        }

        if (HasStateAuthority || HasInputAuthority)
        {
            Inventory = new List<Item>(new Item[inventorySize]);
        }

        if (HasInputAuthority)
        {
            // 내 캐릭터의 일부 모델 파츠 렌더링 비활성화 (카메라 가림 방지)
            foreach (MeshRenderer renderer in modelParts)
            {
                renderer.enabled = false;
                //renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            inputManager = Runner.GetComponent<InputManager>();

            // 플레이어 이름 RPC 호출
            RPC_PlayerName(UIManager.Singleton.Name);

            // 카메라 설정 및 UI 연결
            CameraFollow.Singleton.SetTarget(CamTarget, this);
            UIManager.Singleton.LocalPlayer = this;

            // UI 인벤토리 슬롯 초기화
            for (byte i = 0; i < inventorySize; i++)
            {
                UIManager.Singleton.UpdateItemSlot(i, Inventory[i]?.ItemImage);
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
            CheckSwitchItem();
            CheckDropItem();
        }

        if (HasStateAuthority && HasInputAuthority && Runner.IsForward)
        {
            CheckKickPlayer();
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
                item.InteractServer(this);
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
                UIManager.Singleton.MouseText.text = item.ItemName;
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

    // 아이템 슬롯 바꾸기
    public void SwitchItem(byte index1, byte index2)
    {
        SwitchItemIndexQueue.Enqueue((index1, index2));
    }

    // 바꿀 아이템 대기열
    private void CheckSwitchItem()
    {
        while (SwitchItemIndexQueue.Count > 0)
        {
            var (index1, index2) = SwitchItemIndexQueue.Dequeue();

            if (HasStateAuthority)
            {
                (Inventory[index1], Inventory[index2]) = (Inventory[index2], Inventory[index1]);
                if((index1 == CurrentQuickSlotIndex || index2 == CurrentQuickSlotIndex))
                {
                    EquipItemFlag = true;
                }
            }
            else
            {
                (Inventory[index1], Inventory[index2]) = (Inventory[index2], Inventory[index1]);
                RPC_SwitchSlot(index1, index2);
            }
        }
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
            byte index = DropItemIndexQueue.Dequeue();

            if (HasStateAuthority)
            {
                Inventory[index].IsHideVisual = false;
                Inventory[index].IsHideCollider = false;
                Inventory[index].Hide();
                StartCoroutine(AttachOnParent(Inventory[index], itemCategory, Hand));
                Inventory[index] = null;
                if (index == CurrentQuickSlotIndex)
                {
                    EquipItemFlag = true;
                }
            }
            else
            {
                Inventory[index] = null;
                RPC_DropItem(index);
            }   
        }
    }

    // 아이템 획득 RPC
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_PickUpItem(byte index, NetworkObject item)
    {
        Inventory[index] = item.GetComponent<Item>();
        Inventory[index].InteractLocal(this, index);
    }

    // 인벤토리 풀 RPC
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_InventoryFull()
    {
        // "인벤토리가 가득 찼습니다."
    }

    // 아이템 슬롯 바꾸기 RPC
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SwitchSlot(byte index1, byte index2)
    {
        (Inventory[index1], Inventory[index2]) = (Inventory[index2], Inventory[index1]);
        if (index1 == CurrentQuickSlotIndex || index2 == CurrentQuickSlotIndex)
        {
            EquipItemFlag = true;
        }
    }

    // 아이템 버리기 RPC
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_DropItem(byte index)
    {
        Inventory[index].IsHideVisual = false;
        Inventory[index].IsHideCollider = false;
        Inventory[index].Hide();
        StartCoroutine(AttachOnParent(Inventory[index], itemCategory, Hand));
        Inventory[index] = null;
        if (index == CurrentQuickSlotIndex)
        {
            EquipItemFlag = true;
        }
    }

    // 부모 변경 후, 1프레임 대기하고 위치 변경
    public IEnumerator AttachOnParent(Item item, Transform parent, Transform point)
    {
        item.transform.SetParent(parent); // Network Transform 컴포넌트의 Sync Parent 체크
        yield return null;
        item.GetComponent<NetworkTransform>().Teleport(point.position, point.rotation); // 월드 포지션. 이거 안 하면 위치 조금 어긋남
    }

    // equipItemFlag가 true면 퀵슬롯 아이템 표시
    private void CheckCurrentQuickSlot(NetInput input)
    {
        if (HasStateAuthority)
        {
            if (CurrentQuickSlotIndex != input.CurrentQuickSlotIndex)
            {
                CurrentQuickSlotIndex = input.CurrentQuickSlotIndex;
                EquipItemFlag = true;
            }

            if (EquipItemFlag)
            {
                EquipItem(CurrentQuickSlotIndex);
                EquipItemFlag = false;
            }
        }
    }

    // 아이템 장착
    private void EquipItem(int index)
    {
        for (byte i = 0; i < inventorySize; i++)
        {
            if (Inventory[i] != null)
            {
                Inventory[i].IsHideVisual = true;
                Inventory[i].Hide();
            }
        }

        if (Inventory[index] != null)
        {
            Inventory[index].IsHideVisual = false;
            Inventory[index].Hide();
        }
    }

    [Rpc(RpcSources.StateAuthority | RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_PlayerSendMessage(string message)
    {
        UIManager.Singleton.UpdateChat($"{Name}: {message}");
    }

    private void CheckKickPlayer()
    {
        while (KickPlayerQueue.Count > 0)
        {
            RPC_KickPlayer(KickPlayerQueue.Dequeue());
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    public void RPC_KickPlayer(PlayerRef playerRef)
    {
        if (Runner.LocalPlayer == playerRef)
        {
            UIManager.Singleton._MenuConnection.KickedFromSession();
        }
    }

    public void Teleport(Vector3 position, Quaternion rotation, bool preservePitch = false, bool preserveYaw = false)
    {
        if (Runner.IsForward)
        {
            Kcc.SetPosition(position);
            Kcc.SetLookRotation(rotation, preservePitch, preserveYaw);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SelectVideo(byte index)
    {
        GameStateManager.Singleton.SelectedVideoIndex = index;
    }

    public void Hide()
    {
        Kcc.Collider.enabled = !IsHideCollider;
    }
}
