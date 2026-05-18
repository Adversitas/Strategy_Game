using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Generic unit template containing core attributes and autonomous movement behavior.
/// </summary>
public class Unit : MonoBehaviour, IGridTarget
{
    public enum RoamOrderMode
    {
        FreeRoam,
        PatrolRoam
    }

    public GridTile AssignedTile => CurrentTile;
    [Header("Core Attributes")]
    [SerializeField] protected int strength = 1;
    [SerializeField] protected int constitution = 1;
    [SerializeField] protected int dexterity = 1;
    [SerializeField] protected int agility = 1;
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
    [SerializeField] private RoamOrderMode roamOrderMode = RoamOrderMode.FreeRoam;

    [Header("Combat Awareness")]
    [SerializeField] private Unit detectedEnemy;

    [Header("Setup & Behavior")]
    [SerializeField] private Vector2Int startingGridPosition;
    [SerializeField] protected bool autoMove = true;

    // Current location on the grid
    public GridTile CurrentTile { get; protected set; }

    // Public properties
    public int Strength => strength;
    public int Constitution => constitution;
    public int Dexterity => dexterity;
    public int Agility => agility;
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
    public Unit DetectedEnemy => detectedEnemy;
    public RoamOrderMode CurrentRoamOrderMode => roamOrderMode;
    public string CurrentRoamOrderLabel => roamOrderMode == RoamOrderMode.PatrolRoam ? "Patrol Roam" : "Free Roam";

    protected virtual void Awake()
    {
        CalculateDerivedStats();
    }

    protected virtual void Update()
    {
        RegenerateMovementOverTime(Time.deltaTime);
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
            yield return new WaitForSeconds(0.1f);

            if (isMilitary)
            {
                detectedEnemy = FindVisibleEnemy();
                if (detectedEnemy != null)
                {
                    Debug.Log($"[Unit] {name} spotted enemy {detectedEnemy.name} at {detectedEnemy.CurrentTile.GridPosition}");
                    continue;
                }
            }

            if (activeOrders.Count > 0)
            {
                continue;
            }

            if (CurrentTile == null || currentMovementPoints < 0.5f)
            {
                continue;
            }

            if (isMilitary)
            {
                detectedEnemy = FindVisibleEnemy();
                if (detectedEnemy != null)
                {
                    Debug.Log($"[Unit] {name} spotted enemy {detectedEnemy.name} at {detectedEnemy.CurrentTile.GridPosition}");
                    continue;
                }
            }

            List<GridTile> validNeighbors = new List<GridTile>();

            foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(CurrentTile))
            {
                if (neighbor != null && neighbor.IsEmpty)
                {
                    if (!IsValidRoamDestination(neighbor))
                    {
                        continue;
                    }

                    float cost = GetMovementCost(CurrentTile, neighbor);
                    if (currentMovementPoints >= cost)
                    {
                        validNeighbors.Add(neighbor);
                    }
                }
            }

