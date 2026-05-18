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
    [SerializeField] private bool repeatEnabled = false;

    public float HeldProduction => heldProduction;
    public float MaxProductionCapacity => maxProductionCapacity;
    public bool RepeatEnabled => repeatEnabled;
    
    private IGridTarget targetDestination;
    public IGridTarget TargetDestination
    {
        get => targetDestination;
        set
        {
            targetDestination = value;
            EnsureOriginCity();
            _isReturning = GetTargetConstructionProject() != null && heldProduction <= 0f;
            Debug.Log($"[Courier] {name} assigned destination: {(targetDestination != null ? targetDestination.GetType().Name : "NULL")}, origin={(OriginCity != null ? OriginCity.name : "NULL")}, held={heldProduction:F1}, returning={_isReturning}");
        }
    }

    public City OriginCity { get; set; }
    private bool _isReturning = false;

    public void SetRepeatEnabled(bool enabled)
    {
        repeatEnabled = enabled;
        Debug.Log($"[Courier] {name} repeat mode set to {repeatEnabled}");
    }

    public void ToggleRepeat()
    {
        SetRepeatEnabled(!repeatEnabled);
    }


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

    protected override void Update()
    {
        base.Update();
        ProcessCurrentTileLogistics();

        // Autonomous movement logic
        if (currentMovementPoints >= 0.5f)
        {
            IGridTarget currentGoal = _isReturning ? (IGridTarget)OriginCity : TargetDestination;
            
            if (currentGoal != null && currentGoal.AssignedTile != null && currentGoal.AssignedTile != CurrentTile)
            {
                GridTile nextStep = GetNextStepOnRoad(CurrentTile, currentGoal.AssignedTile);
                if (nextStep != null)
                {
                    float cost = GetMovementCost(CurrentTile, nextStep);
                    if (currentMovementPoints < cost)
                    {
                        return;
                    }

                    // Check if the next step is physically blocked by an enemy
                    bool isEnemyBlocked = nextStep.OccupyingUnit != null && !IsFriendly(nextStep.OccupyingUnit);

                    if (!isEnemyBlocked)
                    {
                        // Path is clear or friendly: Proceed
                        PlaceOnTile(nextStep);
                        currentMovementPoints -= cost;
                    }
                    else
                    {
                        // Path is blocked by an enemy: Stop and wait in front of them
                        if (Time.frameCount % 240 == 0)
                        {
                            Unit blocker = nextStep.OccupyingUnit;
                            int myId = GetComponent<PlayerOwnership>()?.OwnerId ?? -1;
                            int otherId = blocker.GetComponent<PlayerOwnership>()?.OwnerId ?? -1;
                            Debug.LogWarning($"[Courier] {name}(ID:{myId}) is waiting for unit {blocker.name}(ID:{otherId}) at {nextStep.GridPosition}. IsFriendly={IsFriendly(blocker)}");
                        }
                    }
                }
                else if (Time.frameCount % 120 == 0)
                {
                    Debug.LogWarning($"[Courier] {name} cannot find ANY road path to {currentGoal.AssignedTile.GridPosition}. Road might be missing.");
                }
            }
            if (currentGoal == null && Time.frameCount % 120 == 0 && TargetDestination != null)
            {
                Debug.LogWarning($"[Courier] {name} has a target destination but no current movement goal. returning={_isReturning}, origin={(OriginCity != null ? OriginCity.name : "NULL")}");
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
                bool isTarget = (neighbor == target);
                bool hasRoad = neighbor.HasRoad;
                
                // COURIER PATHFINDING: 
                // Ignore all units here so we always pick the shortest physical road path.
                // We also block buildings that aren't the target.
                bool hasBuilding = neighbor.HasBuilding;
                bool isBlockedByStructure = hasBuilding && !isTarget;
                
                bool isPathable = (hasRoad || isTarget) && !isBlockedByStructure;

                if (!cameFrom.ContainsKey(neighbor) && isPathable)
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
        if (tile == null) return;

        // Safety: Only clear the old tile if it actually belongs to THIS unit.
        if (CurrentTile != null && CurrentTile.OccupyingUnit == this)
        {
            CurrentTile.OccupyingUnit = null;
        }

        CurrentTile = tile;
        
        // COURIER EXCEPTION: Only claim the tile if it's actually empty.
        // This allows them to pass through Citizens without "stealing" the selection reference.
        if (tile.OccupyingUnit == null)
        {
            tile.OccupyingUnit = this;
        }

        transform.SetParent(tile.transform.parent);
        transform.localPosition = tile.transform.localPosition + new Vector3(0, 0, -0.1f);
        TileObjectScale.ApplyTo(gameObject);

        ProcessCurrentTileLogistics();
    }

    private void EnsureOriginCity()
    {
        if (OriginCity != null) return;

        City cityOnCurrentTile = CurrentTile?.OccupyingBuilding?.GetComponent<City>();
        if (cityOnCurrentTile != null)
        {
            OriginCity = cityOnCurrentTile;
        }
    }

    private void ProcessCurrentTileLogistics()
    {
        GridTile tile = CurrentTile;
        if (tile == null) return;

        ConstructionProject targetProject = GetTargetConstructionProject();
        if (targetProject != null)
        {
            ProcessConstructionDelivery(tile, targetProject);
            return;
        }

        if (TargetDestination is City destinationCity && destinationCity.AssignedTile == tile)
        {
            Debug.Log($"[Courier] {name} reached city destination at {tile.GridPosition}");
            UnloadAtCity(destinationCity);
            _isReturning = false;
            if (ReferenceEquals(targetDestination, destinationCity))
            {
                targetDestination = null;
            }
            return;
        }

        // 1. Check for Destination Arrival (Facility)
        if (!_isReturning && TargetDestination != null && TargetDestination.AssignedTile == tile)
        {
            Debug.Log($"[Courier] {name} reached destination at {tile.GridPosition}");
            
            Facility facility = tile.OccupyingBuilding?.GetComponent<Facility>();
            if (facility != null)
            {
                float previousHeldProduction = heldProduction;
                ExtractFromFacility(facility);
                _isReturning = heldProduction > previousHeldProduction;
            }
        }
        // 2. Check for Origin Return (City)
        else if (_isReturning && OriginCity != null && OriginCity.AssignedTile == tile)
        {
            Debug.Log($"[Courier] {name} returned to origin city at {tile.GridPosition}");
            UnloadAtCity(OriginCity);
            _isReturning = false; // Ready for next assignment
        }
        // 3. Check for Market (Gather Point)
        else if (tile.OccupyingBuilding != null)
        {
            Market market = tile.OccupyingBuilding.GetComponent<Market>();
            if (market != null && heldProduction > 0f)
            {
                City targetCity = market.OriginCity != null ? market.OriginCity : OriginCity;
                if (targetCity != null)
                {
                    UnloadAtCity(targetCity);
                    if (_isReturning) _isReturning = false;
                    Debug.Log($"[Courier] {name} dropped off resources at Market at {tile.GridPosition}.");
                }
            }

            Facility facility = tile.OccupyingBuilding.GetComponent<Facility>();
            if (facility != null && !_isReturning && (TargetDestination == null || TargetDestination is Facility))
            {
                float previousHeldProduction = heldProduction;
                ExtractFromFacility(facility);
                if (heldProduction > previousHeldProduction) _isReturning = true;
            }
        }
    }

    private void ProcessConstructionDelivery(GridTile tile, ConstructionProject project)
    {
        if (project == null) return;

        EnsureOriginCity();
        if (OriginCity == null)
        {
            if (Time.frameCount % 120 == 0)
            {
                Debug.LogWarning($"[Courier] {name} cannot deliver to construction because OriginCity is null. Put/produce the courier on a city first.");
            }
            return;
        }

        if (!_isReturning && project.AssignedTile == tile)
        {
            GridTile projectTile = project.AssignedTile;
            DepositIntoConstructionProject(project);
            bool projectStillNeedsProduction = project != null && project.RemainingProduction > 0f;
            bool projectCompleted = project == null || project.IsCompleted;
            if (projectCompleted)
            {
                Facility completedFacility = projectTile != null ? projectTile.OccupyingBuilding?.GetComponent<Facility>() : null;
                if (repeatEnabled && completedFacility != null)
                {
                    TargetDestination = completedFacility;
                    Debug.Log($"[Courier] {name} switched into repeat hauling for facility at {projectTile.GridPosition}");
                }
                else
                {
                    TargetDestination = null;
                }
            }
            else if (!projectStillNeedsProduction)
            {
                // Construction has enough stored production and now waits on a citizen.
                // Clear the work tile so the citizen can step onto it.
                _isReturning = OriginCity != null;
                TargetDestination = OriginCity;
            }
            else
            {
                _isReturning = true;
            }
            return;
        }

        if (_isReturning && OriginCity.AssignedTile == tile)
        {
            LoadFromCityForConstruction(OriginCity, project);
            _isReturning = heldProduction <= 0f;
            return;
        }

        if (!_isReturning && heldProduction <= 0f && OriginCity.AssignedTile == tile)
        {
            LoadFromCityForConstruction(OriginCity, project);
            _isReturning = heldProduction <= 0f;
        }
    }

    private ConstructionProject GetTargetConstructionProject()
    {
        ConstructionProject project = TargetDestination as ConstructionProject;
        if (project != null) return project;

        GridTile destinationTile = TargetDestination?.AssignedTile;
        return destinationTile != null
            ? destinationTile.OccupyingBuilding?.GetComponent<ConstructionProject>()
            : null;
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

    private void LoadFromCityForConstruction(City city, ConstructionProject project)
    {
        if (city == null || project == null) return;

        float spaceLeft = maxProductionCapacity - heldProduction;
        float needed = project.RemainingProduction;
        float available = city.ProductionStockpile;
        float toTake = Mathf.Min(spaceLeft, needed, available);

        if (toTake > 0f)
        {
            city.DeductProduction(toTake);
            heldProduction += toTake;
            Debug.Log($"[Courier] {name} loaded {toTake:F1} production from {city.name} for construction. Held: {heldProduction:F1}/{maxProductionCapacity}");
        }
    }

    private void DepositIntoConstructionProject(ConstructionProject project)
    {
        if (project == null || heldProduction <= 0f) return;

        float toDeposit = Mathf.Min(heldProduction, project.RemainingProduction);
        if (toDeposit <= 0f) return;

        heldProduction -= toDeposit;
        project.AddProduction(toDeposit);
        Debug.Log($"[Courier] {name} delivered {toDeposit:F1} production to construction project.");
    }
}
