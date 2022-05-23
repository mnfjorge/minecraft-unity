using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreativeInventory : MonoBehaviour
{
    public GameObject slotPreFab;
    World world;

    List<ItemSlot> slots = new List<ItemSlot>();

    void Start()
    {
        world = GameObject.Find("World").GetComponent<World>();

        for (int i = 1; i < world.blockTypes.Length; i++)
        {
            var newSlot = Instantiate(slotPreFab, transform);

            var stack = new ItemStack((byte)i, 64);

            var slot = new ItemSlot(newSlot.GetComponent<UIItemSlot>(), stack);
            slot.isCreative = true;
        }
    }
}
