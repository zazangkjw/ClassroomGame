using Fusion;
using Fusion.Addons.KCC;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private GameObject myCharacter;
    [SerializeField] private GameObject myCharacterPOV;
    [SerializeField] private Animator myAnimator;
    [SerializeField] private Animator myAnimatorPOV;
    [SerializeField] private Transform povTarget;

    public KCC Kcc;
    public float MasterSpeed;
    public float WalkSpeed;
    public float RunSpeed;
    public Transform CamTarget;
    public Transform Hand;
    public List<Item> Inventory = new();
    public byte CurrentQuickSlotIndex;
    public Queue<(byte, byte)> SwitchItemIndexQueue = new();
    public Queue<byte> DropItemIndexQueue = new();
    public Queue<PlayerRef> KickPlayerQueue = new();
    public Queue<PlayerRef> AuthFailedPlayerQueue = new();
    public Queue<byte> CharacterQueue = new();
    public bool IsReady;
    public bool EquipItemFlag;
    public bool IsGround;

    [Networked] public string Name { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked, OnChangedRender(nameof(Jumped))] private int JumpSync { get; set; }
    [Networked] private bool IsRunning { get; set; }
    [Networked, OnChangedRender(nameof(Hide))] public bool IsHideCollider { get; set; }
    [Networked] public bool IsBlockMovement { get; set; }
    [Networked, OnChangedRender(nameof(ChangeCharacter))] private byte CharacterIndex { get; set; }

    private InputManager inputManager;
    private Vector2 baseLookRotation;
    private Transform itemCategory;
    private Transform spine; // 아바타의 상체
    private List<Vector3> previousPos = new();

    public override void Spawned()
    {
        if (!HasStateAuthority && HasInputAuthority)
        {
            GetSteamAuthTicket();
        }

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
            if (UIManager.Singleton.IsFirstJoin && SceneManager.GetActiveScene().name == "Lobby")
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

        for (int i = 0; i < 5; i++)
        {
            previousPos.Add(transform.position);
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
            CheckCharacterIndex();
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

        if (Physics.Raycast(transform.position + transform.up * 0.1f, -transform.up, out RaycastHit hitInfo, 0.4f))
        {
            IsGround = true;
        }
        else
        {
            IsGround = false;
        }

        // 애니메이션 블렌드 트리
        Vector3 direction = transform.InverseTransformDirection((transform.position - previousPos[4]) / Time.deltaTime); // "이 월드 방향은 이 오브젝트 기준으로 어떤 방향인가?"를 계산. Quaternion.Inverse(transform.rotation) * (transform.position - previousPos)와 같음
        for(int i = previousPos.Count - 1; i > 0; i--)
        {
            previousPos[i] = previousPos[i - 1];
        }
        previousPos[0] = transform.position;
        int directionX = 0;
        int directionZ = 0;
        if (direction.x > 5f) { directionX = 1; }
        else if (direction.x < -5f) { directionX = -1; }
        if (direction.z > 5f) { directionZ = 1; }
        else if (direction.z < -5f) { directionZ = -1; }
        if (IsRunning) { directionX *= 2; directionZ *= 2; }
        myAnimator.SetInteger("DirectionX", directionX);
        myAnimator.SetInteger("DirectionZ", directionZ);
        myAnimator.SetBool("Jump", !IsGround);
        if (myAnimatorPOV.gameObject.activeSelf)
        {
            myAnimatorPOV.SetInteger("DirectionX", directionX);
            myAnimatorPOV.SetInteger("DirectionZ", directionZ);
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
        if (!IsRunning)
        {
            worldDirection = Kcc.FixedData.TransformRotation * input.Direction.X0Y() * WalkSpeed * MasterSpeed;
        }
        else
        {
            worldDirection = Kcc.FixedData.TransformRotation * input.Direction.X0Y() * RunSpeed * MasterSpeed;
        }
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
