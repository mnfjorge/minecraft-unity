using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIItemSlot : MonoBehaviour
{
    public bool isLinked = false;
    public ItemSlot itemSlot;
    public Image slotImage;
    public Image slotIcon;
    public Text slotAmount;

    World world;

    void Awake()
    {
        world = GameObject.Find("World").GetComponent<World>();
    }

    public bool HasItem
    {
        get
        {
            if (itemSlot == null)
                return false;
            else
                return itemSlot.hasItem;
        }
    }

    public void Link(ItemSlot itemSlot)
    {
        this.itemSlot = itemSlot;
        isLinked = true;
        itemSlot.LinkUISlot(this);
        UpdateSlot();
    }

    public void UnLink()
    {
        itemSlot.unLinkUISlot();
        itemSlot = null;
        UpdateSlot();
    }

    public void UpdateSlot()
    {
        if (itemSlot != null && itemSlot.hasItem)
        {
            slotIcon.sprite = world.blockTypes[itemSlot.stack.id].icon;
            slotAmount.text = itemSlot.stack.amount.ToString();
            slotIcon.enabled = true;
            slotAmount.enabled = true;
        }
        else
        {
            Clear();
        }
    }

    public void Clear()
    {
        slotIcon.sprite = null;
        slotAmount.text = "";
        slotIcon.enabled = false;
        slotAmount.enabled = false;
    }

    void OnDestroy()
    {
        if (isLinked)
            itemSlot.unLinkUISlot();
    }
}

public class ItemSlot
{
    public ItemStack stack = null;
    private UIItemSlot uiItemSlot = null;

    public ItemSlot(UIItemSlot slot)
    {
        stack = null;
        uiItemSlot = slot;
        uiItemSlot.Link(this);
    }

    public ItemSlot(UIItemSlot uiItemSlot, ItemStack stack)
    {
        this.uiItemSlot = uiItemSlot;
        this.stack = stack;
        uiItemSlot.Link(this);
    }

    public void LinkUISlot(UIItemSlot uiSlot)
    {
        uiItemSlot = uiSlot;
    }

    public void unLinkUISlot()
    {
        uiItemSlot = null;
    }

    public void EmptySlot()
    {
        stack = null;
        if (uiItemSlot != null)
            uiItemSlot.UpdateSlot();
    }

    public int Take(int amt)
    {
        if (amt > stack.amount)
        {
            int amount = stack.amount;
            EmptySlot();
            return amount;
        }
        else if (amt < stack.amount)
        {
            stack.amount -= amt;
            uiItemSlot.UpdateSlot();
            return amt;
        }
        else
        {
            EmptySlot();
            return amt;
        }
    }

    public bool hasItem
    {
        get { return stack != null; }
    }
}