using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;


// 图片切换类，管理物品的显示
public class BackPack : MonoBehaviour
{
    [Header("UI元素数组")]
    public ToggleUIElements[] BackpackUiElementsArray;  // UI元素数组
    public int BackpackSelectedIndex = 0;  // 当前选中的标签

    [Header("物品槽容器 - 背包")]
    public Transform BackpackSlotContainer; // 背包容器
    public ItemSlot[] BackpackItemSlots; // 背包物品槽数组
    public UIType BackpackCurrentType;     // 当前显示的类型
    private int _selectedBackpackSlotIndex = -1;  // 当前选中的背包槽位

    public Sprite 普通_Sprite, 精美_Sprite, 稀有_Sprite, 极品_Sprite, 神品_Sprite; // 默认边框
    [Header("删除数量面板")]
    public DeleteQuantityPanel deleteQuantityPanel; // 界面B的管理类实例
    [Header("使用数量面板")]
    public DeleteQuantityPanel useQuantityPanel; // 复用删除数量面板的结构

    [Header("物品槽容器 - 装备")]
    public Transform equippedSlotContainer; // 已装备容器
    public ItemSlot[] equippedItemSlots; // 装备物品槽数组

    private int _selectedEquippedSlotIndex = -1;  // 当前选中的装备槽位

    // 用于处理装备/脱下逻辑的按钮
    [Header("操作按钮")]
    public Button takeOffButton;   // 卸下按钮
    public Button equipButton;     // 装备按钮
    public Button deleteButton;    // 添加删除按钮
    public Button useButton, BuyButton, SellButton;   // 使用按钮（新增）

    [Header("摧毁提醒")]
    public GameObject destroyPrompt; // 摧毁提醒面板
    public Button confirmButton;     // 确定按钮
    public Button cancelButton;      // 取消按钮

    [Header("背包设置")]
    public RectTransformHandler BackPackTop;

    [Header("悬浮窗")]
    public FloatingWindow FloatingWindow;
    // 在BackPack类中添加以下成员变量
    private float hoverTimer = 0f;
    public float hoverDelay = 0.5f; // 可自定义的悬浮延迟时间（秒）
    private bool isHovering = false;
    private ItemInstance hoveredItem = null;

    [Header("UI元素数组")]
    public ToggleUIElements[] ShopUiElementsArray;  // UI元素数组
    public int ShopSelectedIndex = 0;  // 当前选中的标签

    [Header("物品槽容器 - 背包")]
    public Transform ShopSlotContainer; // 背包容器
    public ItemSlot[] ShopItemSlots; // 背包物品槽数组
    public UIType ShopCurrentType;     // 当前显示的类型
    private int _selectedShopSlotIndex = -1;  // 当前选中的背包槽位

    public DeleteQuantityPanel BuyToolPanel, SellToolPanel; // 复用购买出售数量面板的结构
    [Header("数据")]
    public PlayerData PlayerData;
    public ButtonClickSound ButtonClickSound;
    [Header("测试")]
    public bool IsDeveloperMode;


    // 在InitializeButton方法中添加删除按钮事件注册
    public void InitializeButton()
    {
        // 注册卸下按钮事件
        if (takeOffButton != null)
        {
            takeOffButton.onClick.AddListener(OnTakeOffButtonClicked);
        }
        else
        {
            Debug.LogWarning("卸下按钮未赋值，请在Inspector中设置");
        }

        // 注册装备按钮事件
        if (equipButton != null)
        {
            equipButton.onClick.AddListener(OnEquipButtonClicked);
        }
        else
        {
            Debug.LogWarning("装备按钮未赋值，请在Inspector中设置");
        }

        // 注册删除按钮事件（新增）
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        }
        else
        {
            Debug.LogWarning("删除按钮未赋值，请在Inspector中设置");
        }
        // 注册使用按钮事件（新增）
        if (useButton != null)
        {
            useButton.onClick.AddListener(OnUseButtonClicked);
        }
        else
        {
            Debug.LogWarning("使用按钮未赋值，请在Inspector中设置");
        }
        useQuantityPanel.confirmButton.onClick.AddListener(() =>
        {
            UseSelectedItem(useQuantityPanel.pendingDeleteCount);
        });
        // 初始化按钮状态
        UpdateButtonStates();

