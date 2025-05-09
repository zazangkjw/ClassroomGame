using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WebSocketSharp;

public class UIManager : MonoBehaviour
{
    public static UIManager Singleton;

    public String Name => nameInputField.text;
    public Player LocalPlayer;
    public byte UIStack;
    public TextMeshProUGUI MouseText;
    public GameObject LeaderboardScreen;
    public MenuConnection _MenuConnection;
    public TextMeshProUGUI CountdownText;
    public List<string> VideoList = new();
    public List<string> VideoListOriginal = new List<string>
    {
        "A",
        "B"
    };

    [SerializeField] private GameObject inventoryScreen;
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private List<Slot> itemSlots = new();
    [SerializeField] private GraphicRaycaster uiRaycaster;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private Image mouseImage;
    [SerializeField] private Image inventoryBackground;
    [SerializeField] private GameObject chatScreen;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private GameObject chatScrollbar;
    [SerializeField] private GameObject chatScrollbarVertical;
    [SerializeField] private Image chatBackground;
    [SerializeField] private Transform chatContent;
    [SerializeField] private GameObject chatMessagePrefab;
    [SerializeField] private LeaderboardItem[] leaderboardItems;
    [SerializeField] private GameObject kickPopUp;
    [SerializeField] private TextMeshProUGUI kickPopUpMessage;
    [SerializeField] private GameObject projectorScreen;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pointerEnterSound;
    [SerializeField] private AudioClip onClickSound;

