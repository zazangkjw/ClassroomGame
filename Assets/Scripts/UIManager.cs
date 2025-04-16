using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WebSocketSharp;

public class UIManager : MonoBehaviour
{
    public static UIManager Singleton;

    public String Name => nameInputField.text;
    public Player LocalPlayer;
    public byte UIStack;

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

    private PointerEventData pointerData;
    private List<RaycastResult> uiRaycasterResults = new();
    private byte selectedSlotIndex;
    private WaitForSeconds waitHideChat = new WaitForSeconds(10f);
    private Coroutine hideChatRoutine;
    private Queue<GameObject> chatList = new();
    private GameObject chat;

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
    }

    private void Update()
    {
        InventoryInteraction();
        OpenUI();
    }

    private void OpenUI()
    {
        if (LocalPlayer == null)
            return;

        if (Input.GetKeyDown(KeyCode.I))
        {
            if(UIStack == 0)
            {
                OpenInventory(true);
            }
            else if (chatInputField.gameObject.activeSelf)
            {

            }
            else if (inventoryScreen.activeSelf)
            {
                OpenInventory(false);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if(UIStack == 0)
            {
                OpenPause(true);
            }
            else if (chatInputField.gameObject.activeSelf)
            {
                OpenChat(false);
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

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (!chatInputField.gameObject.activeSelf)
            {
                OpenChat(true);
            }
            else
            {
                if (!chatInputField.text.IsNullOrEmpty())
                {
                    LocalPlayer.RPC_PlayerSendMessage(chatInputField.text);
                }
                OpenChat(false);
            }
        }

        if (Input.GetMouseButtonDown(0) && EventSystem.current.currentSelectedGameObject != chatInputField.gameObject && EventSystem.current.currentSelectedGameObject != chatScrollbarVertical && chatInputField.gameObject.activeSelf)
        {
            OpenChat(false);
        }
    }

    // 인벤토리 열기/닫기
    private void OpenInventory(bool open)
    {
        // 열기
        if (open)
        {
            UIStack++;
            inventoryScreen.SetActive(true);
        }
        // 닫기
        else
        {
            UIStack--;
            mouseImage.sprite = null;
            mouseImage.enabled = false;
            inventoryScreen.SetActive(false);
        }

        CheckUIStack();
    }

    private void OpenPause(bool open)
    {
        if (open)
        {
            UIStack++;
            pauseScreen.SetActive(true);
        }
        else
        {
            UIStack--;
            pauseScreen.SetActive(false);
        }

        CheckUIStack();
    }

    private void OpenChat(bool open)
    {
        if (open)
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
        else
        {
            UIStack--;
            chatInputField.text = null;
            chatInputField.gameObject.SetActive(false);
            chatScrollbar.SetActive(false);
            chatBackground.enabled = false;
            HideChat();
        }

        CheckUIStack();
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

    // 인벤토리 상호작용
    private void InventoryInteraction()
    {
        if (LocalPlayer == null || !inventoryScreen.activeSelf) return;

        mouseImage.transform.position = Input.mousePosition;
        pointerData.position = Input.mousePosition;

        if (Input.GetMouseButtonDown(0) && !chatInputField.gameObject.activeSelf)
        {
            uiRaycasterResults.Clear();
            uiRaycaster.Raycast(pointerData, uiRaycasterResults);

            foreach (var result in uiRaycasterResults)
            {
                // 아이템 선택
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
                    // 다른 슬롯과 교체
                    if (result.gameObject.TryGetComponent(out Slot slot))
                    {
                        LocalPlayer.SwitchItem(selectedSlotIndex, slot.slotIndex);
                        UpdateItemSlot(selectedSlotIndex, slot.itemImage.sprite);
                        UpdateItemSlot(slot.slotIndex, mouseImage.sprite);
                        mouseImage.sprite = null;
                        mouseImage.enabled = false;
                        return;
                    }
                    // 아이템 버리기
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

    // 인벤토리 아이템 슬롯 업데이트
    public void UpdateItemSlot(int index, Sprite itemImage)
    {
        itemSlots[index].itemImage.sprite = itemImage;
        itemSlots[index].itemImage.enabled = itemImage != null;
    }

    public void HideChatDirect()
    {
        if (hideChatRoutine != null) StopCoroutine(HideChatRoutine());
        chatScreen.SetActive(false);
    }

    private void HideChat()
    {
        if (hideChatRoutine != null) StopCoroutine(HideChatRoutine());
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
}