        // 初始化摧毁提醒按钮
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(() => OnConfirmDestroy(deleteQuantityPanel.pendingDeleteCount));
        }
        else
        {
            Debug.LogWarning("确定按钮未赋值，请在Inspector中设置");
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelDestroy);
        }
        else
        {
            Debug.LogWarning("取消按钮未赋值，请在Inspector中设置");
        }

        // 找到现有初始化代码，添加购买按钮相关逻辑
        if (BuyButton != null)
        {
            BuyButton.onClick.AddListener(OnBuyButtonClicked);
        }

        // 初始化购买面板确认按钮事件
        if (BuyToolPanel != null && BuyToolPanel.confirmButton != null)
        {
            //BuyToolPanel.confirmButton.onClick.RemoveAllListeners();
            BuyToolPanel.confirmButton.onClick.AddListener(OnConfirmBuy);
        }

        if (SellButton != null)
        {
            SellButton.onClick.AddListener(OnSellButtonClicked);
        }

        // 初始化出售面板确认按钮事件
        if (SellToolPanel != null && SellToolPanel.confirmButton != null)
        {
            SellToolPanel.confirmButton.onClick.AddListener(OnConfirmSell);
        }


        // 初始隐藏摧毁提醒
        if (destroyPrompt != null)
        {
            destroyPrompt.SetActive(false);
        }
    }

    // 更新按钮状态方法中添加删除按钮状态控制
    private void UpdateButtonStates()
    {
        // 装备按钮仅在选中背包中类型为武器或装备的物品时可用
        if (equipButton != null)
        {
            bool isEquippable = false;
            // 检查是否选中了有效的背包槽位
            if (_selectedBackpackSlotIndex != -1 && _selectedBackpackSlotIndex < BackpackItemSlots.Length)
            {
                var selectedItem = BackpackItemSlots[_selectedBackpackSlotIndex].itemInstance;
                if (selectedItem != null)
                {
                    // 查找物品所属的UI元素类型
                    var itemType = GetItemElementTypeForCopiedItems(selectedItem, BackpackUiElementsArray);
                    // 判断是否为武器或装备类型
                    isEquippable = itemType.HasValue &&
                                  (itemType.Value == UIType.武器 || itemType.Value == UIType.装备);
                }
            }
            // 装备按钮可用条件：选中有效物品且为可装备类型
            equipButton.interactable = isEquippable;
        }

        // 卸下按钮仅在选中装备物品时可用
        if (takeOffButton != null)
        {
            takeOffButton.interactable = _selectedEquippedSlotIndex != -1 &&
                                       _selectedEquippedSlotIndex < equippedItemSlots.Length &&
                                       equippedItemSlots[_selectedEquippedSlotIndex].itemInstance != null &&
                                     equippedItemSlots[_selectedEquippedSlotIndex].contentImage.sprite != null;
        }

        // 删除按钮仅在选中背包中有物品的槽位时可用（新增）
        if (deleteButton != null)
        {
            deleteButton.interactable = _selectedBackpackSlotIndex != -1 &&
                                       _selectedBackpackSlotIndex < BackpackItemSlots.Length &&
                                       BackpackItemSlots[_selectedBackpackSlotIndex].itemInstance != null;
        }
        // 使用按钮状态控制（新增）
        if (useButton != null)
        {
            bool isFoodType = false;
            // 检查是否选中了有效的背包槽位
            if (_selectedBackpackSlotIndex != -1 && _selectedBackpackSlotIndex < BackpackItemSlots.Length)
            {
                var selectedItem = BackpackItemSlots[_selectedBackpackSlotIndex].itemInstance;
                if (selectedItem != null)
                {
                    // 查找物品所属的UI元素类型
                    var itemType = GetItemElementTypeForCopiedItems(selectedItem, BackpackUiElementsArray);
                    isFoodType = itemType.HasValue && itemType.Value == UIType.食物;
                }
            }
            useButton.interactable = isFoodType;
        }
        // 找到现有方法，添加以下代码
        if (BuyButton != null)
        {
            // 购买按钮仅在选中商店物品槽时可用
            BuyButton.interactable = _selectedShopSlotIndex != -1 &&
                                    _selectedShopSlotIndex < ShopItemSlots.Length &&
                                    ShopItemSlots[_selectedShopSlotIndex].itemInstance != null;
        }
        if (SellButton != null)
        {
            // 出售按钮仅在选中背包中有物品的槽位时可用
            SellButton.interactable = _selectedBackpackSlotIndex != -1 &&
                                     _selectedBackpackSlotIndex < BackpackItemSlots.Length &&
                                     BackpackItemSlots[_selectedBackpackSlotIndex].itemInstance != null;
        }
    }

    // 初始化物品实例
    public void InitializeuiElementsArray()
    {
        List<ItemInstance> a;
        a = new List<ItemInstance>();
        for (int i_0 = 1; i_0 < BackpackUiElementsArray.Length; i_0++)
        {
            for (int i_1 = 0; i_1 < BackpackUiElementsArray[i_0].AllItemInstance.Length; i_1++)
            {
                a.Add(BackpackUiElementsArray[i_0].AllItemInstance[i_1]);
            }
        }
        BackpackUiElementsArray[0].AllItemInstance = a.ToArray();
        a = new List<ItemInstance>();
        for (int i_0 = 1; i_0 < ShopUiElementsArray.Length; i_0++)
        {
            for (int i_1 = 0; i_1 < ShopUiElementsArray[i_0].AllItemInstance.Length; i_1++)
            {
                a.Add(ShopUiElementsArray[i_0].AllItemInstance[i_1]);
            }
        }
        ShopUiElementsArray[0].AllItemInstance = a.ToArray();
    }

    // 初始化物品槽
    public void InitializeItemSlots()
    {
        InitializeBackpackSlots();
        InitializeShopSlots();
        InitializeEquippedSlots();
    }

    // 修改初始化背包物品槽方法，添加NumberText初始化
    private void InitializeBackpackSlots()
    {
        if (BackpackSlotContainer == null)
        {
            Debug.LogError("背包容器(backpackSlotContainer)未赋值!");
            return;
        }

        int childCount = BackpackSlotContainer.childCount;
        BackpackItemSlots = new ItemSlot[childCount];

        for (int i = 0; i < childCount; i++)
        {
            BackpackItemSlots[i] = new ItemSlot();

            // 获取边框Image和按钮组件
            Transform frameTrans = BackpackSlotContainer.GetChild(i);
            BackpackItemSlots[i].frameImage = frameTrans.GetComponent<Image>();
            BackpackItemSlots[i].frameButton = frameTrans.GetComponent<Button>();

            if (BackpackItemSlots[i].frameImage == null)
            {
                Debug.LogWarning($"背包槽{i}的边框没有Image组件，将自动添加");
                BackpackItemSlots[i].frameImage = frameTrans.gameObject.AddComponent<Image>();
            }
            if (BackpackItemSlots[i].frameButton == null)
            {
                Debug.LogWarning($"背包槽{i}的边框没有Button组件，将自动添加");
                BackpackItemSlots[i].frameButton = frameTrans.gameObject.AddComponent<Button>();
            }

            // 注册槽位点击事件
            int slotIndex = i;
            BackpackItemSlots[i].frameButton.onClick.AddListener(() => OnBackpackSlotClicked(slotIndex));

            // 获取内容Image(边框的第一个子物体)
            if (frameTrans.childCount >= 1)
            {
                Transform contentTrans = frameTrans.GetChild(0);
                BackpackItemSlots[i].contentImage = contentTrans.GetComponent<Image>();
                if (BackpackItemSlots[i].contentImage == null)
                {
                    Debug.LogWarning($"背包槽{i}的第一个子物体没有Image组件，将自动添加");
                    BackpackItemSlots[i].contentImage = contentTrans.gameObject.AddComponent<Image>();
                }
                // 初始状态
                BackpackItemSlots[i].contentImage.enabled = BackpackItemSlots[i].contentImage.sprite != null;
            }
            else
            {
                Debug.LogError($"背包槽{i}没有足够的子物体(需要至少1个)");
            }

            // 获取选中边框Image(边框的第二个子物体)
            if (frameTrans.childCount >= 2)
            {
                Transform outlineTrans = frameTrans.GetChild(1);
                BackpackItemSlots[i].selectedOutlineImage = outlineTrans.GetComponent<Image>();
                if (BackpackItemSlots[i].selectedOutlineImage == null)
                {
                    Debug.LogWarning($"背包槽{i}的第二个子物体没有Image组件，将自动添加");
                    BackpackItemSlots[i].selectedOutlineImage = outlineTrans.gameObject.AddComponent<Image>();
                }
                // 初始隐藏选中边框
                BackpackItemSlots[i].selectedOutlineImage.enabled = false;
            }
            else
            {
                Debug.LogError($"背包槽{i}没有足够的子物体(需要至少2个)");
            }

            // 获取数量文本(边框的第三个子物体)
            if (frameTrans.childCount >= 3)
            {
                Transform numberTextTrans = frameTrans.GetChild(2);
                BackpackItemSlots[i].NumberText = numberTextTrans.GetComponent<Text>();
                if (BackpackItemSlots[i].NumberText == null)
                {
                    Debug.LogWarning($"背包槽{i}的第三个子物体没有Text组件，将自动添加");
                    BackpackItemSlots[i].NumberText = numberTextTrans.gameObject.AddComponent<Text>();
                }
                // 初始隐藏数量文本
                BackpackItemSlots[i].NumberText.enabled = false;
            }
            else
            {
                Debug.LogError($"背包槽{i}没有足够的子物体(需要至少3个)");
            }

            // 设置边框图片
            SetFrameSprite(BackpackItemSlots[i]);
        }
    }
    private void InitializeShopSlots()
    {
        if (ShopSlotContainer == null)
        {
            Debug.LogError("背包容器(ShopSlotContainer)未赋值!");
            return;
        }

        int childCount = ShopSlotContainer.childCount;
        ShopItemSlots = new ItemSlot[childCount];

        for (int i = 0; i < childCount; i++)
        {
            ShopItemSlots[i] = new ItemSlot();

            // 获取边框Image和按钮组件
            Transform frameTrans = ShopSlotContainer.GetChild(i);
            ShopItemSlots[i].frameImage = frameTrans.GetComponent<Image>();
            ShopItemSlots[i].frameButton = frameTrans.GetComponent<Button>();

            if (ShopItemSlots[i].frameImage == null)
            {
                Debug.LogWarning($"背包槽{i}的边框没有Image组件，将自动添加");
                ShopItemSlots[i].frameImage = frameTrans.gameObject.AddComponent<Image>();
            }
            if (ShopItemSlots[i].frameButton == null)
            {
                Debug.LogWarning($"背包槽{i}的边框没有Button组件，将自动添加");
                ShopItemSlots[i].frameButton = frameTrans.gameObject.AddComponent<Button>();
            }

            // 注册槽位点击事件
            int slotIndex = i;
            ShopItemSlots[i].frameButton.onClick.AddListener(() => OnShopSlotClicked(slotIndex));

            // 获取内容Image(边框的第一个子物体)
            if (frameTrans.childCount >= 1)
            {
                Transform contentTrans = frameTrans.GetChild(0);
                ShopItemSlots[i].contentImage = contentTrans.GetComponent<Image>();
                if (ShopItemSlots[i].contentImage == null)
                {
                    Debug.LogWarning($"背包槽{i}的第一个子物体没有Image组件，将自动添加");
                    ShopItemSlots[i].contentImage = contentTrans.gameObject.AddComponent<Image>();
                }
                // 初始状态
                ShopItemSlots[i].contentImage.enabled = ShopItemSlots[i].contentImage.sprite != null;
            }
            else
            {
                Debug.LogError($"背包槽{i}没有足够的子物体(需要至少1个)");
            }

            // 获取选中边框Image(边框的第二个子物体)
            if (frameTrans.childCount >= 2)
            {
                Transform outlineTrans = frameTrans.GetChild(1);
                ShopItemSlots[i].selectedOutlineImage = outlineTrans.GetComponent<Image>();
                if (ShopItemSlots[i].selectedOutlineImage == null)
                {
                    Debug.LogWarning($"背包槽{i}的第二个子物体没有Image组件，将自动添加");
                    ShopItemSlots[i].selectedOutlineImage = outlineTrans.gameObject.AddComponent<Image>();
                }
                // 初始隐藏选中边框
                ShopItemSlots[i].selectedOutlineImage.enabled = false;
            }
            else
            {
                Debug.LogError($"背包槽{i}没有足够的子物体(需要至少2个)");
            }

            // 获取数量文本(边框的第三个子物体)
            if (frameTrans.childCount >= 3)
            {
                Transform numberTextTrans = frameTrans.GetChild(2);
                ShopItemSlots[i].NumberText = numberTextTrans.GetComponent<Text>();
                if (ShopItemSlots[i].NumberText == null)
                {
                    Debug.LogWarning($"背包槽{i}的第三个子物体没有Text组件，将自动添加");
                    ShopItemSlots[i].NumberText = numberTextTrans.gameObject.AddComponent<Text>();
                }
                // 初始隐藏数量文本
                ShopItemSlots[i].NumberText.enabled = false;
            }
            else
            {
                Debug.LogError($"背包槽{i}没有足够的子物体(需要至少3个)");
            }

            // 设置边框图片
            SetFrameSprite(ShopItemSlots[i]);
        }
    }

    // 同样修改装备物品槽的初始化方法
    private void InitializeEquippedSlots()
    {
        if (equippedSlotContainer == null)
        {
            Debug.LogError("装备容器(equippedSlotContainer)未赋值!");
            return;
        }

        int childCount = equippedSlotContainer.childCount;
        equippedItemSlots = new ItemSlot[childCount];

        for (int i = 0; i < childCount; i++)
        {
            equippedItemSlots[i] = new ItemSlot();

            // 获取边框Image和按钮组件
            Transform frameTrans = equippedSlotContainer.GetChild(i);
            equippedItemSlots[i].frameImage = frameTrans.GetComponent<Image>();
            equippedItemSlots[i].frameButton = frameTrans.GetComponent<Button>();

            if (equippedItemSlots[i].frameImage == null)
            {
                Debug.LogWarning($"装备槽{i}的边框没有Image组件，将自动添加");
                equippedItemSlots[i].frameImage = frameTrans.gameObject.AddComponent<Image>();
            }
            if (equippedItemSlots[i].frameButton == null)
            {
                Debug.LogWarning($"装备槽{i}的边框没有Button组件，将自动添加");
                equippedItemSlots[i].frameButton = frameTrans.gameObject.AddComponent<Button>();
            }

            // 注册槽位点击事件 - 只实现选中边框的显示逻辑
            int slotIndex = i;
            equippedItemSlots[i].frameButton.onClick.AddListener(() => OnEquippedSlotClicked(slotIndex));

            // 获取内容Image(边框的第一个子物体)
            if (frameTrans.childCount >= 1)
            {
                Transform contentTrans = frameTrans.GetChild(0);
                equippedItemSlots[i].contentImage = contentTrans.GetComponent<Image>();
                if (equippedItemSlots[i].contentImage == null)
                {
                    Debug.LogWarning($"装备槽{i}的第一个子物体没有Image组件，将自动添加");
                    equippedItemSlots[i].contentImage = contentTrans.gameObject.AddComponent<Image>();
                }
                equippedItemSlots[i].contentImage.enabled = false;
            }
            else
            {
                Debug.LogError($"装备槽{i}没有足够的子物体(需要至少1个)");
            }

            // 获取选中边框Image(边框的第二个子物体)
            if (frameTrans.childCount >= 2)
            {
                Transform outlineTrans = frameTrans.GetChild(1);
                equippedItemSlots[i].selectedOutlineImage = outlineTrans.GetComponent<Image>();
                if (equippedItemSlots[i].selectedOutlineImage == null)
                {
                    Debug.LogWarning($"装备槽{i}的第二个子物体没有Image组件，将自动添加");
                    equippedItemSlots[i].selectedOutlineImage = outlineTrans.gameObject.AddComponent<Image>();
                }
                // 初始隐藏选中边框
                equippedItemSlots[i].selectedOutlineImage.enabled = false;
            }
            else
            {
                Debug.LogError($"装备槽{i}没有足够的子物体(需要至少2个)");
            }
            //// 获取数量文本(边框的第三个子物体)
            //if (frameTrans.childCount >= 3)
            //{
            //    Transform numberTextTrans = frameTrans.GetChild(2);
            //    equippedItemSlots[i].NumberText = numberTextTrans.GetComponent<Text>();
            //    if (equippedItemSlots[i].NumberText == null)
            //    {
            //        Debug.LogWarning($"装备槽{i}的第三个子物体没有Text组件，将自动添加");
            //        equippedItemSlots[i].NumberText = numberTextTrans.gameObject.AddComponent<Text>();
            //    }
            //    // 初始隐藏数量文本
            //    equippedItemSlots[i].NumberText.enabled = false;
            //}
            //else
            //{
            //    Debug.LogError($"装备槽{i}没有足够的子物体(需要至少3个)");
            //}

            // 设置边框图片
            SetFrameSprite(equippedItemSlots[i], false);
        }
    }

    // 背包槽点击事件处理
    private void OnBackpackSlotClicked(int index)
    {
        // 关闭之前选中的背包槽边框
        if (_selectedBackpackSlotIndex != -1 && _selectedBackpackSlotIndex < BackpackItemSlots.Length)
        {
            BackpackItemSlots[_selectedBackpackSlotIndex].selectedOutlineImage.enabled = false;
        }

        // 如果点击同一个槽位则取消选中状态
        if (_selectedBackpackSlotIndex == index)
        {
            _selectedBackpackSlotIndex = -1;
        }
        else
        {
            // 选中新的槽位并显示边框
            _selectedBackpackSlotIndex = index;
            if (index < BackpackItemSlots.Length)
            {
                BackpackItemSlots[index].selectedOutlineImage.enabled = true;
            }
        }

        // 更新按钮状态
        UpdateButtonStates();
    }
    private void OnShopSlotClicked(int index)
    {
        // 关闭之前选中的背包槽边框
        if (_selectedShopSlotIndex != -1 && _selectedShopSlotIndex < ShopItemSlots.Length)
        {
            ShopItemSlots[_selectedShopSlotIndex].selectedOutlineImage.enabled = false;
        }

        // 如果点击同一个槽位则取消选中状态
        if (_selectedShopSlotIndex == index)
        {
            _selectedShopSlotIndex = -1;
        }
        else
        {
            // 选中新的槽位并显示边框
            _selectedShopSlotIndex = index;
            if (index < ShopItemSlots.Length)
            {
                ShopItemSlots[index].selectedOutlineImage.enabled = true;
            }
        }

        // 更新按钮状态
        UpdateButtonStates();
    }

    // 装备槽点击事件处理 - 只实现选中边框的显示逻辑
    private void OnEquippedSlotClicked(int index)
    {
        // 关闭之前选中的装备槽边框
        if (_selectedEquippedSlotIndex != -1 && _selectedEquippedSlotIndex < equippedItemSlots.Length)
        {
            equippedItemSlots[_selectedEquippedSlotIndex].selectedOutlineImage.enabled = false;
        }

        // 如果点击同一个槽位则取消选中状态
        if (_selectedEquippedSlotIndex == index)
        {
            _selectedEquippedSlotIndex = -1;
        }
        else
        {
            // 选中新的槽位并显示边框
            _selectedEquippedSlotIndex = index;
            if (index < equippedItemSlots.Length)
            {
                equippedItemSlots[index].selectedOutlineImage.enabled = true;
            }
        }

        // 更新按钮状态
        UpdateButtonStates();
    }

    private void SetFrameSprite(ItemSlot slot, bool IsSetdefaultFrameSprite = true)
    {
        if (IsSetdefaultFrameSprite)
        {
            SetFrameSpriteByQuality(slot);
        }
    }

    private void Awake()
    {
        if (BackpackUiElementsArray == null || BackpackUiElementsArray.Length == 0)
        {
            Debug.LogError("UI元素数组未初始化或为空，请在Inspector中设置");
            return;
        }

        // 注册所有切换按钮的点击事件
        for (int i = 0; i < BackpackUiElementsArray.Length; i++)
        {
            int index = i;
            var uiElements = BackpackUiElementsArray[i];

            if (uiElements.toggleButton != null)
            {
                uiElements.toggleButton.onClick.AddListener(() => BackpackOnToggleClicked(index));
            }
            else
            {
                Debug.LogWarning($"UI元素 {index} 未分配切换按钮，可能无法正常使用");
            }
        }
        // 注册所有切换按钮的点击事件
        for (int i = 0; i < ShopUiElementsArray.Length; i++)
        {
            int index = i;
            var uiElements = ShopUiElementsArray[i];

            if (uiElements.toggleButton != null)
            {
                uiElements.toggleButton.onClick.AddListener(() => ShopOnToggleClicked(index));
            }
            else
            {
                Debug.LogWarning($"UI元素 {index} 未分配切换按钮，可能无法正常使用");
            }
        }
    }

    // 在Start方法中确保调用了AddSlotEventListeners
    private void Start()
    {
        GenerateBackpackSlots();
        GenerateShopSlots();

        deleteQuantityPanel.Initialize(this);
        useQuantityPanel.Initialize(this);
        BuyToolPanel.Initialize(this);
        SellToolPanel.Initialize(this);

        InitializeButton();
        InitializeuiElementsArray();
        FloatingWindow.Initialize();   // 初始化物品窗
        InitializeItemSlots();
        AddSlotEventListeners(); // 确保添加了事件监听
                                 // 默认选中第一个元素并更新状态
                                 // 初始化删除数量面板
        if (BackpackUiElementsArray.Length > 0)
        {
            BackpackCurrentType = BackpackUiElementsArray[0].elementType;
            UpdateBackpackToggleStates(BackpackSelectedIndex);
            // 初始化显示第一个分类的物品
            UpdateBackpackSlotContents(BackpackSelectedIndex);
            Debug.Log($"初始选中分类: {BackpackCurrentType}");
        }
        if (ShopUiElementsArray.Length > 0)
        {
            ShopCurrentType = ShopUiElementsArray[0].elementType;
            UpdateShopToggleStates(ShopSelectedIndex);
            // 初始化显示第一个分类的物品
            UpdateShopSlotContents(ShopSelectedIndex);
            Debug.Log($"初始选中分类: {ShopCurrentType}");
        }
        // 初始隐藏悬浮窗
        if (FloatingWindow.Parent != null)
        {
            FloatingWindow.Parent.gameObject.SetActive(false);
        }
        // 开发者模式下加载CSV物品
        if (IsDeveloperMode)
        {
            LoadBackpackItemsFromXlsxInDeveloperMode(PlayerData.excelPath, PlayerData.ToolSheetName);
            LoadShopItemsFromXlsxInDeveloperMode(PlayerData.excelPath, PlayerData.ToolSheetName);
        }
    }
    // 修改Update方法，添加计时器逻辑
    private void Update()
    {
        // 处理鼠标悬停计时
        if (isHovering && hoveredItem != null)
        {
            hoverTimer += Time.deltaTime;
            if (hoverTimer >= hoverDelay)
            {
                // 显示悬浮窗并更新内容
                if (FloatingWindow.Parent != null && !FloatingWindow.Parent.gameObject.activeSelf)
                {
                    FloatingWindow.Parent.gameObject.SetActive(true);
                    UpdateFloatingWindow(hoveredItem);
                }
            }
        }

        // 如果悬浮窗显示中，更新位置
        if (FloatingWindow.Parent != null && FloatingWindow.Parent.gameObject.activeSelf)
        {
            UpdateFloatingWindowPosition();
        }

        BackPackTop.MoveToEarliestRenderedParent();
    }

    private void BackpackOnToggleClicked(int index)
    {
        if (index < 0 || index >= BackpackUiElementsArray.Length)
        {
            Debug.LogError($"无效索引: {index}");
            return;
        }

        BackpackSelectedIndex = index;
        BackpackCurrentType = BackpackUiElementsArray[index].elementType;
        UpdateBackpackToggleStates(index);
        UpdateBackpackSlotContents(index); // 更新物品显示
        Debug.Log($"当前选中类别: {BackpackCurrentType}");

        // 重置选中状态并更新按钮
        ResetSelection();
        UpdateButtonStates();
    }
    private void ShopOnToggleClicked(int index)
    {
        if (index < 0 || index >= ShopUiElementsArray.Length)
        {
            Debug.LogError($"无效索引: {index}");
            return;
        }

        ShopSelectedIndex = index;
        ShopCurrentType = ShopUiElementsArray[index].elementType;
        UpdateShopToggleStates(index);
        UpdateShopSlotContents(index); // 更新物品显示
        Debug.Log($"当前选中类别: {ShopCurrentType}");

        // 重置选中状态并更新按钮
        ResetSelection();
        UpdateButtonStates();
    }

    // 重置所有选中状态
    private void ResetSelection()
    {
        // 重置背包选中状态
        if (_selectedBackpackSlotIndex != -1 && _selectedBackpackSlotIndex < BackpackItemSlots.Length)
        {
            BackpackItemSlots[_selectedBackpackSlotIndex].selectedOutlineImage.enabled = false;
        }
        _selectedBackpackSlotIndex = -1;

        // 重置装备选中状态
        if (_selectedEquippedSlotIndex != -1 && _selectedEquippedSlotIndex < equippedItemSlots.Length)
        {
            equippedItemSlots[_selectedEquippedSlotIndex].selectedOutlineImage.enabled = false;
        }
        _selectedEquippedSlotIndex = -1;
    }

    public void UpdateBackpackSlotContents(int index)
    {
        SortAllBackpackItemInstancesWhenCurrentTypeIsTotal();
        // 重置所有槽位
        foreach (var slot in BackpackItemSlots)
        {
            slot.frameImage.sprite = 普通_Sprite;
            slot.contentImage.sprite = null;
            slot.contentImage.enabled = false;
            slot.itemInstance = null;
            slot.itemCount = 0;
            UpdateItemCountDisplay(slot);
            // 先默认隐藏按钮，后续根据条件设置
            slot.frameButton.gameObject.SetActive(false);
        }

        var currentUIElement = BackpackUiElementsArray[index];
        var validItems = currentUIElement.AllItemInstance
            .Where(item => item.carrier == Carrier.无人) // 筛选背包物品
            .ToList();

        // 定义类型优先级排序（确保总分类下类型顺序正确）
        var typePriority = new Dictionary<UIType, int>
    {
        { UIType.武器, 0 },
        { UIType.装备, 1 },
        { UIType.技能书, 2 },
        { UIType.食物, 3 },
        { UIType.奇遇卡, 4 }
    };

        // 先按物品类型排序，再按品级排序
        var sortedItems = validItems
            .OrderBy(item =>
            {
                var itemType = GetItemElementTypeForCopiedItems(item, BackpackUiElementsArray);
                return itemType.HasValue ? typePriority[itemType.Value] : int.MaxValue;
            })
            .ThenBy(item => item.道具品级)
            .ToList();

        // 按类型、品级和图标分组
        var itemGroups = sortedItems
            .GroupBy(item => $"{GetItemElementTypeForCopiedItems(item, BackpackUiElementsArray)}_{item.道具品级}_{item.icon.name}_{item.GetType().Name}")
            .ToList();


        int slotIndex = 0;
        foreach (var group in itemGroups)
        {
            var itemTemplate = group.First();
            int totalCount = group.Count();
            int maxPerSlot = itemTemplate.MaxTogetherNumber;
            int slotsNeeded = Mathf.CeilToInt((float)totalCount / maxPerSlot);

            for (int i = 0; i < slotsNeeded; i++)
            {
                if (slotIndex >= BackpackItemSlots.Length) break;

                var slot = BackpackItemSlots[slotIndex];
                slot.itemInstance = itemTemplate;
                slot.contentImage.sprite = itemTemplate.icon;
                slot.contentImage.enabled = true;
                slot.itemCount = Mathf.Min(maxPerSlot, totalCount - i * maxPerSlot);

                SetFrameSprite(slot);
                UpdateItemCountDisplay(slot);
                // 有物品的槽位始终显示按钮
                slot.frameButton.gameObject.SetActive(true);

                slotIndex++;
            }
        }

        // 当当前类型是"总"时，显示所有槽位的按钮
        if (BackpackCurrentType == UIType.总) // 这里的"��"对应枚举中的"总"
        {
            foreach (var slot in BackpackItemSlots)
            {
                slot.frameButton.gameObject.SetActive(true);
            }
        }

        ResetSelection();
    }
    private void UpdateShopSlotContents(int index)
    {
        SortAllShopItemInstancesWhenCurrentTypeIsTotal();
        // 重置所有槽位
        foreach (var slot in ShopItemSlots)
        {
            slot.frameImage.sprite = 普通_Sprite;
            slot.contentImage.sprite = null;
            slot.contentImage.enabled = false;
            slot.itemInstance = null;
            slot.itemCount = 0;
            UpdateItemCountDisplay(slot);
            // 先默认隐藏按钮，后续根据条件设置
            slot.frameButton.gameObject.SetActive(false);
        }

        var currentUIElement = ShopUiElementsArray[index];
        var validItems = currentUIElement.AllItemInstance
            .Where(item => item.carrier == Carrier.无人) // 筛选背包物品
            .ToList();

        // 定义类型优先级排序（确保总分类下类型顺序正确）
        var typePriority = new Dictionary<UIType, int>
    {
        { UIType.武器, 0 },
        { UIType.装备, 1 },
        { UIType.技能书, 2 },
        { UIType.食物, 3 },
        { UIType.奇遇卡, 4 }
    };

        // 先按物品类型排序，再按品级排序
        var sortedItems = validItems
            .OrderBy(item =>
            {
                var itemType = GetItemElementTypeForCopiedItems(item, ShopUiElementsArray);
                return itemType.HasValue ? typePriority[itemType.Value] : int.MaxValue;
            })
            .ThenBy(item => item.道具品级)
            .ToList();

        // 按类型、品级和图标分组
        var itemGroups = sortedItems
            .GroupBy(item => $"{GetItemElementTypeForCopiedItems(item, ShopUiElementsArray)}_{item.道具品级}_{item.icon.name}_{item.GetType().Name}")
            .ToList();


        int slotIndex = 0;
        foreach (var group in itemGroups)
        {
            var itemTemplate = group.First();
            int totalCount = group.Count();
            int maxPerSlot = itemTemplate.MaxTogetherNumber;
            int slotsNeeded = Mathf.CeilToInt((float)totalCount / maxPerSlot);

            for (int i = 0; i < slotsNeeded; i++)
            {
                if (slotIndex >= ShopItemSlots.Length) break;

                var slot = ShopItemSlots[slotIndex];
                slot.itemInstance = itemTemplate;
                slot.contentImage.sprite = itemTemplate.icon;
                slot.contentImage.enabled = true;
                slot.itemCount = Mathf.Min(maxPerSlot, totalCount - i * maxPerSlot);

                SetFrameSprite(slot);
                UpdateItemCountDisplay(slot);
                // 有物品的槽位始终显示按钮
                slot.frameButton.gameObject.SetActive(true);

                slotIndex++;
            }
        }

        // 当当前类型是"总"时，显示所有槽位的按钮
        if (ShopCurrentType == UIType.总) // 这里的"��"对应枚举中的"总"
        {
            foreach (var slot in ShopItemSlots)
            {
                slot.frameButton.gameObject.SetActive(true);
            }
        }

        ResetSelection();
    }
    // 新增：根据道具品级设置边框Sprite的方法
    private void SetFrameSpriteByQuality(ItemSlot slot)
    {
        if (slot.itemInstance == null)
        {
            slot.frameImage.sprite = 普通_Sprite;
            return;
        }

        // 根据品级设置对应的边框
        switch (slot.itemInstance.道具品级)
        {
            case ItemInstance.品级.普通:
                slot.frameImage.sprite = 普通_Sprite;
                break;
            case ItemInstance.品级.精美:
                slot.frameImage.sprite = 精美_Sprite;
                break;
            case ItemInstance.品级.稀有:
                slot.frameImage.sprite = 稀有_Sprite;
                break;
            case ItemInstance.品级.极品:
                slot.frameImage.sprite = 极品_Sprite;
                break;
            case ItemInstance.品级.神品:
                slot.frameImage.sprite = 神品_Sprite;
                break;
            default:
                slot.frameImage.sprite = 普通_Sprite;
                break;
        }
    }
    private void UpdateBackpackToggleStates(int selectedIndex)
    {
        for (int i = 0; i < BackpackUiElementsArray.Length; i++)
        {
            var uiElements = BackpackUiElementsArray[i];
            bool isSelected = (i == selectedIndex);

            if (uiElements?.displayImage != null)
            {
                uiElements.displayImage.sprite = isSelected ?
                    uiElements.selectedSprite :
                    uiElements.unselectedSprite;
            }
            else
            {
                Debug.LogWarning($"UI元素 {i} 显示图片未正确设置，无法更新图片状态");
            }
        }
    }
    private void UpdateShopToggleStates(int selectedIndex)
    {
        for (int i = 0; i < ShopUiElementsArray.Length; i++)
        {
            var uiElements = ShopUiElementsArray[i];
            bool isSelected = (i == selectedIndex);

            if (uiElements?.displayImage != null)
            {
                uiElements.displayImage.sprite = isSelected ?
                    uiElements.selectedSprite :
                    uiElements.unselectedSprite;
            }
            else
            {
                Debug.LogWarning($"UI元素 {i} 显示图片未正确设置，无法更新图片状态");
            }
        }
    }
    public void SetSelectedIndex(int index)
    {
        if (index >= 0 && index < BackpackUiElementsArray.Length)
        {
            BackpackSelectedIndex = index;
            BackpackCurrentType = BackpackUiElementsArray[index].elementType;
            UpdateBackpackToggleStates(index);
            UpdateBackpackSlotContents(index); // 更新物品显示
            Debug.Log($"通过代码设置选中类别: {BackpackCurrentType}");

            // 重置选中状态并更新按钮
            ResetSelection();
            UpdateButtonStates();
        }
    }

    public UIType GetCurrentType()
    {
        return BackpackCurrentType;
    }

    /// <summary>
    /// 脱下按钮点击事件
    /// </summary>
    private void OnTakeOffButtonClicked()
    {
        // 检查是否有选中的装备槽位
        if (_selectedEquippedSlotIndex == -1 || _selectedEquippedSlotIndex >= equippedItemSlots.Length)
        {
            Debug.Log("请选择要脱下的装备槽位");
            return;
        }

        var selectedEquipSlot = equippedItemSlots[_selectedEquippedSlotIndex];

        // 检查选中槽位是否有物品
        if (selectedEquipSlot.itemInstance == null)
        {
            Debug.Log("选中的装备槽位没有物品");
            return;
        }

        // 将物品携带位置改为背包
        selectedEquipSlot.itemInstance.carrier = Carrier.无人;

        // 更新装备槽位显示
        selectedEquipSlot.itemInstance = null;
        selectedEquipSlot.contentImage.sprite = null;
        selectedEquipSlot.contentImage.enabled = false;
        selectedEquipSlot.selectedOutlineImage.enabled = false;
        SetFrameSprite(selectedEquipSlot, false);  // 重置装备槽位边框

        // 重置选中状态
        _selectedEquippedSlotIndex = -1;

        // 更新背包显示
        UpdateBackpackSlotContents(BackpackSelectedIndex);

        // 更新按钮状态
        UpdateButtonStates();
    }

    // 修改装备按钮点击事件，支持堆叠物品
    private void OnEquipButtonClicked()
    {
        // 检查是否选中有效的背包物品
        if (_selectedBackpackSlotIndex == -1 || _selectedBackpackSlotIndex >= BackpackItemSlots.Length)
        {
            Debug.Log("未选择要装备的物品");
            return;
        }

        var selectedBackpackSlot = BackpackItemSlots[_selectedBackpackSlotIndex];

        // 检查选中的物品是否存在
        if (selectedBackpackSlot.itemInstance == null)
        {
            Debug.Log("选中的背包槽位没有物品");
            return;
        }

        // 查找匹配的装备槽位
        int matchingSlotIndex = FindMatchingEquipSlot(selectedBackpackSlot.itemInstance);
        if (matchingSlotIndex == -1)
        {
            Debug.Log($"未找到匹配 {selectedBackpackSlot.itemInstance.icon.name} 的装备槽位");
            return;
        }

        // 处理目标装备槽中已有的物品
        var targetEquipSlot = equippedItemSlots[matchingSlotIndex];
        if (targetEquipSlot.itemInstance != null)
        {
            // 将已有装备移回背包
            targetEquipSlot.itemInstance.carrier = Carrier.无人;
        }

        // 装备选中的物品
        selectedBackpackSlot.itemInstance.carrier = PlayerData.所有人物[PlayerData.CharacterSelector.RecordIndex].名字;

        // 更新装备槽显示
        targetEquipSlot.itemInstance = selectedBackpackSlot.itemInstance;
        targetEquipSlot.contentImage.sprite = selectedBackpackSlot.contentImage.sprite;
        targetEquipSlot.contentImage.enabled = true;
        targetEquipSlot.itemCount = 1; // 装备槽中物品数量为1
        UpdateItemCountDisplay(targetEquipSlot);

        // 减少背包中物品的数量
        selectedBackpackSlot.itemCount--;
        if (selectedBackpackSlot.itemCount <= 0)
        {
            selectedBackpackSlot.itemInstance = null;
            selectedBackpackSlot.contentImage.sprite = null;
            selectedBackpackSlot.contentImage.enabled = false;
        }
        UpdateItemCountDisplay(selectedBackpackSlot);

        // 重置选择状态
        selectedBackpackSlot.selectedOutlineImage.enabled = false;
        _selectedBackpackSlotIndex = -1;

        // 更新背包显示
        UpdateBackpackSlotContents(BackpackSelectedIndex);

        // 更新按钮状态
        UpdateButtonStates();
    }
    // 修改BackPack类的OnDeleteButtonClicked方法
    private void OnDeleteButtonClicked()
    {
        if (_selectedBackpackSlotIndex == -1 || _selectedBackpackSlotIndex >= BackpackItemSlots.Length)
        {
            Debug.LogWarning("没有选中有效的背包槽位");
            return;
        }

        var selectedSlot = BackpackItemSlots[_selectedBackpackSlotIndex];
        if (selectedSlot.itemInstance == null || selectedSlot.itemCount <= 0)
        {
            Debug.LogWarning("选中的槽位没有物品");
            return;
        }

        // 如果数量大于1，显示数量选择面板，否则直接显示确认摧毁面板
        deleteQuantityPanel.Show(_selectedBackpackSlotIndex, GetTotalItemCountIn(selectedSlot.itemInstance, BackpackUiElementsArray));
        if (selectedSlot.itemCount <= 1)
        {
            deleteQuantityPanel.OnConfirm();
        }
    }
    // 4. 添加使用按钮点击事件处理方法
    // 修改使用按钮点击事件处理方法
    private void OnUseButtonClicked()
    {
        if (_selectedBackpackSlotIndex == -1 || _selectedBackpackSlotIndex >= BackpackItemSlots.Length)
            return;

        var selectedSlot = BackpackItemSlots[_selectedBackpackSlotIndex];
        if (selectedSlot.itemInstance == null)
            return;

        // 显示使用数量面板
        useQuantityPanel.Show(_selectedBackpackSlotIndex, GetTotalItemCountIn(selectedSlot.itemInstance, BackpackUiElementsArray));
        // 检查物品数量是否大于1
        if (selectedSlot.itemCount <= 1)
        {
            useQuantityPanel.OnConfirm();
            // 数量为1时直接使用
            UseSelectedItem();
        }
    }   // 完善确认删除逻辑
    // 修改OnConfirmDestroy方法，使其接受删除数量参数
    /// <summary>
    /// 确认摧毁按钮点击事件
    /// </summary>
    private void OnConfirmDestroy(int deleteCount)
    {
        if (destroyPrompt != null)
        {
            destroyPrompt.SetActive(false);
        }

        if (_selectedBackpackSlotIndex == -1 || _selectedBackpackSlotIndex >= BackpackItemSlots.Length)
            return;

        ItemSlot selectedSlot = BackpackItemSlots[_selectedBackpackSlotIndex];
        if (selectedSlot.itemInstance == null || selectedSlot.itemCount == 0)
            return;

        // 获取要删除的物品图标
        Sprite targetSprite = selectedSlot.itemInstance.icon;

        // 从ToggleUIElements的AllItemInstance中查找匹配图标的物品并移除
        foreach (var uiElement in BackpackUiElementsArray)
        {
            // 查找所有匹配图标的物品实例
            var itemsToRemove = uiElement.AllItemInstance
                .Where(item => item.icon == targetSprite)
                .Take(deleteCount)
                .ToList();

            // 从数组中移除找到的物品
            if (itemsToRemove.Count > 0)
            {
                uiElement.AllItemInstance = uiElement.AllItemInstance
                    .Except(itemsToRemove)
                    .ToArray();
            }
        }

        // 更新槽位数量
        selectedSlot.itemCount -= deleteCount;
        if (selectedSlot.itemCount <= 0)
        {
            // 清空槽位
            selectedSlot.itemInstance = null;
            selectedSlot.contentImage.sprite = null;
            selectedSlot.contentImage.enabled = false;
            selectedSlot.frameImage.sprite = 普通_Sprite;
        }

        // 更新数量显示
        UpdateItemCountDisplay(selectedSlot);

        // 重置选中状态和按钮状态
        ResetSelection();
        UpdateButtonStates();

        // 刷新背包显示
        UpdateBackpackSlotContents(BackpackSelectedIndex);
    }
    // 确保以下辅助方法与OnUseButtonClicked中使用的逻辑一致
    private ItemInstance FindItemInstanceToRemove(string uniqueId)
    {
        // 遍历所有UI元素中的物品实例，找到第一个匹配uniqueId的实例
        foreach (var uiElement in BackpackUiElementsArray)
        {
            if (uiElement.AllItemInstance != null)
            {
                var targetItem = uiElement.AllItemInstance
                    .FirstOrDefault(item => item.uniqueId == uniqueId);
                if (targetItem != null)
                {
                    return targetItem;
                }
            }
        }
        return null;
    }

    private void RemoveItemFromUIElements(ItemInstance itemToRemove)
    {
        // 从所有UI元素数组中移除目标实例（与使用逻辑保持一致）
        foreach (var uiElement in BackpackUiElementsArray)
        {
            if (uiElement.AllItemInstance != null)
            {
                uiElement.AllItemInstance = uiElement.AllItemInstance
                    .Where(item => item.uniqueId != itemToRemove.uniqueId)
                    .ToArray();
            }
        }
    }


    /// <summary>
    /// 取消摧毁道具
    /// </summary>
    private void OnCancelDestroy()
    {
        // 关闭提醒面板
        if (destroyPrompt != null)
        {
            destroyPrompt.SetActive(false);
        }
    }

    /// <summary>
    /// 查找匹配的装备槽位
    /// </summary>
    private int FindMatchingEquipSlot(ItemInstance item)
    {
        for (int i = 0; i < equippedItemSlots.Length; i++)
        {
            if (item.icon.name.Contains(equippedItemSlots[i].frameImage.name))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 判断两个RectTransform的渲染顺序
    /// </summary>
    /// <param name="a">第一个RectTransform</param>
    /// <param name="b">第二个RectTransform</param>
    /// <returns>如果a比b先渲染返回-1，a比b后渲染返回1，同时渲染返回0</returns>
    public static int CompareRenderOrder(RectTransform a, RectTransform b)
    {
        // 检查是否为同一对象
        if (a == b) return 0;

        // 获取各自的Canvas
        Canvas canvasA = a.GetComponentInParent<Canvas>();
        Canvas canvasB = b.GetComponentInParent<Canvas>();

        // Canvas为null的情况（不在UI系统中）
        if (canvasA == null && canvasB == null) return 0;
        if (canvasA == null) return -1; // 没有Canvas的先渲染
        if (canvasB == null) return 1;

        // 比较Canvas的Sorting Layer
        int layerA = SortingLayer.GetLayerValueFromID(canvasA.sortingLayerID);
        int layerB = SortingLayer.GetLayerValueFromID(canvasB.sortingLayerID);
        if (layerA != layerB)
        {
            return layerA.CompareTo(layerB);
        }

        // 比较Canvas的渲染顺序
        if (canvasA.sortingOrder != canvasB.sortingOrder)
        {
            return canvasA.sortingOrder.CompareTo(canvasB.sortingOrder);
        }

        // 检查是否属于同一Canvas
        if (canvasA != canvasB)
        {
            // 不同Canvas但排序相同，无法确定
            return 0;
        }

        // 检查层级关系，父对象比子对象先渲染
        if (IsParentOf(a, b))
        {
            return -1; // a是b的父对象，a先渲染
        }
        if (IsParentOf(b, a))
        {
            return 1; // b是a的父对象，b先渲染
        }

        // 检查是否有共同父级，比较在Hierarchy中的顺序
        Transform commonParent = FindCommonParent(a, b);
        if (commonParent != null)
        {
            int indexA = GetSiblingIndexInParent(a, commonParent);
            int indexB = GetSiblingIndexInParent(b, commonParent);
            return indexA.CompareTo(indexB);
        }

        // 没有共同父级且排序相同，无法确定
        return 0;
    }

    /// <summary>
    /// 检查parent是否是child的父对象
    /// </summary>
    private static bool IsParentOf(Transform parent, Transform child)
    {
        if (parent == null || child == null) return false;

        Transform current = child.parent;
        while (current != null)
        {
            if (current == parent)
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    /// <summary>
    /// 查找两个变换的共同父级
    /// </summary>
    private static Transform FindCommonParent(Transform a, Transform b)
    {
        if (a == null || b == null) return null;

        // 收集a的所有父级
        System.Collections.Generic.HashSet<Transform> aParents = new System.Collections.Generic.HashSet<Transform>();
        Transform current = a;
        while (current != null)
        {
            aParents.Add(current);
            current = current.parent;
        }

        // 检查b的父级是否在a的父级集合中
        current = b;
        while (current != null)
        {
            if (aParents.Contains(current))
            {
                return current;
            }
            current = current.parent;
        }

        return null;
    }

    /// <summary>
    /// 获取目标在共同父级下的层级索引（考虑嵌套情况）
    /// </summary>
    private static int GetSiblingIndexInParent(Transform target, Transform commonParent)
    {
        if (target == commonParent) return 0;

        int index = 0;
        Transform current = target;
        while (current.parent != commonParent)
        {
            current = current.parent;
        }

        return current.GetSiblingIndex();
    }
    public static void SetAsPenultimateChild(Transform childToMove, Transform newParent)
    {
        if (childToMove == null || newParent == null)
        {
            Debug.LogError("物体A或物体B为空，无法执行操作");
            return;
        }

        // 将A设置为B的子物体
        childToMove.SetParent(newParent);

        // 获取B当前的子物体数量
        int childCount = newParent.childCount;

        // 计算倒数第二个位置的索引
        // 如果只有0或1个子物体，直接放在最后即可
        int targetIndex = (childCount <= 1) ? childCount - 1 : childCount - 2;

        // 确保索引不小于0
        targetIndex = Mathf.Max(0, targetIndex);

        // 设置到目标位置
        childToMove.SetSiblingIndex(targetIndex);
    }
    /// <summary>
    /// 刷新装备槽显示，根据当前选中人物的装备
    /// </summary>
    public void RefreshEquippedSlots()
    {
        if (PlayerData == null || PlayerData.所有人物[PlayerData.CharacterSelector.RecordIndex] == null)
        {
            Debug.LogWarning("当前选中人物为空，无法刷新装备槽");
            return;
        }

        // 清空所有装备槽显示
        foreach (var slot in equippedItemSlots)
        {
            slot.itemInstance = null;
            slot.contentImage.sprite = null;
            slot.contentImage.enabled = false;
            slot.selectedOutlineImage.enabled = false;
            SetFrameSprite(slot, false);
        }

        // 获取当前人物对应的装备（遍历所有物品实例，筛选出属于当前人物的装备）
        var currentCharacter = PlayerData.所有人物[PlayerData.CharacterSelector.RecordIndex];
        var equippedItems = new List<ItemInstance>();

        // 从所有UI元素中收集属于当前人物的装备
        foreach (var uiElement in BackpackUiElementsArray)
        {
            if (uiElement.AllItemInstance == null) continue;

            equippedItems.AddRange(uiElement.AllItemInstance
                .Where(item => item.carrier == currentCharacter.名字));
        }

        // 填充装备槽
        foreach (var item in equippedItems)
        {
            int slotIndex = FindMatchingEquipSlot(item);
            if (slotIndex != -1 && slotIndex < equippedItemSlots.Length)
            {
                equippedItemSlots[slotIndex].itemInstance = item;
                equippedItemSlots[slotIndex].contentImage.sprite = item.icon;
                equippedItemSlots[slotIndex].contentImage.enabled = true;
            }
        }

        // 重置选中状态并更新按钮
        ResetSelection();
        UpdateButtonStates();
    }

    // 在BackPack类中添加以下代码

    //private void OnEnable()
    //{
    //    // 为所有物品槽添加鼠标进入和退出事件监听
    //    AddSlotEventListeners();
    //}

    //private void OnDisable()
    //{
    //    // 移除所有事件监听
    //    RemoveSlotEventListeners();
    //}

    // 鼠标进入槽位时
    private void OnSlotPointerEnter(ItemSlot slot)
    {
        if (slot.itemInstance != null)
        {
            isHovering = true;
            hoverTimer = 0f;
            hoveredItem = slot.itemInstance;
        }
    }

    /// <summary>
    /// 移除所有物品槽的鼠标事件监听
    /// </summary>
    private void RemoveSlotEventListeners()
    {
        // 移除背包物品槽的监听
        if (BackpackItemSlots != null)
        {
            foreach (var slot in BackpackItemSlots)
            {
                if (slot.frameButton != null)
                {
                    var eventTrigger = slot.frameButton.gameObject.GetComponent<EventTrigger>();
                    if (eventTrigger != null)
                    {
                        eventTrigger.triggers.Clear();
                    }
                }
            }
        }

        // 移除装备物品槽的监听
        if (equippedItemSlots != null)
        {
            foreach (var slot in equippedItemSlots)
            {
                if (slot.frameButton != null)
                {
                    var eventTrigger = slot.frameButton.gameObject.GetComponent<EventTrigger>();
                    if (eventTrigger != null)
                    {
                        eventTrigger.triggers.Clear();
                    }
                }
            }
        }
    }

    /// <summary>
    /// 向EventTrigger添加事件监听（修正版）
    /// </summary>
    private void AddEventTriggerListener(EventTrigger trigger, EventTriggerType eventType, System.Action<BaseEventData> callback)
    {
        if (trigger == null) return;

        var entry = new EventTrigger.Entry();
        entry.eventID = eventType;
        // 正确的委托添加方式
        entry.callback.AddListener(new UnityAction<BaseEventData>(callback));
        trigger.triggers.Add(entry);
    }

    // 鼠标离开槽位时
    private void OnSlotPointerExit()
    {
        isHovering = false;
        hoverTimer = 0f;
        hoveredItem = null;
        // 隐藏悬浮窗
        if (FloatingWindow.Parent != null)
        {
            FloatingWindow.Parent.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 在uiElementsArray中查找对应的ItemInstance
    /// </summary>
    private ItemInstance FindItemInstanceInUIElements(ItemInstance target)
    {
        if (BackpackUiElementsArray == null || target == null)
        {
            return null;
        }

        foreach (var uiElement in BackpackUiElementsArray)
        {
            if (uiElement.AllItemInstance != null)
            {
                foreach (var item in uiElement.AllItemInstance)
                {
                    if (item == target) // 找到匹配的物品实例
                    {
                        return item;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 更新悬浮窗显示的信息
    /// </summary>
    private void UpdateFloatingWindow(ItemInstance item)
    {
        if (FloatingWindow == null || item == null)
        {
            return;
        }

        // 更新名称（取'【'前面的内容）
        if (FloatingWindow.NameText != null)
        {
            string iconName = item.icon != null ? item.icon.name : "";
            int bracketIndex = iconName.IndexOf('（');
            FloatingWindow.NameText.text = bracketIndex > 0 ? iconName.Substring(0, bracketIndex) : iconName;
        }

        // 更新图标（使用对应UI元素的unselectedSprite）
        if (FloatingWindow.IconImage != null)
        {
            var uiElement = FindUIElementContainingItem(item);
            //FloatingWindow.IconImage.sprite = uiElement != null ? uiElement.unselectedSprite : item.icon;
            FloatingWindow.IconImage.sprite = item.icon;
        }

        // 更新描述
        if (FloatingWindow.DescriptionText != null)
        {
            FloatingWindow.DescriptionText.text = item.Description;
        }

        // 更新效果
        if (FloatingWindow.EffectText != null)
        {
            FloatingWindow.EffectText.text = item.Effect;
        }

        // 更新价格
        if (FloatingWindow.ItemPriceText != null)
        {
            if (item.CanSale)
            {
                FloatingWindow.ItemPriceText.text = $"价格：{item.Price}";
            }
            else
            {
                FloatingWindow.ItemPriceText.text = "不可买卖";
            }
        }
    }

    /// <summary>
    /// 查找包含该物品的UI元素
    /// </summary>
    private ToggleUIElements FindUIElementContainingItem(ItemInstance item)
    {
        if (BackpackUiElementsArray == null || item == null)
        {
            return null;
        }

        foreach (var uiElement in BackpackUiElementsArray)
        {
            if (uiElement.elementType != UIType.总 && uiElement.AllItemInstance != null && uiElement.AllItemInstance.Contains(item))
            {
                return uiElement;
            }
        }
        return null;
    }

    /// <summary>
    /// 更新悬浮窗位置到鼠标左上角偏移处
    /// </summary>
    private void UpdateFloatingWindowPosition()
    {
        if (FloatingWindow.Parent == null || FloatingWindow.Parent.GetComponent<RectTransform>() == null)
        {
            return;
        }

        // 获取鼠标位置
        Vector2 mousePosition = Input.mousePosition;

        // 转换为UI坐标
        RectTransform canvasRect = FloatingWindow.Parent.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mousePosition, null, out localPoint);

        // 设置偏移量（可根据需要调整）
        Vector2 offset = new Vector2(-10, 10); // 向左10，向上20的偏移

        // 设置悬浮窗位置
        RectTransform floatingRect = FloatingWindow.Parent.GetComponent<RectTransform>();
        floatingRect.anchoredPosition = localPoint + offset;

        // 确保悬浮窗不会超出屏幕范围
        //ClampFloatingWindowToScreen(floatingRect, canvasRect);
        Vector2 a_0;
        if (!IsInScreen(floatingRect, FloatingWindow.Canvas))
        {
            a_0 = floatingRect.pivot;
            a_0.y = a_0.y == 0 ? 1 : 0;
            floatingRect.pivot = a_0;
        }
    }
    /// <summary>
    /// 确保悬浮窗不会超出屏幕范围
    /// </summary>
    /// <summary>
    /// 检查RectTransform是否完全或部分超出屏幕范围
    /// </summary>
    /// <param name="rectTransform">目标RectTransform</param>
    /// <param name="outOfScreenCount">是否完全超出屏幕（true=完全超出，false=部分超出）</param>
    /// <returns>是否超出屏幕</returns>
    /// <summary>
    /// 判断UI元素是否超出屏幕范围
    /// </summary>
    /// <param name="rectTransform">目标UI的RectTransform</param>
    /// <param name="canvas">目标UI所在的Canvas（用于获取渲染相机）</param>
    /// <returns>true：超出屏幕；false：在屏幕内</returns>
    public static bool IsInScreen(RectTransform rectTransform, Canvas canvas)
    {
        if (rectTransform == null || canvas == null)
        {
            Debug.LogError("RectTransform或Canvas为空，无法判断是否超出屏幕");
            return true;
        }

        // 1. 获取屏幕范围（左下角(0,0)，右上角(Screen.width, Screen.height)）
        Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);

        // 2. 将UI的四个顶点从局部坐标转换为屏幕坐标
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners); // 获取UI四个顶点的世界坐标
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera; //  overlay模式无需相机，其他模式需指定相机

        // 3. 检查每个顶点是否都在屏幕外
        bool allCornersInScreen = true;
        foreach (Vector3 corner in corners)
        {
            // 将世界坐标转换为屏幕坐标（UI专用转换）
            Vector2 screenPoint;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Overlay模式直接使用RectTransform的局部坐标转换
                screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corner);
            }
            else
            {
                // WorldSpace或Camera模式需通过相机转换
                screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corner);
            }

            // 检查当前顶点是否在屏幕内
            if (!screenRect.Contains(screenPoint))
            {
                allCornersInScreen = false;
                break; // 只要有一个顶点在屏幕内，就不算完全超出
            }
        }
        return allCornersInScreen;
    }

    // 添加鼠标悬停事件处理
    private void AddHoverEvents(GameObject slotObject, ItemSlot slot)
    {
        // 添加鼠标进入事件
        EventTrigger trigger = slotObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = slotObject.AddComponent<EventTrigger>();
        }

        // 鼠标进入事件
        var enterEvent = new EventTrigger.Entry();
        enterEvent.eventID = EventTriggerType.PointerEnter;
        enterEvent.callback.AddListener((data) =>
        {
            OnSlotPointerEnter(slot);
        });
        trigger.triggers.Add(enterEvent);

        // 鼠标离开事件
        var exitEvent = new EventTrigger.Entry();
        exitEvent.eventID = EventTriggerType.PointerExit;
        exitEvent.callback.AddListener((data) =>
        {
            OnSlotPointerExit();
        });
        trigger.triggers.Add(exitEvent);
    }

    // 添加更新悬浮窗内容的方法
    private void UpdateFloatingWindowContent(ItemInstance item)
    {
        if (FloatingWindow.NameText != null)
            FloatingWindow.NameText.text = item.icon.name;

        if (FloatingWindow.IconImage != null)
            FloatingWindow.IconImage.sprite = item.icon;

        if (FloatingWindow.DescriptionText != null)
            FloatingWindow.DescriptionText.text = item.Description;

        if (FloatingWindow.EffectText != null)
            FloatingWindow.EffectText.text = item.Effect;

        if (FloatingWindow.ItemPriceText != null)
            FloatingWindow.ItemPriceText.text = $"价格: {item.Price}";
    }

    // 修改AddSlotEventListeners方法（如果没有则新增）
    private void AddSlotEventListeners()
    {
        // 为背包槽添加鼠标事件
        foreach (var slot in BackpackItemSlots)
        {
            AddHoverEvents(slot.frameButton.gameObject, slot);
        }

        // 为装备槽添加鼠标事件
        foreach (var slot in equippedItemSlots)
        {
            AddHoverEvents(slot.frameButton.gameObject, slot);
        }

        // 为商店槽添加鼠标事件
        foreach (var slot in ShopItemSlots)
        {
            AddHoverEvents(slot.frameButton.gameObject, slot);
        }
    }

    /// <summary>
    /// 开发者模式下从CSV文件加载物品，直接通过列索引读取B列(索引1)和F列(索引5)
    /// </summary>
    /// <param name="csvFilePath">CSV文件路径</param>
    private void LoadItemsFromCsvInDeveloperMode(string csvFilePath)
    {
        if (!IsDeveloperMode)
        {
            Debug.Log("未启用开发者模式，跳过CSV物品加载");
            return;
        }

        if (string.IsNullOrEmpty(csvFilePath) || !File.Exists(csvFilePath))
        {
            Debug.LogError($"CSV文件不存在: {csvFilePath}");
            return;
        }

        if (PlayerData == null || PlayerData.AllToolTeam == null || BackpackUiElementsArray == null)
        {
            Debug.LogError("初始化数据不完整，无法加载物品");
            return;
        }

        Debug.Log($"===== 开始从CSV加载物品: {csvFilePath} =====");

        try
        {
            var lines = File.ReadAllLines(csvFilePath);
            if (lines.Length < 2)
            {
                Debug.LogWarning("CSV文件内容不足（至少需要标题行+1行数据）");
                return;
            }

            int successCount = 0;
            // 从第二行开始读取数据（跳过标题行）
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // 使用PlayerData的CSV解析方法处理行数据（支持引号和转义）
                var columns = PlayerData.ParseCsvLine(line, ',');

                // 检查列数是否足够（至少需要11列才能读取F列）
                if (columns.Count < 11)
                {
                    Debug.LogWarning($"第{i + 1}行列数不足（需要至少11列），跳过");
                    continue;
                }

                // B列是第2列（索引1），F列是第11列（索引10）
                string itemName = columns[1].Trim();
                string countStr = columns[10].Trim();

                // 验证物品名称
                if (string.IsNullOrEmpty(itemName))
                {
                    Debug.LogWarning($"第{i + 1}行B列（索引1）物品名称为空，跳过");
                    continue;
                }

                // 验证数量
                if (!int.TryParse(countStr, out int count) || count <= 0)
                {
                    Debug.LogWarning($"第{i + 1}行F列（索引10）数量无效（值：{countStr}），跳过");
                    continue;
                }

                // 查找匹配的物品
                var matchedItems = FindMatchedItems(itemName);
                if (matchedItems == null || matchedItems.Count == 0)
                {
                    Debug.LogWarning($"第{i + 1}行未找到匹配物品：{itemName}");
                    continue;
                }

                // 添加物品到UI元素
                AddItemsToUIElements(matchedItems, count, BackpackUiElementsArray);
                successCount++;
                Debug.Log($"第{i + 1}行加载成功：{itemName} x{count}");
            }

            // 刷新背包显示
            UpdateBackpackSlotContents(BackpackSelectedIndex);
            Debug.Log($"===== CSV物品加载完成，成功加载 {successCount}/{lines.Length - 1} 行数据 =====");
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载CSV物品失败：{ex.Message}\n堆栈跟踪：{ex.StackTrace}");
        }
    }
    // 在BackPack.cs中替换LoadItemsFromCsvInDeveloperMode方法
    private void LoadBackpackItemsFromXlsxInDeveloperMode(string xlsxFilePath, string TableName)
    {
        // 检查开发者模式是否启用
        if (!IsDeveloperMode)
        {
            Debug.Log("未启用开发者模式，跳过XLSX物品加载");
            return;
        }

        // 验证文件路径有效性
        if (string.IsNullOrEmpty(xlsxFilePath) || !File.Exists(xlsxFilePath))
        {
            Debug.LogError($"XLSX文件不存在: {xlsxFilePath}");
            return;
        }

        // 验证核心数据是否初始化
        if (PlayerData == null || PlayerData.AllToolTeam == null || BackpackUiElementsArray == null)
        {
            Debug.LogError("初始化数据不完整（PlayerData/AllToolTeam/uiElementsArray为空），无法加载物品");
            return;
        }

        Debug.Log($"===== 开始从XLSX加载物品: {xlsxFilePath} =====");

        try
        {
            // 读取XLSX文件流
            using (var stream = File.Open(xlsxFilePath, FileMode.Open, FileAccess.Read))
            {
                // 创建Excel阅读器（支持.xlsx格式）
                using (var reader = ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    // 转换为DataSet（不筛选表格，后续手动筛选）
                    var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = tableReader => new ExcelDataTableConfiguration
                        {
                            UseHeaderRow = true // 使用第一行作为表头
                        }
                    });

                    // 查找"开发者模式数据"表格
                    DataTable targetTable = null;
                    foreach (DataTable table in dataSet.Tables)
                    {
                        if (table.TableName == TableName)
                        {
                            targetTable = table;
                            break;
                        }
                    }

                    if (targetTable == null)
                    {
                        Debug.LogError("未找到名为'" + TableName + "'的表格");
                        return;
                    }

                    int successCount = 0;
                    // 从第二行开始读取数据（跳过表头行）
                    for (int i = 0; i < targetTable.Rows.Count; i++)
                    {
                        DataRow row = targetTable.Rows[i];
                        if (row == null) continue;

                        // 验证列数（保持与原CSV逻辑一致，至少需要11列）
                        if (row.ItemArray.Length < 11)
                        {
                            Debug.LogWarning($"第{i + 1}行列数不足（需要至少11列），跳过");
                            continue;
                        }

                        // 读取B列（索引1）和F列（索引10），与原CSV逻辑保持一致
                        string itemName = row[1]?.ToString()?.Trim() ?? "";
                        string countStr = row[10]?.ToString()?.Trim() ?? "";

                        // 验证物品名称
                        if (string.IsNullOrEmpty(itemName))
                        {
                            Debug.LogWarning($"第{i + 1}行B列（索引1）物品名称为空，跳过");
                            continue;
                        }

                        // 验证数量有效性
                        if (!int.TryParse(countStr, out int count) || count <= 0)
                        {
                            Debug.LogWarning($"第{i + 1}行F列（索引10）数量无效（值：{countStr}），跳过");
                            continue;
                        }

                        // 查找匹配的物品（复用现有逻辑）
                        List<ItemInstance> matchedItems = FindMatchedItems(itemName);
                        if (matchedItems == null || matchedItems.Count == 0)
                        {
                            Debug.LogWarning($"第{i + 1}行未找到匹配物品：{itemName}");
                            continue;
                        }

                        // 添加物品到UI元素（复用现有逻辑）
                        AddItemsToUIElements(matchedItems, count, BackpackUiElementsArray);
                        successCount++;
                        Debug.Log($"第{i + 1}行加载成功：{itemName} x{count}");
                    }

                    // 刷新背包显示
                    UpdateBackpackSlotContents(BackpackSelectedIndex);
                    Debug.Log($"===== XLSX物品加载完成，成功加载 {successCount}/{targetTable.Rows.Count - 1} 行数据 =====");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载XLSX物品失败：{ex.Message}\n堆栈跟踪：{ex.StackTrace}");
        }
    }
    // 在BackPack.cs中替换LoadItemsFromCsvInDeveloperMode方法
    private void LoadShopItemsFromXlsxInDeveloperMode(string xlsxFilePath, string TableName)
    {
        // 检查开发者模式是否启用
        if (!IsDeveloperMode)
        {
            Debug.Log("未启用开发者模式，跳过XLSX物品加载");
            return;
        }

        // 验证文件路径有效性
        if (string.IsNullOrEmpty(xlsxFilePath) || !File.Exists(xlsxFilePath))
        {
            Debug.LogError($"XLSX文件不存在: {xlsxFilePath}");
            return;
        }

        // 验证核心数据是否初始化
        if (PlayerData == null || PlayerData.AllToolTeam == null || ShopUiElementsArray == null)
        {
            Debug.LogError("初始化数据不完整（PlayerData/AllToolTeam/uiElementsArray为空），无法加载物品");
            return;
        }

        Debug.Log($"===== 开始从XLSX加载物品: {xlsxFilePath} =====");

        try
        {
            // 读取XLSX文件流
            using (var stream = File.Open(xlsxFilePath, FileMode.Open, FileAccess.Read))
            {
                // 创建Excel阅读器（支持.xlsx格式）
                using (var reader = ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    // 转换为DataSet（不筛选表格，后续手动筛选）
                    var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = tableReader => new ExcelDataTableConfiguration
                        {
                            UseHeaderRow = true // 使用第一行作为表头
                        }
                    });

                    // 查找"开发者模式数据"表格
                    DataTable targetTable = null;
                    foreach (DataTable table in dataSet.Tables)
                    {
                        if (table.TableName == TableName)
                        {
                            targetTable = table;
                            break;
                        }
                    }

                    if (targetTable == null)
                    {
                        Debug.LogError("未找到名为'" + TableName + "'的表格");
                        return;
                    }

                    int successCount = 0;
                    // 从第二行开始读取数据（跳过表头行）
                    for (int i = 0; i < targetTable.Rows.Count; i++)
                    {
                        DataRow row = targetTable.Rows[i];
                        if (row == null) continue;

                        // 验证列数（保持与原CSV逻辑一致，至少需要16列）
                        if (row.ItemArray.Length < 16)
                        {
                            Debug.LogWarning($"第{i + 1}行列数不足（需要至少16列），跳过");
                            continue;
                        }

                        // 读取B列（索引1）和P列（索引15），与原CSV逻辑保持一致
                        string itemName = row[1]?.ToString()?.Trim() ?? "";
                        string countStr = row[15]?.ToString()?.Trim() ?? "";

                        // 验证物品名称
                        if (string.IsNullOrEmpty(itemName))
                        {
                            Debug.LogWarning($"第{i + 1}行B列（索引1）物品名称为空，跳过");
                            continue;
                        }

                        // 验证数量有效性
                        if (!int.TryParse(countStr, out int count) || count <= 0)
                        {
                            Debug.LogWarning($"第{i + 1}行P列（索引15）数量无效（值：{countStr}），跳过");
                            continue;
                        }

                        // 查找匹配的物品（复用现有逻辑）
                        List<ItemInstance> matchedItems = FindMatchedItems(itemName);
                        if (matchedItems == null || matchedItems.Count == 0)
                        {
                            Debug.LogWarning($"第{i + 1}行未找到匹配物品：{itemName}");
                            continue;
                        }

                        // 添加物品到UI元素（复用现有逻辑）
                        AddItemsToUIElements(matchedItems, count, ShopUiElementsArray);
                        successCount++;
                        Debug.Log($"第{i + 1}行加载成功：{itemName} x{count}");
                    }

                    // 刷新背包显示
                    UpdateShopSlotContents(ShopSelectedIndex);
                    Debug.Log($"===== XLSX物品加载完成，成功加载 {successCount}/{targetTable.Rows.Count - 1} 行数据 =====");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载XLSX物品失败：{ex.Message}\n堆栈跟踪：{ex.StackTrace}");
        }
    }
    /// <summary>
    /// 读取CSV数据并转换为字典列表
    /// </summary>
    private List<Dictionary<string, string>> ReadCsvData(string filePath)
    {
        var result = new List<Dictionary<string, string>>();
        try
        {
            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
            {
                Debug.LogWarning("CSV文件行数不足（至少需要标题行和一行数据）");
                return result;
            }

            // 获取标题行(假设第一行为标题: A,B,C,D,E,F...)
            var headers = lines[0].Split(',');
            Debug.Log($"CSV标题行: {string.Join(", ", headers)}");

            // 处理数据行
            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                var rowData = new Dictionary<string, string>();

                for (int j = 0; j < headers.Length && j < values.Length; j++)
                {
                    rowData[headers[j]] = values[j].Trim();
                }

                result.Add(rowData);
                Debug.Log($"读取CSV行 {i}: {string.Join("; ", rowData.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"读取CSV失败: {e.Message}");
        }
        return result;
    }

    /// <summary>
    /// 查找所有匹配名称的物品实例
    /// </summary>
    public List<ItemInstance> FindMatchedItems(string itemName)
    {
        var result = new List<ItemInstance>();

        foreach (var toolTeam in PlayerData.AllToolTeam)
        {
            if (toolTeam.AllItemInstance == null) continue;

            // 查找icon名称包含目标名称的物品
            var matchedInTeam = toolTeam.AllItemInstance
                .Where(item => item != null && item.icon != null
                            && !string.IsNullOrEmpty(item.icon.name)
                            && item.icon.name.Contains(itemName))
                .ToList();

            if (matchedInTeam.Count > 0)
            {
                Debug.Log($"在工具组 {toolTeam.elementType} 中找到 {matchedInTeam.Count} 个匹配物品");
            }

            result.AddRange(matchedInTeam);
        }

        return result;
    }

    /// <summary>
    /// 将物品添加到对应类型的UI元素
    /// </summary>
    private void AddItemsToUIElements(List<ItemInstance> items, int count, ToggleUIElements[] AllToggleUIElements)
    {
        // 使用第一个匹配的物品作为模板
        var targetItem = items[0];
        string itemIconName = targetItem.icon != null ? targetItem.icon.name : "未知物品";

        // 获取物品类型
        var itemType = GetItemElementType(targetItem);
        if (itemType == null)
        {
            Debug.LogWarning($"无法确定物品 {itemIconName} 的类型，未添加");
            return;
        }

        // 查找匹配类型的UI元素
        var targetUIElement = AllToggleUIElements.FirstOrDefault(ui => ui.elementType == itemType);
        if (targetUIElement == null)
        {
            Debug.LogWarning($"未找到类型为 {itemType} 的UI元素，物品 {itemIconName} 未添加");
            return;
        }

        // 按数量添加物品
        for (int i = 0; i < count; i++)
        {
            // 创建物品副本(避免引用同一实例)
            var newItem = new ItemInstance()
            {
                icon = targetItem.icon,
                Price = targetItem.Price,
                CanSale = targetItem.CanSale,
                Description = targetItem.Description,
                Effect = targetItem.Effect,
                MaxTogetherNumber = targetItem.MaxTogetherNumber,
                carrier = Carrier.无人, // 设置为背包携带

                攻击 = targetItem.攻击,
                防御 = targetItem.防御,
                血量 = targetItem.血量,
                内力 = targetItem.内力,
                醉意值 = targetItem.醉意值,
                虎肉食用次数 = targetItem.虎肉食用次数,
                虎鞭食用次数 = targetItem.虎鞭食用次数,

                道具品级 = targetItem.道具品级,
            };

            // 添加到UI元素
            targetUIElement.AllItemInstance = MergeItemInstances(
                targetUIElement.AllItemInstance,
                new[] { newItem }
            );

            // 同时添加到"总"类型的UI元素
            var totalUIElement = AllToggleUIElements.FirstOrDefault(ui => ui.elementType == UIType.总);
            if (totalUIElement != null)
            {
                totalUIElement.AllItemInstance = MergeItemInstances(
                    totalUIElement.AllItemInstance,
                    new[] { newItem }
                );
            }
        }

        Debug.Log($"成功添加 {count} 个 {itemIconName} 到 {itemType} 类型的UI元素中");
    }

    /// <summary>
    /// 合并物品实例数组
    /// </summary>
    private ItemInstance[] MergeItemInstances(ItemInstance[] existing, ItemInstance[] newItems)
    {
        var list = existing?.ToList() ?? new List<ItemInstance>();
        list.AddRange(newItems);
        return list.ToArray();
    }

    /// <summary>
    /// 获取物品所属的elementType
    /// </summary>
    private UIType? GetItemElementType(ItemInstance item)
    {
        foreach (var toolTeam in PlayerData.AllToolTeam)
        {
            if (toolTeam.AllItemInstance != null && toolTeam.AllItemInstance.Contains(item))
            {
                return toolTeam.elementType;
            }
        }
        return null;
    }
    /// <summary>
    /// 获取物品实例对应的UI元素类型（处理副本物品的情况）
    /// </summary>
    /// <param name="itemInstance">物品实例</param>
    /// <returns>对应的UI元素类型，找不到返回null</returns>
    public UIType? GetItemElementTypeForCopiedItems(ItemInstance itemInstance, ToggleUIElements[] AllToggleUIElements)
    {
        if (itemInstance == null) return null;

        // 遍历所有UI元素类型
        foreach (var uiElement in AllToggleUIElements)
        {
            if (uiElement.elementType == UIType.总) { continue; }
            // 检查该类型下是否有与目标物品信息匹配的原始物品
            foreach (var originalItem in uiElement.AllItemInstance)
            {
                // 通过关键属性匹配副本与原始物品（根据实际情况调整匹配条件）
                if (IsItemMatch(originalItem, itemInstance))
                {
                    return uiElement.elementType;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 检查两个物品是否为同一类（用于匹配原始物品和副本）
    /// </summary>
    private bool IsItemMatch(ItemInstance original, ItemInstance copy)
    {
        // 根据实际业务逻辑判断两个物品是否属于同一类型
        // 这里示例使用多个属性组合判断，可根据需要调整
        return original.icon == copy.icon &&
               original.Price == copy.Price &&
               original.Description == copy.Description &&
               original.Effect == copy.Effect &&
               original.攻击 == copy.攻击 &&
               original.防御 == copy.防御;
    }
    // 添加一个方法用于更新物品数量显示
    // 确保UpdateItemCountDisplay方法已实现（如果没有的话添加）
    private void UpdateItemCountDisplay(ItemSlot slot)
    {
        if (slot.NumberText != null)
        {
            if (slot.itemCount > 1)
            {
                slot.NumberText.text = slot.itemCount.ToString();
                slot.NumberText.enabled = true;
            }
            else
            {
                slot.NumberText.enabled = false;
            }
        }
    }
    // 添加处理删除的方法
    public void ProcessDelete(int slotIndex, int deleteCount)
    {
        if (slotIndex < 0 || slotIndex >= BackpackItemSlots.Length) return;

        var slot = BackpackItemSlots[slotIndex];
        if (slot.itemInstance == null) return;

        // 减少物品数量
        slot.itemCount -= deleteCount;

        // 如果数量为0，清空槽位
        if (slot.itemCount <= 0)
        {
            // 从数组中移除该物品实例
            var itemToRemove = slot.itemInstance;
            for (int i = 0; i < BackpackUiElementsArray.Length; i++)
            {
                BackpackUiElementsArray[i].AllItemInstance = BackpackUiElementsArray[i].AllItemInstance
                    .Where(item => item.uniqueId != itemToRemove.uniqueId)
                    .ToArray();
            }

            slot.itemInstance = null;
            slot.contentImage.sprite = null;
            slot.contentImage.enabled = false;
        }

        // 更新显示
        UpdateItemCountDisplay(slot);
        UpdateBackpackSlotContents(BackpackSelectedIndex);
        ResetSelection();
        UpdateButtonStates();
    }

    // 在BackPack类中添加使用物品的核心方法
    private void UseSelectedItem(int useCount = 1)
    {
        if (_selectedBackpackSlotIndex == -1 || _selectedBackpackSlotIndex >= BackpackItemSlots.Length)
            return;

        var selectedSlot = BackpackItemSlots[_selectedBackpackSlotIndex];
        if (selectedSlot.itemInstance == null || selectedSlot.itemCount <= 0)
            return;

        // 确保使用数量不超过物品数量
        //useCount = Mathf.Min(useCount, selectedSlot.itemCount);

        // 叠加使用多个物品的效果
        for (int i = 0; i < useCount; i++)
        {
            ApplyItemEffect(selectedSlot.itemInstance);
        }

        // 获取要删除的物品图标
        Sprite targetSprite = selectedSlot.itemInstance.icon;

        // 从ToggleUIElements的AllItemInstance中查找匹配图标的物品并移除
        foreach (var uiElement in BackpackUiElementsArray)
        {
            // 查找所有匹配图标的物品实例
            var itemsToRemove = uiElement.AllItemInstance
                .Where(item => item.icon == targetSprite)
                .Take(useCount)
                .ToList();

            // 从数组中移除找到的物品
            if (itemsToRemove.Count > 0)
            {
                uiElement.AllItemInstance = uiElement.AllItemInstance
                    .Except(itemsToRemove)
                    .ToArray();
            }
        }

        // 更新槽位数量
        selectedSlot.itemCount -= useCount;
        if (selectedSlot.itemCount <= 0)
        {
            // 清空槽位
            selectedSlot.itemInstance = null;
            selectedSlot.contentImage.sprite = null;
            selectedSlot.contentImage.enabled = false;
            selectedSlot.frameImage.sprite = 普通_Sprite;
        }

        // 更新数量显示
        UpdateItemCountDisplay(selectedSlot);

        // 重置选中状态和按钮状态
        ResetSelection();
        UpdateButtonStates();

        // 刷新背包显示
        UpdateBackpackSlotContents(BackpackSelectedIndex);
    }

    // 应用物品效果的方法
    private void ApplyItemEffect(ItemInstance item)
    {
        if (PlayerData == null)
        {
            Debug.LogWarning("PlayerData未赋值，无法应用物品效果");
            return;
        }

        // 应用物品效果（仅处理非零值）
        if (item.血量 > 0)
        {
            PlayerData.所有人物[PlayerData.CharacterSelector.RecordIndex].现在血量 += item.血量;
        }

        if (item.内力 > 0)
        {
            PlayerData.所有人物[PlayerData.CharacterSelector.RecordIndex].现在内力 += item.内力;
        }

        if (item.醉意值 > 0)
        {
            PlayerData.所有人物[PlayerData.CharacterSelector.RecordIndex].现在醉意值 += item.醉意值;
        }

        if (item.虎肉食用次数 > 0)
        {
            PlayerData.所有人物[PlayerData.CharacterSelector.RecordIndex].虎肉食用次数 += item.虎肉食用次数;
        }

        if (item.虎鞭食用次数 > 0)
        {
            PlayerData.所有人物[PlayerData.CharacterSelector.RecordIndex].虎鞭食用次数 += item.虎鞭食用次数;
        }
        Debug.Log($"使用了{item.icon.name}，效果已叠加");
    }

    /// <summary>
    /// 当当前类型为"总"时，对ToggleUIElements的AllItemInstance进行排序
    /// 排序顺序：武器,装备,技能书,食物,奇遇卡
    /// 应在每次更新equippedItemSlots之前调用
    /// </summary>
    private void SortAllBackpackItemInstancesWhenCurrentTypeIsTotal()
    {
        // 仅当当前类型为"总"时执行排序
        if (BackpackCurrentType != UIType.总) return;

        // 遍历所有UI元素，对AllItemInstance进行排序
        foreach (var uiElement in BackpackUiElementsArray)
        {
            // 使用OrderBy根据物品类型对应的枚举顺序进行排序
            var sortedItems = uiElement.AllItemInstance
                .OrderBy(item => GetItemTypeOrder(GetItemElementTypeForCopiedItems(item, BackpackUiElementsArray)))
                .ToArray();

            // 更新排序后的数组
            uiElement.AllItemInstance = sortedItems;
        }
    }
    private void SortAllShopItemInstancesWhenCurrentTypeIsTotal()
    {
        // 仅当当前类型为"总"时执行排序
        if (ShopCurrentType != UIType.总) return;

        // 遍历所有UI元素，对AllItemInstance进行排序
        foreach (var uiElement in ShopUiElementsArray)
        {
            // 使用OrderBy根据物品类型对应的枚举顺序进行排序
            var sortedItems = uiElement.AllItemInstance
                .OrderBy(item => GetItemTypeOrder(GetItemElementTypeForCopiedItems(item, ShopUiElementsArray)))
                .ToArray();

            // 更新排序后的数组
            uiElement.AllItemInstance = sortedItems;
        }
    }

    /// <summary>
    /// 获取物品类型对应的排序优先级
    /// </summary>
    /// <param name="uiType">物品类型</param>
    /// <returns>排序优先级（值越小越靠前）</returns>
    private int GetItemTypeOrder(UIType? uiType)
    {
        // 按照指定顺序返回优先级
        switch (uiType)
        {
            case UIType.武器:
                return 0;
            case UIType.装备:
                return 1;
            case UIType.技能书:
                return 2;
            case UIType.食物:
                return 3;
            case UIType.奇遇卡:
                return 4;
            default:
                return 5; // 未知类型放最后
        }
    }

    private void GenerateBackpackSlots()
    {
        // 1. 读取Excel中的X值
        int x = ReadXFromExcel(3, 12);
        if (x <= 1)
        {
            Debug.Log($"无需生成新槽位，X值为: {x}");
            return;
        }

        // 2. 获取模板物体
        Transform slotContainer = BackpackSlotContainer;
        if (slotContainer.childCount == 0)
        {
            Debug.LogError("backpackSlotContainer中没有子物体作为模板");
            return;
        }

        Transform templateSlot = slotContainer.GetChild(0);
        Button templateButton = templateSlot.GetComponent<Button>();
        if (templateButton == null)
        {
            Debug.LogError("模板物体上没有Button组件");
            return;
        }

        // 3. 找到对应的音效配置
        ButtonSound targetSound = FindButtonSound(templateButton);
        if (targetSound == null)
        {
            Debug.LogWarning("未找到匹配的ButtonSound配置");
            return;
        }

        // 4. 生成(x-1)个新物体
        for (int i = 0; i < x - 1; i++)
        {
            Transform newSlot = Instantiate(templateSlot, slotContainer);
            //newSlot.name = $"Slot_{slotContainer.childCount}";

            Button newButton = newSlot.GetComponent<Button>();
            if (newButton != null)
            {
                // 添加按钮点击音效事件
                AddButtonSoundEvent(newButton, targetSound.clickSound);
            }
        }

        // 5. 重新初始化背包槽位
        //InitializeBackpackSlots();
        //AddSlotEventListeners();
    }
    private void GenerateShopSlots()
    {
        // 1. 读取Excel中的X值
        int x = ReadXFromExcel(3, 17);
        if (x <= 1)
        {
            Debug.Log($"无需生成新槽位，X值为: {x}");
            return;
        }

        // 2. 获取模板物体
        Transform slotContainer = ShopSlotContainer;
        if (slotContainer.childCount == 0)
        {
            Debug.LogError("ShopSlotContainer中没有子物体作为模板");
            return;
        }

        Transform templateSlot = slotContainer.GetChild(0);
        Button templateButton = templateSlot.GetComponent<Button>();
        if (templateButton == null)
        {
            Debug.LogError("模板物体上没有Button组件");
            return;
        }

        // 3. 找到对应的音效配置
        ButtonSound targetSound = FindButtonSound(templateButton);
        if (targetSound == null)
        {
            Debug.LogWarning("未找到匹配的ButtonSound配置");
            return;
        }

        // 4. 生成(x-1)个新物体
        for (int i = 0; i < x - 1; i++)
        {
            Transform newSlot = Instantiate(templateSlot, slotContainer);
            //newSlot.name = $"Slot_{slotContainer.childCount}";

            Button newButton = newSlot.GetComponent<Button>();
            if (newButton != null)
            {
                // 添加按钮点击音效事件
                AddButtonSoundEvent(newButton, targetSound.clickSound);
            }
        }

        // 5. 重新初始化背包槽位
        //InitializeShopSlots();
        //AddSlotEventListeners();
    }

    private int ReadXFromExcel(int rowIndex, int colIndex)
    {
        try
        {
            using (var stream = File.Open(PlayerData.excelPath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        if (reader.Name == PlayerData.ToolSheetName)
                        {

                            if (reader.RowCount > rowIndex)
                            {
                                reader.Read(); // 移动到指定行
                                for (int i = 0; i < rowIndex; i++)
                                {
                                    reader.Read();
                                }

                                if (reader.FieldCount > colIndex && reader.GetValue(colIndex) != null)
                                {
                                    if (int.TryParse(reader.GetValue(colIndex).ToString(), out int result))
                                    {
                                        return result;
                                    }
                                }
                            }
                        }
                    } while (reader.NextResult());
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"读取Excel失败: {ex.Message}");
        }

        return 1; // 默认值
    }

    private ButtonSound FindButtonSound(Button targetButton)
    {
        foreach (var sound in ButtonClickSound.buttonSounds)
        {
            if (sound.targetButtons != null)
            {
                foreach (var btn in sound.targetButtons)
                {
                    if (btn == targetButton)
                    {
                        return sound;
                    }
                }
            }
        }
        return null;
    }

    private void AddButtonSoundEvent(Button button, AudioClip clip)
    {
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        // 添加点击事件
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        entry.callback.AddListener((data) =>
        {
            if (button.interactable && ButtonClickSound.audioSource != null && clip != null)
            {
                ButtonClickSound.audioSource.PlayOneShot(clip);
            }
        });
        trigger.triggers.Add(entry);
    }

    // 添加获取相同物品总数量的方法
    public int GetTotalItemCountIn(ItemInstance targetItem, ToggleUIElements[] AllToggleUIElements)
    {
        if (targetItem == null) return 0;

        // 计算背包中所有相同物品的总数量（排除elementType为'总'的UI元素）
        return AllToggleUIElements
            .Where(ui => ui.elementType != UIType.总) // 排除类型为'总'的UI元素
            .SelectMany(ui => ui.AllItemInstance)
            .Where(item => item != null
                && item.icon == targetItem.icon
                && item.GetType() == targetItem.GetType()
                && item.carrier == Carrier.无人
            ) // 保留原有筛选条件
            .Count();
    }

    // 2. 购买按钮点击事件
    private void OnBuyButtonClicked()
    {
        if (_selectedShopSlotIndex == -1 || _selectedShopSlotIndex >= ShopItemSlots.Length)
            return;

        var selectedSlot = ShopItemSlots[_selectedShopSlotIndex];
        if (selectedSlot.itemInstance == null || selectedSlot.itemCount <= 0)
            return;

        // 显示购买数量面板
        BuyToolPanel.Show(_selectedShopSlotIndex, GetTotalItemCountIn(selectedSlot.itemInstance, ShopUiElementsArray));
        // 更新购买面板确认按钮状态
        UpdateBuyPanelConfirmButtonState();
    }


    // 4. 更新购买面板确认按钮状态
    public void UpdateBuyPanelConfirmButtonState()
    {
        if (BuyToolPanel == null || BuyToolPanel.confirmButton == null)
            return;

        bool isEnabled = false;
        if (_selectedShopSlotIndex != -1 && _selectedShopSlotIndex < ShopItemSlots.Length)
        {
            var selectedItem = ShopItemSlots[_selectedShopSlotIndex].itemInstance;
            if (selectedItem != null && BuyToolPanel.quantitySlider != null)
            {
                int buyCount = (int)BuyToolPanel.quantitySlider.value;
                float totalCost = buyCount * selectedItem.Price;

                // 检查金币是否足够且背包有足够空间
                isEnabled = totalCost <= PlayerData.金币 &&
                           HasEnoughSpace(selectedItem, buyCount, BackpackUiElementsArray, BackpackItemSlots)
                           && selectedItem.CanSale;
            }
        }
        BuyToolPanel.confirmButton.interactable = isEnabled;
    }

    // 5. 检查背包是否有足够空间
    // 更新检查背包空间的方法，基于"总"类型UI元素的物品数量和堆叠上限计算
    // 修正检查背包空间的方法（修复itemGroups.First使用错误）
    public bool HasEnoughSpace(ItemInstance item, int count, ToggleUIElements[] AllUiElementsArray, ItemSlot[] AllItemSlot)
    {
        if (item == null || count <= 0) return false;

        // 获取"总"类型的UI元素（包含所有背包物品）
        var totalUIElement = AllUiElementsArray.FirstOrDefault(ui => ui.elementType == UIType.总);
        if (totalUIElement == null || totalUIElement.AllItemInstance == null)
        {
            Debug.LogWarning("未找到'总'类型的UI元素，无法计算背包空间");
            return false;
        }

        // 1. 计算现有同类型物品可堆叠的剩余空间
        int stackableSpace = 0;
        // 按物品类型分组统计（复用现有逻辑）
        var itemGroups = totalUIElement.AllItemInstance
            .GroupBy(item => $"{GetItemElementTypeForCopiedItems(item, AllUiElementsArray)}_{item.道具品级}_{item.icon.name}_{item.GetType().Name}")
            .ToList();

        // 找到当前物品对应的分组（修复分组查找逻辑）
        var targetGroupKey = $"{GetItemElementTypeForCopiedItems(item, AllUiElementsArray)}_{item.道具品级}_{item.icon.name}_{item.GetType().Name}";
        var targetGroup = itemGroups.FirstOrDefault(g => g.Key == targetGroupKey); // 关键修复：使用FirstOrDefault并比较Key

        if (targetGroup != null && targetGroup.Any())
        {
            // 计算该分组已占用的槽位数
            int totalInGroup = targetGroup.Count();
            int maxPerSlot = item.MaxTogetherNumber;
            int fullSlots = totalInGroup / maxPerSlot;
            int remainingInLastSlot = totalInGroup % maxPerSlot;
            remainingInLastSlot = remainingInLastSlot == 0 ? maxPerSlot : remainingInLastSlot;
            // 最后一个未满的槽位可堆叠空间
            //print("totalInGroup:" + totalInGroup);
            //print("maxPerSlot:" + maxPerSlot);
            //print("remainingInLastSlot:" + remainingInLastSlot);
            stackableSpace = maxPerSlot - remainingInLastSlot;
        }

        // 2. 计算总槽位数和已使用槽位数（基于所有物品）
        int totalSlots = AllItemSlot.Length;
        int usedSlots = 0;

        // 计算所有物品占用的槽位数
        foreach (var group in itemGroups)
        {
            int groupCount = group.Count();
            int maxPerSlot = group.First().MaxTogetherNumber; // 这里的First是有效的，因为group是IEnumerable<ItemInstance>
            usedSlots += Mathf.CeilToInt((float)groupCount / maxPerSlot);
        }

        // 3. 计算剩余空槽位
        int emptySlots = totalSlots - usedSlots;
        int spaceFromEmptySlots = emptySlots * item.MaxTogetherNumber;

        // 总可用空间 = 可堆叠空间 + 空槽位能容纳的空间
        int totalAvailableSpace = stackableSpace + spaceFromEmptySlots;
        //print("stackableSpace:" + stackableSpace);
        //print("spaceFromEmptySlots:" + spaceFromEmptySlots);
        //print("totalSlots:" + totalSlots);
        //print("usedSlots:" + usedSlots);
        //print("emptySlots:" + emptySlots);
        //print("totalAvailableSpace:" + totalAvailableSpace);
        //print("count:" + count);
        return totalAvailableSpace >= count;
    }
    // 6. 确认购买事件
    private void OnConfirmBuy()
    {
        if (BuyToolPanel == null || _selectedShopSlotIndex == -1 ||
            _selectedShopSlotIndex >= ShopItemSlots.Length)
            return;

        int buyCount = BuyToolPanel.pendingDeleteCount;
        var shopSlot = ShopItemSlots[_selectedShopSlotIndex];
        var itemToBuy = shopSlot.itemInstance;

        if (itemToBuy == null || buyCount <= 0)
            return;

        // 计算总成本
        float totalCost = buyCount * itemToBuy.Price;

        // 再次检查条件（防止并发问题）
        if (totalCost > PlayerData.金币 || !HasEnoughSpace(itemToBuy, buyCount, BackpackUiElementsArray, BackpackItemSlots))
            return;

        // 扣除金币
        PlayerData.金币 -= (int)totalCost;

        // 从商店移除相应数量物品
        RemoveAllUiElementsArrayItems(itemToBuy, buyCount, ShopUiElementsArray);

        // 向背包添加相应数量物品
        AddItemsToAllUiElementsArray(itemToBuy, buyCount, BackpackUiElementsArray);

        // 隐藏购买面板
        if (BuyToolPanel.Panel != null)
            BuyToolPanel.Panel.SetActive(false);

        // 刷新界面
        UpdateShopSlotContents(ShopSelectedIndex);
        UpdateBackpackSlotContents(BackpackSelectedIndex);
        ResetSelection();
        UpdateButtonStates();
    }

    // 7. 从商店移除物品
    private void RemoveAllUiElementsArrayItems(ItemInstance item, int count, ToggleUIElements[] AllUiElementsArray)
    {
        foreach (var uiElement in AllUiElementsArray)
        {
            var itemsToRemove = uiElement.AllItemInstance
                .Where(i => AreItemsSameType(i, item))
                .Take(count)
                .ToList();

            if (itemsToRemove.Count > 0)
            {
                uiElement.AllItemInstance = uiElement.AllItemInstance
                    .Except(itemsToRemove)
                    .ToArray();
                //count -= itemsToRemove.Count;

                if (count <= 0)
                    break;
            }
        }
    }

    // 8. 向背包添加物品
    /// <summary>
    /// 将新物品添加到背包对应的UI元素数组
    /// 根据物品图标匹配PlayerData中AllToolTeam的物品，确定所属类型后添加到对应UI元素
    /// </summary>
    /// <param name="newItem">要添加的新物品</param>
    /// <param name="count">添加数量</param>
    public void AddItemsToAllUiElementsArray(ItemInstance newItem, int count, ToggleUIElements[] AllUiElementsArray)
    {
        if (newItem == null || count <= 0 || PlayerData == null || PlayerData.AllToolTeam == null)
        {
            Debug.LogWarning("添加物品失败：参数无效或PlayerData未初始化");
            return;
        }

        // 1. 查找PlayerData中AllToolTeam里与新物品图标一致的物品，确定所属ToolTeam
        var matchedToolTeam = PlayerData.AllToolTeam
            .FirstOrDefault(team => team.AllItemInstance != null
                && team.AllItemInstance.Any(item => item != null
                    && item.icon == newItem.icon)); // 核心匹配逻辑：图标一致

        if (matchedToolTeam == null)
        {
            Debug.LogWarning($"未找到与物品 {newItem.icon?.name} 图标匹配的ToolTeam，无法添加到背包");
            return;
        }

        // 2. 根据ToolTeam的elementType查找背包中对应的UI元素
        var targetUIElement = AllUiElementsArray
            .FirstOrDefault(ui => ui.elementType == matchedToolTeam.elementType);

        if (targetUIElement == null)
        {
            Debug.LogWarning($"未找到类型为 {matchedToolTeam.elementType} 的背包UI元素，物品添加失败");
            return;
        }

        // 3. 按数量添加物品到目标UI元素（创建副本避免引用冲突）
        for (int i = 0; i < count; i++)
        {
            var itemCopy = new ItemInstance()
            {
                uniqueId = Guid.NewGuid().ToString(), // 生成唯一ID
                icon = newItem.icon,
                Price = newItem.Price,
                CanSale = newItem.CanSale,
                Description = newItem.Description,
                Effect = newItem.Effect,
                MaxTogetherNumber = newItem.MaxTogetherNumber,
                carrier = Carrier.无人, // 背包物品携带状态为无人
                攻击 = newItem.攻击,
                防御 = newItem.防御,
                道具品级 = newItem.道具品级,
                // 复制其他必要属性
                血量 = newItem.血量,
                内力 = newItem.内力,
                醉意值 = newItem.醉意值,
                虎肉食用次数 = newItem.虎肉食用次数,
                虎鞭食用次数 = newItem.虎鞭食用次数
            };

            // 添加到目标UI元素的物品数组
            targetUIElement.AllItemInstance = MergeItemInstances(
                targetUIElement.AllItemInstance,
                new[] { itemCopy }
            );

            // 同时添加到"总"类型的UI元素（包含所有物品）
            var totalUIElement = AllUiElementsArray.FirstOrDefault(ui => ui.elementType == UIType.总);
            if (totalUIElement != null)
            {
                totalUIElement.AllItemInstance = MergeItemInstances(
                    totalUIElement.AllItemInstance,
                    new[] { itemCopy }
                );
            }
        }

        Debug.Log($"成功添加 {count} 个 {newItem.icon?.name} 到 {matchedToolTeam.elementType} 类型的背包UI元素");

    }
    // 9. 辅助方法：获取物品对应的背包UI元素
    private ToggleUIElements GetBackpackUiElementForItem(ItemInstance item)
    {
        // 根据物品类型找到对应的UI元素（根据实际逻辑调整）
        foreach (var uiElement in BackpackUiElementsArray)
        {
            // 这里假设通过物品图标名称判断类型，实际应根据具体逻辑调整
            if (uiElement.AllItemInstance.Any(i => i.icon == item.icon))
            {
                return uiElement;
            }
        }
        return null;
    }

    // 补充物品类型匹配方法（替代AreItemsSameType）
    private bool AreItemsSameType(ItemInstance item1, ItemInstance item2)
    {
        if (item1 == null || item2 == null)
            return false;

        // 基于关键属性判断是否为同一类型物品（与IsItemMatch逻辑保持一致）
        return item1.icon == item2.icon &&
               item1.Price == item2.Price &&
               item1.Description == item2.Description &&
               item1.Effect == item2.Effect &&
               item1.攻击 == item2.攻击 &&
               item1.防御 == item2.防御 &&
               item1.道具品级 == item2.道具品级;
    }

    // 出售按钮点击事件
    private void OnSellButtonClicked()
    {
        if (_selectedBackpackSlotIndex == -1 || _selectedBackpackSlotIndex >= BackpackItemSlots.Length)
            return;

        var selectedSlot = BackpackItemSlots[_selectedBackpackSlotIndex];
        if (selectedSlot.itemInstance == null || selectedSlot.itemCount <= 0)
            return;

        // 显示出售数量面板
        SellToolPanel.Show(_selectedBackpackSlotIndex, GetTotalItemCountIn(selectedSlot.itemInstance, BackpackUiElementsArray));
        // 更新出售面板确认按钮状态
        UpdateSellPanelConfirmButtonState();
    }

    // 更新出售面板确认按钮状态
    public void UpdateSellPanelConfirmButtonState()
    {
        if (SellToolPanel == null || SellToolPanel.confirmButton == null)
            return;

        bool isEnabled = false;

        if (_selectedBackpackSlotIndex != -1 && _selectedBackpackSlotIndex < BackpackItemSlots.Length)
        {
            var selectedItem = BackpackItemSlots[_selectedBackpackSlotIndex].itemInstance;
            if (selectedItem != null && SellToolPanel.quantitySlider != null)
            {
                int sellCount = (int)SellToolPanel.quantitySlider.value;

                // 只检查商店是否有足够空间，与金币无关
                isEnabled = HasEnoughSpace(selectedItem, sellCount, ShopUiElementsArray, ShopItemSlots) && selectedItem.CanSale;
            }
        }
        SellToolPanel.confirmButton.interactable = isEnabled;
    }

    // 确认出售事件
    private void OnConfirmSell()
    {
        if (SellToolPanel == null || _selectedBackpackSlotIndex == -1 ||
            _selectedBackpackSlotIndex >= BackpackItemSlots.Length)
            return;

        int sellCount = SellToolPanel.pendingDeleteCount;
        var backpackSlot = BackpackItemSlots[_selectedBackpackSlotIndex];
        var itemToSell = backpackSlot.itemInstance;

        if (itemToSell == null || sellCount <= 0)
            return;

        // 再次检查商店空间（防止并发问题）
        if (!HasEnoughSpace(itemToSell, sellCount, ShopUiElementsArray, ShopItemSlots))
            return;

        // 计算售价（总价格的一半）
        float totalRevenue = (sellCount * itemToSell.Price) / 2;
        PlayerData.金币 += (int)totalRevenue;

        // 从背包移除相应数量物品
        RemoveAllUiElementsArrayItems(itemToSell, sellCount, BackpackUiElementsArray);

        // 向商店添加相应数量物品
        AddItemsToAllUiElementsArray(itemToSell, sellCount, ShopUiElementsArray);

        // 隐藏出售面板
        if (SellToolPanel.Panel != null)
            SellToolPanel.Panel.SetActive(false);

        // 刷新界面
        UpdateBackpackSlotContents(BackpackSelectedIndex);
        UpdateShopSlotContents(ShopSelectedIndex);
        ResetSelection();
        UpdateButtonStates();
    }

    public void RemoveRandomItemFromBackpack()
    {
        // 1. 优先在非"总"分类中找有物品的分类
        var nonTotalElements = BackpackUiElementsArray
            .Where(e => e.elementType != UIType.总 && e.AllItemInstance.Length > 0)
            .ToArray();

        if (nonTotalElements.Length > 0)
        {
            // 随机选择一个非"总"分类
            var randomElement = nonTotalElements[UnityEngine.Random.Range(0, nonTotalElements.Length)];
            if (randomElement.AllItemInstance.Length > 0)
            {
                // 随机选择一个道具
                int randomIndex = UnityEngine.Random.Range(0, randomElement.AllItemInstance.Length);
                var itemToRemove = randomElement.AllItemInstance[randomIndex];
                string itemName = itemToRemove.icon.name;

                // 从当前分类中移除
                var tempList = new List<ItemInstance>(randomElement.AllItemInstance);
                tempList.RemoveAt(randomIndex);
                randomElement.AllItemInstance = tempList.ToArray();

                // 2. 在"总"分类中找到同名道具并移除一个
                var totalElement = BackpackUiElementsArray.FirstOrDefault(e => e.elementType == UIType.总);
                if (totalElement != null && totalElement.AllItemInstance.Length > 0)
                {
                    var itemInTotal = totalElement.AllItemInstance.FirstOrDefault(i => i.icon.name == itemName);
                    if (itemInTotal != null)
                    {
                        var totalList = new List<ItemInstance>(totalElement.AllItemInstance);
                        totalList.Remove(itemInTotal);
                        totalElement.AllItemInstance = totalList.ToArray();
                    }
                }

                Debug.Log($"丢失物品: {itemName}");
                UpdateBackpackSlotContents(BackpackSelectedIndex);
                return;
            }
        }

        Debug.Log("背包中没有非'总'分类物品可移除");
    }
}
    // 定义UI元素类型枚举
    public enum UIType
{
    总,
    武器,
    装备,
    技能书,
    食物,
    奇遇卡
}
public enum Carrier
{
    无人,
    武松,
    张青,
    孙二娘,
    杨志
}
// UI元素的数据结构
[System.Serializable]
public class ToggleUIElements
{
    [Header("UI元素")]
    public Image displayImage;        // 显示的图片
    public Button toggleButton;       // 用于切换的按钮
    public Sprite unselectedSprite;   // 未选中状态的图片
    public Sprite selectedSprite;     // 选中状态的图片

    [Header("类型")]
    public UIType elementType;        // 该UI元素对应的类型

    [Header("物品")]
    public ItemInstance[] AllItemInstance;
}

// 物品实例类
[System.Serializable]
public class ItemInstance
{
    [Header("基础")]
    public string uniqueId; // 唯一ID（关键）
    public Sprite icon;             // 物品图标
    public float Price;
    public bool CanSale;
    public string Description;
    public string Effect;
    public int MaxTogetherNumber;
    public Carrier carrier;         // 携带位置
    public 品级 道具品级;
    [Header("加成")]
    public int 攻击; public int 防御, 血量, 内力, 醉意值;
    public int 虎肉食用次数, 虎鞭食用次数;
    // 构造时生成唯一ID
    public ItemInstance()
    {
        uniqueId = Guid.NewGuid().ToString(); // 确保唯一性
    }

    public enum 品级
    {
        普通,
        精美,
        稀有,
        极品,
        神品
    }
}

// 物品槽类
[System.Serializable]
public class ItemSlot
{
    public ItemInstance itemInstance; // 包含的物品实例
    public Button frameButton;
    public Image frameImage; // 边框Image
    public Image contentImage; // 内容Image
    public Image selectedOutlineImage; // 选中状态显示的边框Image
    public Text NumberText;
    public int itemCount = 0; // 物品数量
}

// 悬浮窗
[System.Serializable]
public class FloatingWindow // 类A（悬浮窗相关）
{
    public Transform Parent;
    // 悬浮窗中的各个UI元素
    public Text NameText;
    public Image IconImage;
    public Text DescriptionText;
    public Text EffectText;
    public Text ItemPriceText;

    public Canvas Canvas;

    /// <summary>
    /// 初始化悬浮窗UI元素
    /// </summary>
    /// <param name="parentB">父物体B</param>
    public void Initialize()
    {
        if (Parent == null)
        {
            Debug.LogError("父物体B不能为空，无法初始化悬浮窗UI元素");
            return;
        }

        // 初始化名称文本（父物体B的第0个子物体的第0个子物体）
        Transform nameTextTrans = GetChildTransform(Parent, 0, 0);
        if (nameTextTrans != null)
        {
            NameText = nameTextTrans.GetComponent<Text>();
            if (NameText == null)
            {
                Debug.LogWarning("名称文本物体上未找到TextMeshProUGUI组件");
            }
        }
        else
        {
            Debug.LogWarning("未找到名称文本对应的物体");
        }

        // 初始化图标（父物体B的第0个子物体的第1个子物体）
        Transform iconImageTrans = GetChildTransform(Parent, 0, 1);
        if (iconImageTrans != null)
        {
            IconImage = iconImageTrans.GetComponent<Image>();
            if (IconImage == null)
            {
                Debug.LogWarning("图标物体上未找到Image组件");
            }
        }
        else
        {
            Debug.LogWarning("未找到图标对应的物体");
        }

        // 初始化描述介绍文本（父物体B的第1个子物体的第1个子物体）
        Transform descTextTrans = GetChildTransform(Parent, 1, 1);
        if (descTextTrans != null)
        {
            DescriptionText = descTextTrans.GetComponent<Text>();
            if (DescriptionText == null)
            {
                Debug.LogWarning("描述介绍文本物体上未找到TextMeshProUGUI组件");
            }
        }
        else
        {
            Debug.LogWarning("未找到描述介绍文本对应的物体");
        }

        // 初始化效果介绍文本（父物体B的第2个子物体的第1个子物体）
        Transform effectTextTrans = GetChildTransform(Parent, 2, 1);
        if (effectTextTrans != null)
        {
            EffectText = effectTextTrans.GetComponent<Text>();
            if (EffectText == null)
            {
                Debug.LogWarning("效果介绍文本物体上未找到TextMeshProUGUI组件");
            }
        }
        else
        {
            Debug.LogWarning("未找到效果介绍文本对应的物体");
        }

        // 初始化物品价格文本（父物体B的第3个子物体）
        Transform priceTextTrans = GetChildTransform(Parent, 3);
        if (priceTextTrans != null)
        {
            ItemPriceText = priceTextTrans.GetComponent<Text>();
            if (ItemPriceText == null)
            {
                Debug.LogWarning("物品价格文本物体上未找到TextMeshProUGUI组件");
            }
        }
        else
        {
            Debug.LogWarning("未找到物品价格文本对应的物体");
        }
        Canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
    }
    /// <summary>
    /// 获取子物体的Transform（单层子物体）
    /// </summary>
    private Transform GetChildTransform(Transform parent, int childIndex)
    {
        if (parent.childCount > childIndex)
        {
            return parent.GetChild(childIndex);
        }
        Debug.LogError($"父物体 {parent.name} 不存在索引为 {childIndex} 的子物体");
        return null;
    }

    /// <summary>
    /// 获取子物体的Transform（双层子物体）
    /// </summary>
    private Transform GetChildTransform(Transform parent, int firstLevelIndex, int secondLevelIndex)
    {
        Transform firstLevelChild = GetChildTransform(parent, firstLevelIndex);
        if (firstLevelChild != null)
        {
            return GetChildTransform(firstLevelChild, secondLevelIndex);
        }
        return null;
    }
}
[System.Serializable]
public class DeleteQuantityPanel
{
    [Header("界面")]
    public GameObject Panel; // 界面B面板
    public Text reminderText; // 提醒文本
    public Slider quantitySlider; // 数量滑块
    public int pendingDeleteCount;
    public Text minQuantityText; // 最小数量文本
    public Text maxQuantityText; // 最大数量文本
    public Button confirmButton; // 确定按钮
    public Button cancelButton; // 取消按钮
    public string reminderContent;

    private int currentMaxQuantity; // 当前最大可删除数量
    private int selectedSlotIndex; // 选中的槽位索引
    private BackPack backpack; // 背包引用

    [Header("买卖")]
    public bool CanTrade; public TradeStage CurrentTradeStage;
    public enum TradeStage
    {
        buy,
        sell
    }

    /// <summary>
    /// 初始化删除数量面板
    /// </summary>
    public void Initialize(BackPack backpackInstance)
    {
        backpack = backpackInstance;

        // 注册按钮事件
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        // 初始隐藏面板
        if (Panel != null)
            Panel.SetActive(false);
    }

    /// <summary>
    /// 显示面板并设置参数
    /// </summary>
    // 修改DeleteQuantityPanel类的Show方法，设置提醒文本
    public void Show(int slotIndex, int maxQuantity)
    {
        if (Panel == null) return;

        selectedSlotIndex = slotIndex;
        currentMaxQuantity = maxQuantity;

        // 设置滑块范围
        if (quantitySlider != null)
        {
            quantitySlider.minValue = 1;
            quantitySlider.maxValue = currentMaxQuantity;
            quantitySlider.value = 1; // 默认删除1个
        }

        // 更新文本显示
        if (minQuantityText != null)
            minQuantityText.text = "1";

        if (maxQuantityText != null)
            maxQuantityText.text = currentMaxQuantity.ToString();

        // 设置提醒文本
        if (reminderText != null && backpack != null &&
            slotIndex >= 0 && slotIndex < backpack.BackpackItemSlots.Length)
        {
            var selectedItem = backpack.BackpackItemSlots[slotIndex].itemInstance;
            if (this == backpack.BuyToolPanel) { selectedItem = backpack.ShopItemSlots[slotIndex].itemInstance; }
            if (selectedItem != null && selectedItem.icon != null)
            {
                // 获取'sprite.name'中'('前面的内容
                string itemName = selectedItem.icon.name.Split('（')[0];
                reminderText.text = reminderContent + $"{quantitySlider.value}个{itemName}";
                if (CanTrade)
                {
                    if (CurrentTradeStage == TradeStage.buy)
                    {
                        reminderText.text += "，需要花费" + quantitySlider.value * selectedItem.Price;
                    }
                    else if (CurrentTradeStage == TradeStage.sell)
                    {
                        reminderText.text += "，能卖出" + quantitySlider.value * selectedItem.Price * 0.5f;
                    }
                }
                // 监听滑块值变化，实时时更新提醒文本
                quantitySlider.onValueChanged.RemoveAllListeners();
                quantitySlider.onValueChanged.AddListener((value) =>
                {
                    reminderText.text = reminderContent + $"{(int)value}个{itemName}";
                    if (backpack != null)
                    {
                        if (this == backpack.BuyToolPanel)
                        {
                            backpack.UpdateBuyPanelConfirmButtonState();
                        }
                        else if (this == backpack.SellToolPanel)
                        {
                            backpack.UpdateSellPanelConfirmButtonState();
                        }
                    }
                    if (CanTrade)
                    {
                        if (CurrentTradeStage == TradeStage.buy)
                        {
                            reminderText.text += "，需要花费" + (int)value * selectedItem.Price;
                        }
                        else if (CurrentTradeStage == TradeStage.sell)
                        {
                            reminderText.text += "，能卖出" + (int)value * selectedItem.Price * 0.5f;
                        }
                    }
                });
            }
        }
        // 显示面板
        Panel.SetActive(true);
    }

    /// <summary>
    /// 确定按钮点击事件
    /// </summary>
    // 修改DeleteQuantityPanel类的OnConfirm方法
    public void OnConfirm()
    {
        if (backpack == null || selectedSlotIndex < 0) return;

        int deleteCount = quantitySlider != null ? (int)quantitySlider.value : 1;

        // 隐藏数量选择面板
        if (Panel != null)
            Panel.SetActive(false);

        // 显示摧毁确认面板
        if (Panel.name.Contains("摧毁"))
        {
            if (backpack.destroyPrompt != null)
            {
                backpack.destroyPrompt.SetActive(true);
            }
        }
        // 存储要删除的数量，供确认时使用
        pendingDeleteCount = deleteCount;

    }

    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void OnCancel()
    {
        if (Panel != null)
            Panel.SetActive(false);
    }
}
/// <summary>
/// 存储父物体RectTransform及其目标位置信息的类（类A）
/// </summary>
[System.Serializable]
public class ParentRectData
{
    [Tooltip("作为父物体的RectTransform")]
    public RectTransform parentRect;

    [Tooltip("转移后子物体的锚点位置")]
    public Vector3 targetPosition;
}

/// <summary>
/// 处理RectTransform转移逻辑的类（类B）
/// </summary>
[System.Serializable]
public class RectTransformHandler
{
    [Tooltip("需要被转移的RectTransform")]
    public RectTransform targetRect;
    public int CountDownNumber;
    [Tooltip("所有可能的父物体数据数组")]
    public ParentRectData[] parentDatas;

    public Canvas Canvas;

    /// <summary>
    /// 将目标RectTransform移动到最早渲染的父物体下
    /// </summary>
    public void MoveToEarliestRenderedParent()
    {
        if (targetRect == null || parentDatas == null || parentDatas.Length == 0)
        {
            Debug.LogWarning("目标RectTransform或父物体数组未正确设置");
            return;
        }

        ParentRectData earliestParent = FindEarliestRenderedParent();

        if (earliestParent != null && earliestParent.parentRect != null)
        {
            TransferToParent(earliestParent);
        }
        else
        {
            Debug.LogWarning("未找到有效的父物体");
        }
    }

    /// <summary>
    /// 查找最早渲染的父物体（高层级优先比较）
    /// </summary>
    private ParentRectData FindEarliestRenderedParent()
    {
        if (parentDatas.Length == 0) return null;

        // 获取所有父物体到Canvas的层级路径
        var hierarchyPaths = new (ParentRectData data, int[] path)[parentDatas.Length];

        for (int i = 0; i < parentDatas.Length; i++)
        {
            if (parentDatas[i]?.parentRect != null)
            {
                hierarchyPaths[i] = (parentDatas[i], GetHierarchyPath(parentDatas[i].parentRect));
            }
        }

        // 以第一个有效父物体作为初始参考
        ParentRectData earliest = null;
        int[] earliestPath = null;

        foreach (var (data, path) in hierarchyPaths)
        {
            if (data?.parentRect == null || path == null) continue;

            if (earliest == null)
            {
                earliest = data;
                earliestPath = path;
                continue;
            }

            // 比较层级路径，高层级优先
            if (!IsEarlierInHierarchy(path, earliestPath))
            {
                earliest = data;
                earliestPath = path;
            }
        }

        return earliest;
    }

    /// <summary>
    /// 获取从RectTransform到Canvas的层级路径（每个层级的siblingIndex）
    /// 路径索引0表示最靠近Canvas的层级，数值越大层级越深
    /// </summary>
    private int[] GetHierarchyPath(RectTransform rect)
    {
        // 查找最顶层的Canvas
        Transform current = rect;

        while (current != null)
        {
            if (Canvas != null) break;
            current = current.parent;
        }

        if (Canvas == null)
        {
            Debug.LogWarning($"{rect.name}不在任何Canvas下");
            return null;
        }

        // 收集从rect到Canvas的所有父物体的siblingIndex
        System.Collections.Generic.List<int> path = new System.Collections.Generic.List<int>();
        current = rect;

        while (current != null && current != Canvas.transform)
        {
            path.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        // 反转列表，使索引0为最靠近Canvas的层级
        path.Reverse();
        return path.ToArray();
    }

    /// <summary>
    /// 比较两个层级路径，判断哪个在层级中更靠前（渲染更早）
    /// 优先比较高层级（靠近Canvas）的索引，一旦有差异就返回结果
    /// </summary>
    private bool IsEarlierInHierarchy(int[] pathA, int[] pathB)
    {
        int minLength = Mathf.Min(pathA.Length, pathB.Length);

        // 从最高层级（最靠近Canvas）开始比较
        for (int i = 0; i < minLength; i++)
        {
            if (pathA[i] < pathB[i])
            {
                // A在当前层级更靠前，直接返回true
                return true;
            }
            else if (pathA[i] > pathB[i])
            {
                // B在当前层级更靠前，直接返回false
                return false;
            }
            // 如果相等则继续比较下一层级
        }

        // 如果前面的层级都相等，则层级较少的（更靠近Canvas）更靠前
        return pathA.Length < pathB.Length;
    }

    /// <summary>
    /// 将目标RectTransform转移到指定父物体下
    /// </summary>
    private void TransferToParent(ParentRectData parentData)
    {
        if (targetRect.parent != parentData.parentRect)
        {
            targetRect.SetParent(parentData.parentRect);
            int childCount = parentData.parentRect.childCount;
            int targetIndex = Mathf.Max(0, childCount - CountDownNumber);
            targetRect.SetSiblingIndex(targetIndex);
            targetRect.localScale = Vector3.one;
            targetRect.localRotation = Quaternion.identity;
            targetRect.anchoredPosition3D = parentData.targetPosition;

            Debug.Log($"已将{targetRect.name}转移到父物体{parentData.parentRect.name}下");
        }
    }
}