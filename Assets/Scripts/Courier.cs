using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A high-mobility civilian unit designed for rapid movement.
/// </summary>
public class Courier : Unit
{
    [Header("Courier Logistics")]
    [SerializeField] private float heldProduction = 0;
    [SerializeField] private float maxProductionCapacity = 50;

    public float HeldProduction => heldProduction;
    public float MaxProductionCapacity => maxProductionCapacity;
    public IGridTarget TargetDestination { get; set; }
    public City OriginCity { get; set; }
    private bool _isReturning = false;


    protected override void Awake()
    {
        // Couriers are non-military and focus on speed (Dexterity)
        isMilitary = false;
        strength = 0;
        dexterity = 2; 
        constitution = 1;
        charisma = 1;
        autoMove = false;
        
        base.Awake();
    }

    protected void Update()
    {

        // Autonomous movement logic
        if (currentMovementPoints >= 1.0f)
        {
            IGridTarget currentGoal = _isReturning ? (IGridTarget)OriginCity : TargetDestination;
            
            if (currentGoal != null && currentGoal.AssignedTile != null && currentGoal.AssignedTile != CurrentTile)
            {
                GridTile nextStep = GetNextStepOnRoad(CurrentTile, currentGoal.AssignedTile);
                if (nextStep != null)
                {
                    // Move to next tile
                    PlaceOnTile(nextStep);
                    currentMovementPoints -= 1.0f;
                }
            }
        }
    }

    private GridTile GetNextStepOnRoad(GridTile start, GridTile target)
    {
        if (start == null || target == null) return null;

        // BFS to find shortest road path
        Queue<GridTile> queue = new Queue<GridTile>();
        Dictionary<GridTile, GridTile> cameFrom = new Dictionary<GridTile, GridTile>();
        
        queue.Enqueue(start);
        cameFrom[start] = null;

        while (queue.Count > 0)
        {
            GridTile current = queue.Dequeue();

            if (current == target)
            {
                // Reconstruct path to find the first step from 'start'
                GridTile step = target;
                while (cameFrom[step] != null && cameFrom[step] != start)
                {
                    step = cameFrom[step];
                }
                return step;
            }

            foreach (var neighbor in SquareGridGenerator.Instance.GetNeighbors(current))
            {
                // Can move to neighbor if it has a road OR it is the target (City/Facility)
                bool isTarget = (neighbor == target);
                bool hasRoad = neighbor.HasRoad;

                if (!cameFrom.ContainsKey(neighbor) && (hasRoad || isTarget))
                {
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return null; // No road path found
    }

    public override void PlaceOnTile(GridTile tile)
    {
        base.PlaceOnTile(tile);

        if (tile == null) return;

        // 1. Check for Destination Arrival (Facility)
        if (!_isReturning && TargetDestination != null && TargetDestination.AssignedTile == tile)
        {
            Debug.Log($"[Courier] {name} reached destination at {tile.GridPosition}");
            
            Facility facility = tile.OccupyingBuilding?.GetComponent<Facility>();
            if (facility != null)
            {
                ExtractFromFacility(facility);
                _isReturning = true; // Automatically head back after loading
            }
        }
        // 2. Check for Origin Return (City)
        else if (_isReturning && OriginCity != null && OriginCity.AssignedTile == tile)
        {
            Debug.Log($"[Courier] {name} returned to origin city at {tile.GridPosition}");
            UnloadAtCity(OriginCity);
            _isReturning = false; // Ready for next assignment
        }
        // 3. Fallback check for buildings on tile even if not goal
        else if (tile.OccupyingBuilding != null)
        {
            Facility facility = tile.OccupyingBuilding.GetComponent<Facility>();
            if (facility != null && !_isReturning)
            {
                ExtractFromFacility(facility);
                if (heldProduction >= maxProductionCapacity) _isReturning = true;
            }
        }
    }

    private void ExtractFromFacility(Facility facility)
    {
        // Calculate extraction amount
        float spaceLeft = maxProductionCapacity - heldProduction;
        float available = facility.ProductionStockpile;
        float toTake = Mathf.Min(spaceLeft, available);

        if (toTake > 0)
        {
            facility.DeductProduction(toTake);
            heldProduction += toTake;
            Debug.Log($"[Courier] {name} extracted {toTake:F1} production from facility. Total held: {heldProduction:F1}/{maxProductionCapacity}");
        }
    }

    private void UnloadAtCity(City city)
    {
        if (heldProduction > 0)
        {
            city.AddProduction(heldProduction);
            Debug.Log($"[Courier] {name} delivered {heldProduction:F1} production to {city.name}.");
            heldProduction = 0;
        }
    }
}