            if (validNeighbors.Count > 0)
            {
                GridTile target = validNeighbors[Random.Range(0, validNeighbors.Count)];
                MoveTo(target);
            }
        }
    }

    public void CycleRoamOrderMode()
    {
        roamOrderMode = roamOrderMode == RoamOrderMode.FreeRoam
            ? RoamOrderMode.PatrolRoam
            : RoamOrderMode.FreeRoam;

        Debug.Log($"[Unit] {name} roam order set to {CurrentRoamOrderLabel}");
    }

    public void SetRoamOrderMode(RoamOrderMode newMode)
    {
        roamOrderMode = newMode;
    }

    private Unit FindVisibleEnemy()
    {
        if (!isMilitary || CurrentTile == null || AssetRegistry.Instance == null) return null;

        foreach (Unit unit in AssetRegistry.Instance.Units)
        {
            if (unit == null || unit == this || unit.CurrentTile == null) continue;
            if (!IsEnemyUnit(unit)) continue;
            if (GetHexDistance(CurrentTile.GridPosition, unit.CurrentTile.GridPosition) <= range)
            {
                return unit;
            }
        }

        return null;
    }

    private bool IsEnemyUnit(Unit other)
    {
        if (other == null || other == this) return false;

        PlayerOwnership myOwnership = GetComponent<PlayerOwnership>();
        PlayerOwnership otherOwnership = other.GetComponent<PlayerOwnership>();

        if (myOwnership == null || otherOwnership == null)
        {
            return false;
        }

        return myOwnership.OwnerId != otherOwnership.OwnerId;
    }

    private bool IsValidRoamDestination(GridTile tile)
    {
        if (tile == null) return false;
        if (!isMilitary) return true;
        if (roamOrderMode != RoamOrderMode.PatrolRoam) return true;

        return IsWithinPatrolRangeOfFriendlyStructure(tile);
    }

    private bool IsWithinPatrolRangeOfFriendlyStructure(GridTile tile)
    {
        if (tile == null || AssetRegistry.Instance == null) return false;

        foreach (City city in AssetRegistry.Instance.Cities)
        {
            if (city == null || city.AssignedTile == null) continue;
            if (!IsFriendlyStructure(city.gameObject)) continue;
            if (GetHexDistance(tile.GridPosition, city.AssignedTile.GridPosition) <= 2)
            {
                return true;
            }
        }

        foreach (Facility facility in AssetRegistry.Instance.Facilities)
        {
            if (facility == null || facility.AssignedTile == null) continue;
            if (!IsFriendlyStructure(facility.gameObject)) continue;
            if (GetHexDistance(tile.GridPosition, facility.AssignedTile.GridPosition) <= 2)
            {
                return true;
            }
        }

        return false;
    }

    public virtual bool IsFriendly(Unit other)
    {
        if (other == null) return true;

        PlayerOwnership myOwnership = GetComponent<PlayerOwnership>();
        PlayerOwnership otherOwnership = other.GetComponent<PlayerOwnership>();

        // If either is missing ownership, we don't treat them as hostile by default
        // to prevent getting stuck on uninitialized units.
        if (myOwnership == null || otherOwnership == null) return true;

        return myOwnership.OwnerId == otherOwnership.OwnerId;
    }

    private bool IsFriendlyStructure(GameObject structure)
    {
        if (structure == null) return false;

        PlayerOwnership myOwnership = GetComponent<PlayerOwnership>();
        PlayerOwnership structureOwnership = structure.GetComponent<PlayerOwnership>();

        if (myOwnership == null || structureOwnership == null)
        {
            return myOwnership == null && structureOwnership == null;
        }

        return myOwnership.OwnerId == structureOwnership.OwnerId;
    }

    private static int GetHexDistance(Vector2Int a, Vector2Int b)
    {
        int dq = a.x - b.x;
        int dr = a.y - b.y;
        int ds = (-a.x - a.y) - (-b.x - b.y);
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
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
        
        // Safety: Only clear the old tile if it actually belongs to THIS unit.
        // This prevents high-mobility units (like couriers) from clearing a tile 
        // that still has a valid citizen on it if they accidentally overlapped.
        if (CurrentTile != null && CurrentTile.OccupyingUnit == this)
        {
            CurrentTile.OccupyingUnit = null;
        }
        
        CurrentTile = tile;
        tile.OccupyingUnit = this;

        transform.SetParent(tile.transform.parent);
        transform.localPosition = tile.transform.localPosition + new Vector3(0, 0, -0.1f);
        TileObjectScale.ApplyTo(gameObject);
    }

    public bool MoveTo(GridTile targetTile)
    {
        if (targetTile == null || CurrentTile == null) return false;
        
        // Safety check: Cannot move into a tile already occupied by someone else
        if (!targetTile.IsEmpty && targetTile.OccupyingUnit != this) return false;

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

    protected void RegenerateMovementOverTime(float deltaTime)
    {
        if (maxMovementPoints <= 0f || deltaTime <= 0f) return;

        float turnDuration = GetTurnDuration();
        if (turnDuration <= 0f) return;

        float movementPerSecond = maxMovementPoints / turnDuration;
        currentMovementPoints = Mathf.Min(maxMovementPoints, currentMovementPoints + movementPerSecond * deltaTime);
    }

    public float GetTurnDuration()
    {
        return 5f / Mathf.Max(1, agility);
    }

    [ContextMenu("Recalculate Stats")]
    public virtual void CalculateDerivedStats()
    {
        attack = strength * 2;
        maxHealth = constitution * 2;
        currentHealth = maxHealth;
        maxMovementPoints = dexterity * 1f;
        currentMovementPoints = Mathf.Clamp(currentMovementPoints, 0f, maxMovementPoints);
        if (!Application.isPlaying)
        {
            currentMovementPoints = maxMovementPoints;
        }
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