    private PointerEventData pointerData;
    private List<RaycastResult> uiRaycasterResults = new();
    private byte selectedSlotIndex;
    private WaitForSeconds waitHideChat = new WaitForSeconds(10f);
    private Coroutine hideChatRoutine;
    private Queue<GameObject> chatList = new();
    private GameObject chat;
    private int selectedKickIndex;

    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }

        Singleton = this;
        DontDestroyOnLoad(gameObject);

        pointerData = new PointerEventData(eventSystem);
        for (byte i = 0; i < itemSlots.Count; i++)
        {
            itemSlots[i].slotIndex = i;
        }

        VideoList = VideoListOriginal; // �� ���� �̸� �����ϱ�
    }

    private void Update()
    {
        InventoryInteraction();
        OpenUI();
    }

    public void UnFocus()
    {
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void OpenUI()
    {
        if (LocalPlayer == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            if(UIStack == 0)
            {
                OpenInventory(true);
            }
            else if (inventoryScreen.activeSelf)
            {
                OpenInventory(false);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (UIStack == 0)
            {
                OpenPause(true);
            }
            else
            {
                if (chatInputField.gameObject.activeSelf)
                {
                    OpenChat(false);
                }
                else if (projectorScreen.activeSelf)
                {
                    OpenProjector(false);
                }
                else if (kickPopUp.activeSelf)
                {
                    OpenKickPopUp(false);
                }
                else if (LeaderboardScreen.activeSelf)
                {
                    OpenLeaderboard(false);
                }
                else if (inventoryScreen.activeSelf)
                {
                    OpenInventory(false);
                }
                else if (pauseScreen.activeSelf)
                {
                    OpenPause(false);
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (chatInputField.gameObject.activeSelf)
            {
                if (!chatInputField.text.IsNullOrEmpty())
                {
                    LocalPlayer.RPC_PlayerSendMessage(chatInputField.text);
                }
                OpenChat(false);
            }
            else if (!kickPopUp.activeSelf)
            {
                OpenChat(true);
            }
            else
            {
                KickPlayer();
            }
        }

        if(Input.GetKeyDown(KeyCode.Tab))
        {
            if (UIStack == 0)
            {
                OpenLeaderboard(true);
            }
            else if (LeaderboardScreen.activeSelf)
            {
                OpenLeaderboard(false);
                if (kickPopUp.activeSelf)
                {
                    OpenKickPopUp(false);
                }
            }
        }

        if (Input.GetMouseButtonDown(0) && chatInputField.gameObject.activeSelf && 
            EventSystem.current.currentSelectedGameObject != chatInputField.gameObject && 
            EventSystem.current.currentSelectedGameObject != chatScrollbarVertical)
        {
            OpenChat(false);
        }

        CheckUIStack();
    }

    // �κ��丮 ����/�ݱ�
    private void OpenInventory(bool open)
    {
        // ����
        if (open && !inventoryScreen.activeSelf)
        {
            UIStack++;
            inventoryScreen.SetActive(true);
        }
        // �ݱ�
        else if (!open && inventoryScreen.activeSelf)
        {
            UIStack--;
            mouseImage.sprite = null;
            mouseImage.enabled = false;
            inventoryScreen.SetActive(false);
        }
    }

    public void OpenPause(bool open)
    {
        if (open && !pauseScreen.activeSelf)
        {
            UIStack++;
            pauseScreen.SetActive(true);
        }
        else if (!open && pauseScreen.activeSelf)
        {
            UIStack--;
            pauseScreen.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void OpenChat(bool open)
    {
        if (open && !chatInputField.gameObject.activeSelf)
        {
            UIStack++;
            chatScreen.SetActive(true);
            chatInputField.gameObject.SetActive(true);
            chatInputField.Select();
            chatInputField.ActivateInputField();
            chatScrollbar.SetActive(true);
            chatBackground.enabled = true;
            if (hideChatRoutine != null) StopCoroutine(hideChatRoutine);
        }
        else if (!open && chatInputField.gameObject.activeSelf)
        {
            UIStack--;
            chatInputField.text = null;
            chatInputField.gameObject.SetActive(false);
            chatScrollbar.SetActive(false);
            chatBackground.enabled = false;
            HideChat();
        }
    }

    private void OpenLeaderboard(bool open)
    {
        if (open && !LeaderboardScreen.activeSelf)
        {
            UIStack++;
            LeaderboardScreen.SetActive(true);
        }
        else if (!open && LeaderboardScreen.activeSelf)
        {
            UIStack--;
            LeaderboardScreen.SetActive(false);
        }
    }

    public void OpenKickPopUp(bool open)
    {
        if (open && !kickPopUp.activeSelf)
        {
            UIStack++;
            kickPopUp.SetActive(true);
            EventSystem.current.SetSelectedGameObject(null);
        }
        else if(!open && kickPopUp.activeSelf)
        {
            UIStack--;
            kickPopUp.SetActive(false);
        }
    }

    public void OpenProjector(bool open)
    {
        if (open && !projectorScreen.activeSelf)
        {
            UIStack++;
            projectorScreen.SetActive(true);
        }
        else if (!open && projectorScreen.activeSelf)
        {
            UIStack--;
            projectorScreen.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void CheckUIStack()
    {
        if (UIStack == 0)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // �κ��丮 ��ȣ�ۿ�
    private void InventoryInteraction()
    {
        if (LocalPlayer == null || !inventoryScreen.activeSelf) return;

        mouseImage.transform.position = Input.mousePosition;
        pointerData.position = Input.mousePosition;

        if (Input.GetMouseButtonDown(0) && !chatInputField.gameObject.activeSelf && !LeaderboardScreen.gameObject.activeSelf)
        {
            uiRaycasterResults.Clear();
            uiRaycaster.Raycast(pointerData, uiRaycasterResults);

            foreach (var result in uiRaycasterResults)
            {
                // ������ ����
                if (!mouseImage.enabled)
                {
                    if (result.gameObject.TryGetComponent(out Slot slot) && slot.itemImage.sprite != null)
                    {
                        mouseImage.sprite = slot.itemImage.sprite;
                        mouseImage.enabled = true;
                        selectedSlotIndex = slot.slotIndex;
                        return;
                    }
                }
                else
                {
                    // �ٸ� ���԰� ��ü
                    if (result.gameObject.TryGetComponent(out Slot slot))
                    {
                        LocalPlayer.SwitchItem(selectedSlotIndex, slot.slotIndex);
                        UpdateItemSlot(selectedSlotIndex, slot.itemImage.sprite);
                        UpdateItemSlot(slot.slotIndex, mouseImage.sprite);
                        mouseImage.sprite = null;
                        mouseImage.enabled = false;
                        return;
                    }
                    // ������ ������
                    else if (result.gameObject == inventoryBackground.gameObject)
                    {
                        LocalPlayer.DropItem(selectedSlotIndex);
                        UpdateItemSlot(selectedSlotIndex, null);
                        mouseImage.sprite = null;
                        mouseImage.enabled = false;
                        return;
                    }
                }
            }
        }
    }

    // �κ��丮 ������ ���� ������Ʈ
    public void UpdateItemSlot(int index, Sprite itemImage)
    {
        itemSlots[index].itemImage.sprite = itemImage;
        itemSlots[index].itemImage.enabled = itemImage != null;
    }

    private void HideChatDirect()
    {
        if (hideChatRoutine != null)
        {
            StopCoroutine(HideChatRoutine());
        }

        chatScreen.SetActive(false);
    }

    private void HideChat()
    {
        if (hideChatRoutine != null)
        {
            StopCoroutine(HideChatRoutine());
        }

        hideChatRoutine = StartCoroutine(HideChatRoutine());
    }

    private IEnumerator HideChatRoutine()
    {
        yield return waitHideChat;
        chatScreen.SetActive(false);
    }

    public void UpdateChat(string message)
    {
        if (chatList.Count + 1 > 50)
        {
            Destroy(chatList.Dequeue().gameObject);
        }

        chat = Instantiate(chatMessagePrefab, chatContent);
        chat.GetComponent<TextMeshProUGUI>().text = message;
        chatList.Enqueue(chat);

        chatScreen.SetActive(true);
        HideChat();
    }

    public void UpdateLeaderboard(KeyValuePair<Fusion.PlayerRef, Player>[] players)
    {
        for (int i = 0; i < leaderboardItems.Length; i++)
        {
            if (i < players.Length)
            {
                leaderboardItems[i].playerRef = players[i].Key;
                leaderboardItems[i].nameText.text = players[i].Value.Name;
                if (LocalPlayer.HasStateAuthority && leaderboardItems[i].playerRef != LocalPlayer.Runner.LocalPlayer)
                {
                    leaderboardItems[i].kickButton.gameObject.SetActive(true);
                }
                else
                {
                    leaderboardItems[i].kickButton.gameObject.SetActive(false);
                }
            }
            else
            {
                leaderboardItems[i].playerRef = PlayerRef.None;
                leaderboardItems[i].nameText.text = "";
                leaderboardItems[i].kickButton.gameObject.SetActive(false);
            }
        }
    }

    [Serializable]
    private struct LeaderboardItem
    {
        public PlayerRef playerRef;
        public TextMeshProUGUI nameText;
        public Button kickButton;
    }

    public void SelectKickPlayer(int index)
    {
        if (LocalPlayer != null)
        {
            selectedKickIndex = index;
            kickPopUpMessage.text = $"Kick \'{leaderboardItems[selectedKickIndex].nameText.text}\'?";
            OpenKickPopUp(true);
        }
    }

    public void KickPlayer()
    {
        if (LocalPlayer != null)
        {
            LocalPlayer.RPC_KickPlayer(leaderboardItems[selectedKickIndex].playerRef);
            OpenKickPopUp(false);
        }
    }

    public void SelectVideo(int index)
    {
        LocalPlayer.SelectVideo(((byte)index));
    }

    public void PlayPointerEnterSound()
    {
        audioSource.PlayOneShot(pointerEnterSound);
    }

    public void PlayOnClickSound()
    {
        audioSource.PlayOneShot(onClickSound);
    }
}
