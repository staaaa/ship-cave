using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class Equipment : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private int initialCapacity = 3;
    [SerializeField] private Transform itemsContainer;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private Vector3 holdLocalPosition = Vector3.zero;
    [SerializeField] private Quaternion holdLocalRotation = Quaternion.identity;
    
    [Header("Pickup / Drop")]
    [SerializeField] private float pickupDistance = 2.5f;
    [SerializeField] private float dropThrowForce = 2f;
    
    [Header("Misc")]
    [SerializeField] private InputActionAsset inputActions;
    
    //internal
    private List<Item> _items;
    private int _selectedIndex = -1;
    private int _currentCapacity = -1;
    private Camera _cam;
    private Rigidbody _rb;

    private InputAction _pickupAction;
    private InputAction _dropAction;
    private InputAction _selectSlotAction;
    
    private void Awake()
    {
        _cam = Camera.main;
        _rb =  GetComponent<Rigidbody>();

        //initialize container if not present
        if (itemsContainer == null)
        {
            GameObject containerGO = new GameObject("InventoryItems");
            containerGO.transform.SetParent(transform, false);
            itemsContainer = containerGO.transform;
        }
        
        //initialize item list
        _items = new List<Item>(initialCapacity);
        for (int i = 0; i < initialCapacity; i++)
            _items.Add(null);
        _currentCapacity = initialCapacity;
        
        //initialize hold point if not present
        if (holdPoint == null)
        {
            GameObject hp = new GameObject("HoldPoint");
            hp.transform.SetParent(transform, false);
            hp.transform.localPosition = new Vector3(0f, 0.5f, 1f);
            holdPoint = hp.transform;
        }
        
        _pickupAction = inputActions["Pickup"];
        _dropAction = inputActions["Drop"];
        _selectSlotAction = inputActions["SelectSlot"];
        
        _selectedIndex = -1;
    }
    
    private void OnEnable()
    {
        inputActions?.Enable();
        if (_selectSlotAction != null)
            _selectSlotAction.performed += OnSelectSlotPerformed;
    }

    private void OnDisable()
    {
        if (_selectSlotAction != null)
            _selectSlotAction.performed -= OnSelectSlotPerformed;
        inputActions?.Disable();
    }

    private void Update()
    {
        if (_pickupAction != null && _pickupAction.ReadValue<float>() > 0f)
        {
            TryPickup();
        }

        if (_dropAction != null && _dropAction.ReadValue<float>() > 0f)
        {
            DropItem(_selectedIndex);
        }
    }

    private void TryPickup()
    {
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
        {
            if (!hit.collider.CompareTag("Item")) return;

            Item item = hit.collider.GetComponentInParent<Item>();
            if (item == null) return;
            
            if (AddToInventory(item))
            {
                Debug.Log($"Picked up: {item.name}");
            }
            else
            {
                Debug.Log("No free inventory slot to pick up the item.");
            }
        }
    }

    private void DropItem(int slot)
    {
        if (_selectedIndex < 0) return;
        if (_selectedIndex >= _items.Count) return;

        Item item = _items[slot];
        if (item == null) return;
        
        Vector3 dropPos = (holdPoint != null) ? holdPoint.position : transform.position + transform.forward * 1f + Vector3.up * 0.5f;
        Vector3 throwForce = transform.forward * dropThrowForce + Vector3.up * 0.5f;
        
        _items[_selectedIndex] = null;
        
        item.OnDrop(dropPos, throwForce, _rb);
    }
    
    private bool AddToInventory(Item item)
    {
        if (item == null) return false;

        if (_items[_selectedIndex] != null)
        {
            DropItem(_selectedIndex);
        }

        StoreItem(item, _selectedIndex);
        return true;
    }

    private void StoreItem(Item item, int slot)
    {
        if (slot < 0 || slot > initialCapacity) return;
        _items[slot] = item;
        item.OnPickup(_rb);
        item.OnEquip(holdPoint, holdLocalPosition, holdLocalRotation);
    }

    private void ToggleSelectSlot(int index)
    {
        if (index < 0 || index >= _currentCapacity) return;
        
        if (_selectedIndex == index)
        {
            UnequipCurrent();
        }
        else
        {
            if (_selectedIndex != -1)
            {
                UnequipCurrent();
            }
            EquipSlot(index);
        }
        Debug.Log("OBECNIE WYBRANY SLOT:" + _selectedIndex);
    }

    private void EquipSlot(int index)
    {
        _selectedIndex = index;
        Item item = _items[index];
        if (item == null) return;
        item.OnEquip(holdPoint, holdLocalPosition, holdLocalRotation);
    }

    private void UnequipCurrent()
    {
        Item item = _items[_selectedIndex];
        if (item != null)
        {
            item.OnStore(itemsContainer);
        }

        _selectedIndex = -1;
    }
    
    private void OnSelectSlotPerformed(InputAction.CallbackContext ctx)
    {
        if (ctx.control is KeyControl keyControl)
        {
            Key key = keyControl.keyCode;
            
            if (key >= Key.Digit1 && key <= Key.Digit9)
            {
                int slotIndex = (int)key - (int)Key.Digit1;
                ToggleSelectSlot(slotIndex);
            }
        }
    }
}
