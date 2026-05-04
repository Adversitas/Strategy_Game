using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Generic unit template containing core attributes and autonomous movement behavior.
/// </summary>
public class Unit : MonoBehaviour, IGridTarget
{
    public GridTile AssignedTile => CurrentTile;
    [Header("Core Attributes")]
    [SerializeField] protected int strength = 1;
    [SerializeField] protected int constitution = 1;
    [SerializeField] protected int dexterity = 1;
    [SerializeField] protected int charisma = 1;
    [SerializeField] protected bool isMilitary = false;

    [Header("Combat Stats")]
    [SerializeField] protected int range = 1;

    [Header("Derived Stats (Calculated)")]
    [SerializeField] protected int attack;
    [SerializeField] protected int maxHealth;
    [SerializeField] protected int currentHealth;
    [SerializeField] protected float maxMovementPoints;
    [SerializeField] protected float currentMovementPoints;
    [SerializeField] protected int morale;
    
    [Header("Inventory")]
    [SerializeField] private int maxInventorySize = 2;
    [SerializeField] private List<Item> inventory = new List<Item>();

    [Header("Orders & Policies")]
    [SerializeField] private int maxOrderSlots = 1;
    [SerializeField] private List<Order> activeOrders = new List<Order>();

    [Header("Setup & Behavior")]
    [SerializeField] private Vector2Int startingGridPosition;
    [SerializeField] protected bool autoMove = true;

    // Current location on the grid
    public GridTile CurrentTile { get; private set; }

    // Public properties
    public int Strength => strength;
    public int Constitution => constitution;
    public int Dexterity => dexterity;
    public int Charisma => charisma;
    public int Range => range;
    public int Attack => attack;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public float MaxMovementPoints => maxMovementPoints;
    public float CurrentMovementPoints => currentMovementPoints;
    public int Morale => morale;
    public int MaxInventorySize => maxInventorySize;
    public IReadOnlyList<Item> Inventory => inventory;
    public int MaxOrderSlots => maxOrderSlots;
    public IReadOnlyList<Order> ActiveOrders => activeOrders;
    public bool IsMilitary => isMilitary;

    protected virtual void Awake()
    {
        CalculateDerivedStats();
    }

    protected virtual void Start()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.RegisterUnit(this);
        
        StartCoroutine(DelayedPlacement());
        
        if (autoMove)
        {
            StartCoroutine(RandomMoveRoutine());
        }
    }

    protected virtual void OnDestroy()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.UnregisterUnit(this);
    }

    private IEnumerator DelayedPlacement()
    {
        // If the unit has already been placed manually (e.g. via production), skip delayed placement.
        if (CurrentTile != null) yield break;

        yield return null; // Wait for grid generation

        if (SquareGridGenerator.Instance != null)
        {
            GridTile tile = SquareGridGenerator.Instance.GetTileAt(startingGridPosition);
            if (tile != null) PlaceOnTile(tile);
        }
        
        if (CurrentTile == null)
        {
            Debug.LogWarning($"Unit {name} could not find its starting tile at {startingGridPosition}");
        }
    }

    private IEnumerator RandomMoveRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            
            // Restore movement points before starting the wander
            ResetMovement();
            
            // Continue moving randomly until we run out of points or get stuck
            while (currentMovementPoints >= 0.5f)
            {
                if (CurrentTile == null) break;
                
                List<GridTile> validNeighbors = new List<GridTile>();
                
                // Scan for valid neighbors using the grid's centralized logic
                foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(CurrentTile))
                {
                    if (neighbor != null && neighbor.IsEmpty)
                    {
                        // Use centralized cost calculation
                        float cost = GetMovementCost(CurrentTile, neighbor);
                        
                        // Only consider neighbors we can actually afford to move to
                        if (currentMovementPoints >= cost)
                        {
                            validNeighbors.Add(neighbor);
                        }
                    }
                }
                
                if (validNeighbors.Count > 0)
                {
                    GridTile target = validNeighbors[Random.Range(0, validNeighbors.Count)];
                    bool success = MoveTo(target);
                    
                    if (!success) break; // Should not happen given the checks, but safety first
                    
                    // Small delay between steps so we can see the movement
                    yield return new WaitForSeconds(0.2f);
                }
                else
                {
                    // No valid moves left
                    break;
                }
            }
        }
    }

    public void Initialize(int str, int con, int dex, int cha, int rng, GridTile startTile = null)
    {
        strength = str;
        constitution = con;
        dexterity = dex;
        charisma = cha;
        range = rng;
        CalculateDerivedStats();

        if (startTile != null) PlaceOnTile(startTile);
    }

    public virtual void PlaceOnTile(GridTile tile)
    {
        if (tile == null) return;
        if (CurrentTile != null) CurrentTile.OccupyingUnit = null;
        
        CurrentTile = tile;
        tile.OccupyingUnit = this;

        transform.SetParent(tile.transform.parent);
        transform.localPosition = tile.transform.localPosition + new Vector3(0, 0, -0.1f);
        transform.localScale = Vector3.one; 
    }

    public bool MoveTo(GridTile targetTile)
    {
        if (targetTile == null || CurrentTile == null) return false;
        if (!targetTile.IsEmpty) return false;

        float cost = GetMovementCost(CurrentTile, targetTile);

        if (currentMovementPoints >= cost)
        {
            currentMovementPoints -= cost;
            PlaceOnTile(targetTile);
            return true;
        }

        return false;
    }

    public bool AddItem(Item item)
    {
        if (item == null || inventory.Count >= maxInventorySize)
        {
            return false;
        }

        inventory.Add(item);
        return true;
    }

    public bool RemoveItem(Item item)
    {
        if (item == null || !inventory.Contains(item))
        {
            return false;
        }

        inventory.Remove(item);
        return true;
    }

    public void AddOrder(Order newOrder)
    {
        if (newOrder == null || activeOrders.Count >= maxOrderSlots)
        {
            Debug.LogWarning($"Unit {name} cannot add order: {(newOrder == null ? "Null order" : "No slots available")}");
            return;
        }

        activeOrders.Add(newOrder);
        Debug.Log($"Unit {name} assigned new order: {newOrder.orderName}");
    }

    public bool RemoveOrder(Order order)
    {
        if (order == null || !activeOrders.Contains(order)) return false;
        
        activeOrders.Remove(order);
        Debug.Log($"Unit {name} removed order: {order.orderName}");
        return true;
    }

    // Keep this for backward compatibility if needed, or remove it
    public void SetOrder(Order newOrder)
    {
        activeOrders.Clear();
        if (newOrder != null) AddOrder(newOrder);
    }

    public void ResetMovement()
    {
        currentMovementPoints = maxMovementPoints;
    }

    [ContextMenu("Recalculate Stats")]
    public virtual void CalculateDerivedStats()
    {
        attack = strength * 2;
        maxHealth = constitution * 2;
        currentHealth = maxHealth;
        maxMovementPoints = dexterity * 1f;
        currentMovementPoints = maxMovementPoints;
        morale = charisma * 2;
    }

    protected virtual void OnValidate()
    {
        CalculateDerivedStats();
    }

    public float GetMovementCost(GridTile from, GridTile to)
    {
        if (from == null || to == null) return 1.0f;

        // In a hex grid, all neighbors are equidistant (base cost 1.0)
        float baseCost = 1.0f;

        // Road bonus: Moving from one road tile to another costs half
        if (from.HasRoad && to.HasRoad)
        {
            return baseCost * 0.5f;
        }

        return baseCost;
    }
}
