using Fusion;
using Fusion.Addons.KCC;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Player : NetworkBehaviour
{
    [SerializeField] private MeshRenderer[] modelParts;
    [SerializeField] private LayerMask lagCompLayers;
    [SerializeField] private AudioSource source;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private Vector3 jumpImpulse = new(0f, 2f, 0f);
    [SerializeField] private float interactionRange = 5f;
    [SerializeField] private byte inventorySize = 12;
    [SerializeField] private GameObject myCharacter;
    [SerializeField] private GameObject myCharacterPOV;
    [SerializeField] private Animator myAnimator;
    [SerializeField] private Animator myAnimatorPOV;
    [SerializeField] private Transform povTarget;
    [SerializeField] private EnvironmentProcessor runProcessor;

    public KCC Kcc;
    public Transform CamTarget;
    public Transform Hand;
    public List<Item> Inventory = new();
    public byte CurrentQuickSlotIndex;
    public Queue<(byte, byte)> SwitchItemIndexQueue = new();
    public Queue<byte> DropItemIndexQueue = new();
    public Queue<PlayerRef> KickPlayerQueue = new();
    public Queue<PlayerRef> AuthFailedPlayerQueue = new();
    public Queue<byte> CharacterQueue = new();
    public Queue<(Vector3, Quaternion, bool, bool)> TeleportQueue = new();
    public bool IsReady;
    public bool EquipItemFlag;

    [Networked] public string SteamName { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync { get; set; }
    [Networked] private byte Direction { get; set; }
    [Networked] private bool IsRunning { get; set; }
    [Networked] private bool IsGround { get; set; }
    [Networked, OnChangedRender(nameof(Hide))] public bool IsHideCollider { get; set; }
    [Networked] public bool IsBlockMovement { get; set; }
    [Networked, OnChangedRender(nameof(ChangeCharacter))] private byte CharacterIndex { get; set; }

    private InputManager inputManager;
    private Vector2 baseLookRotation;
    private Transform itemCategory;
    private Transform spine; // 아바타의 상체

    public override void Spawned()
    {
        if (!HasStateAuthority && HasInputAuthority)
        {
            //GetSteamAuthTicket();  // 테스트 할 때는 주석 처리
        }

        if (HasStateAuthority)
        {
            itemCategory = GameObject.Find("----- Items -----").transform;

            if (HasInputAuthority)
            {
                SteamName = SteamClient.Name;
            }
        }

        if (HasStateAuthority || HasInputAuthority)
        {
            Inventory = new List<Item>(new Item[inventorySize]);
        }

        if (HasInputAuthority)
        {
            inputManager = Runner.GetComponent<InputManager>();

            // 플레이어 이름 RPC 호출
            RPC_PlayerName(SteamClient.Name);

            // 카메라 설정 및 UI 연결
            CameraFollow.Singleton.SetTarget(CamTarget, this);
            UIManager.Singleton.LocalPlayer = this;

            // UI 인벤토리 슬롯 초기화
            for (byte i = 0; i < inventorySize; i++)
            {
                UIManager.Singleton.UpdateItemSlot(i, Inventory[i]?.ItemImage);
            }

            // 캐릭터 외형 변경
            if (CharacterIndex != UIManager.Singleton.CharacterIndex)
            {
                if (HasStateAuthority)
                {
                    CharacterIndex = UIManager.Singleton.CharacterIndex;
                    ChangeCharacter();
                }
                else
                {
                    RPC_ChangeCharacter(UIManager.Singleton.CharacterIndex);
                }
            }
            else
            {
                // 내 캐릭터의 일부 모델 파츠 렌더링 비활성화 (카메라 가림 방지)
                foreach (SkinnedMeshRenderer renderer in myCharacter.GetComponent<CharacterInfo>().Visuals)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                myCharacterPOV.SetActive(true);
            }

            // 첫 입장 시, 캐릭터 선택창 켜기
            if (UIManager.Singleton.IsFirstJoin && SceneManager.GetActiveScene().name == "LivingRoom")
            {
                UIManager.Singleton.OpenCharacterScreen(true);
                UIManager.Singleton.IsFirstJoin = false;
            }
        }

        if (!HasInputAuthority)
        {
            if (CharacterIndex != 0)
            {
                ChangeCharacter();
            }
        }
    }

    private void Start()
    {
        spine = myAnimator.GetBoneTransform(HumanBodyBones.Spine); // 상체값 가져오기 (허리 위)
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
                CheckRun(input);
                SetInputDirection(input);
            }
            else
            {
                Kcc.SetInputDirection(Vector3.zero);
                Kcc.SetKinematicVelocity(Vector3.zero); // 이동 멈추기
                Kcc.SetDynamicVelocity(Vector3.zero); // 점프 멈추기
                Kcc.SetExternalVelocity(Vector3.zero);
                IsGround = true;
                Direction = 0;
            }

            Kcc.AddLookRotation(input.LookDelta * lookSensitivity, -maxPitch, maxPitch);
            Vector3 lookDirection = CamTarget.forward;
            CheckInteraction(input, lookDirection);
            CheckCurrentQuickSlot(input);
            UpdateCamTarget();
            baseLookRotation = Kcc.GetLookRotation();
            PreviousButtons = input.Buttons;
        }

        if (HasInputAuthority && Runner.IsForward)
        {
            CheckCharacterIndex();
            CheckSwitchItem();
            CheckDropItem();
        }

        if (HasStateAuthority && HasInputAuthority && Runner.IsForward)
        {
            CheckKickPlayer();
        }

        if (HasStateAuthority && Runner.IsForward)
        {
            CheckTeleport();
        }
    }

    public override void Render()
    {
        // 카메라 타겟 로컬 회전
        if (Kcc.IsPredictingLookRotation)
        {
            Vector2 predictedLookRotation = baseLookRotation + inputManager.AccumulatedMouseDelta * lookSensitivity;
            Kcc.SetLookRotation(predictedLookRotation, -maxPitch, maxPitch);
        }
        UpdateCamTarget();

        // 상호작용 레이캐스트 로컬 체크
        if (HasInputAuthority)
        {
            CheckInteractionLocal(CamTarget.forward);
        }

        myAnimator.SetInteger("Direction", Direction);
        myAnimator.SetBool("Jump", !IsGround);
        if (myAnimatorPOV.gameObject.activeSelf)
        {
            myAnimatorPOV.SetInteger("Direction", Direction);
            myAnimatorPOV.SetBool("Jump", !IsGround);
        }
    }

    private void LateUpdate()
    {
        // 캐릭터 상체 회전
        float x = CamTarget.localEulerAngles.x;
        if (x < 180f)
        {
            x = x * (60f / 85f);
        }
        else
        {
            x = 360f - (360f - x) * (60f / 85f);
        }
        spine.localRotation = Quaternion.Euler(x, spine.localEulerAngles.y, spine.localEulerAngles.z);
    }

    private void CheckJump(NetInput input)
    {
        IsGround = Kcc.FixedData.IsGrounded;
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Jump))
        {
            if (IsGround)
            {
                Kcc.Jump(jumpImpulse);
                JumpSync++;
            }
        }
    }

    private void CheckRun(NetInput input)
    {
        if (input.Buttons.WasPressed(PreviousButtons, InputButton.Run))
        {
            Kcc.AddModifier(runProcessor);
            IsRunning = true;
        }
        else if(input.Buttons.WasReleased(PreviousButtons, InputButton.Run))
        {
            Kcc.RemoveModifier(runProcessor);
            IsRunning = false;
        }
    }

    private void SetInputDirection(NetInput input)
    {
        Vector3 worldDirection = transform.rotation * input.Direction.X0Y();
        Kcc.SetInputDirection(worldDirection);

        if(input.Direction.y == 0)
        {
            if (input.Direction.x == 0) { Direction = 0; }
            else if (input.Direction.x > 0) { Direction = 3; }
            else if (input.Direction.x < 0) { Direction = 7; }
        }
        else if (input.Direction.y > 0)
        {
            if(input.Direction.x == 0) { Direction = 1; }
            else if(input.Direction.x > 0) { Direction = 2; }
            else if (input.Direction.x < 0) { Direction = 8; }
        }
        else if(input.Direction.y < 0)
        {
            if (input.Direction.x == 0) { Direction = 5; }
            else if (input.Direction.x > 0) { Direction = 4; }
            else if (input.Direction.x < 0) { Direction = 6; }
        }
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
        SteamName = name;
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
            // 플레이어
            else if (hitInfo.collider.transform.parent != null && hitInfo.collider.transform.parent.TryGetComponent(out Player player) && player != this)
            {
                UIManager.Singleton.MouseText.text = player.SteamName;
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
                StartCoroutine(AttachOnParent(Inventory[index].gameObject, itemCategory, Hand));
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
        StartCoroutine(AttachOnParent(Inventory[index].gameObject, itemCategory, Hand));
        Inventory[index] = null;
        if (index == CurrentQuickSlotIndex)
        {
            EquipItemFlag = true;
        }
    }

    // 부모 변경 후, 1프레임 대기하고 위치 변경
    public IEnumerator AttachOnParent(GameObject obj, Transform parent, Transform point)
    {
        obj.transform.SetParent(parent); // Network Transform 컴포넌트의 Sync Parent 체크
        yield return null;
        obj.GetComponent<NetworkTransform>().Teleport(point.position, point.rotation); // 월드 포지션. 이거 안 하면 위치 조금 어긋남
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
        UIManager.Singleton.UpdateChat($"{SteamName}: {message}");
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

    private void Teleport(Vector3 position, Quaternion rotation, bool preservePitch = false, bool preserveYaw = false)
    {
        if (Runner.IsForward)
        {
            Kcc.SetPosition(position);
            Kcc.SetLookRotation(rotation, preservePitch, preserveYaw);
        }
    }

    private void CheckTeleport()
    {
        while (TeleportQueue.Count > 0)
        {
            var (position, rotation, pitch, yaw) = TeleportQueue.Dequeue();
            Teleport(position, rotation, pitch, yaw);
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

    private async void GetSteamAuthTicket()
    {
        if (UIManager.Singleton._MenuConnection.Ticket == null)
        {
            UIManager.Singleton._MenuConnection.Ticket = await SteamUser.GetAuthSessionTicketAsync(UIManager.Singleton._MenuConnection.CurrentLobby.Owner.Id);
            Rpc_SendSteamTicket(UIManager.Singleton._MenuConnection.Ticket.Data, SteamClient.SteamId);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SendSteamTicket(byte[] steamTicket, ulong steamId)
    {
        if (!GameStateManager.Singleton.AuthenticatedPlayers.Contains(steamId))
        {
            // 서버에서 검증
            VerifyTicket(steamTicket, steamId);
        }
    }

    private void VerifyTicket(byte[] steamTicket, ulong steamId)
    {
        bool result = SteamServer.BeginAuthSession(steamTicket, steamId);
        
        if (result)
        {
            // 인증 성공
            Debug.Log($"인증 성공: {steamId}");
            GameStateManager.Singleton.AuthenticatedPlayers.Add(steamId);
        }
        else
        {
            // 인증 실패 → 연결 끊기
            RPC_SteamAuthFail();
            Debug.Log($"Steam auth failed: {steamId}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_SteamAuthFail()
    {
        Debug.Log("추방당함");
        UIManager.Singleton._MenuConnection.SteamAuthFail();
    }

    private void CheckCharacterIndex()
    {
        while (CharacterQueue.Count > 0)
        {
            byte index = CharacterQueue.Dequeue();

            if(UIManager.Singleton.CharacterIndex != index)
            {
                UIManager.Singleton.CharacterIndex = index;

                if (HasStateAuthority)
                {
                    CharacterIndex = index;
                    ChangeCharacter();
                }
                else
                {
                    RPC_ChangeCharacter(UIManager.Singleton.CharacterIndex);
                }
            }
        }
    }

    private void ChangeCharacter()
    {
        if (myCharacter != null)
        {
            Destroy(myCharacter);
            Destroy(myCharacterPOV);
        }

        myCharacter = Instantiate(UIManager.Singleton.Characters[CharacterIndex], transform.position, transform.rotation, transform);
        myCharacterPOV = Instantiate(UIManager.Singleton.CharactersPOV[CharacterIndex], povTarget.position, povTarget.rotation, povTarget);
        myAnimator = myCharacter.GetComponent<Animator>();
        myAnimatorPOV = myCharacterPOV.GetComponent<Animator>();
        spine = myAnimator.GetBoneTransform(HumanBodyBones.Spine);

        if (HasInputAuthority)
        {
            // 내 캐릭터의 일부 모델 파츠 렌더링 비활성화 (카메라 가림 방지)
            foreach (SkinnedMeshRenderer renderer in myCharacter.GetComponent<CharacterInfo>().Visuals)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            myCharacterPOV.SetActive(true);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ChangeCharacter(byte index)
    {
        CharacterIndex = index;
        ChangeCharacter();
    }
}
