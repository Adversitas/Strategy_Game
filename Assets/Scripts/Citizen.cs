using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A non-combatant unit used for labor and expansion.
/// </summary>
public class Citizen : Unit
{
    public enum ConstructionState
    {
        None,
        FetchingProduction,
        DeliveringProduction,
        WorkingConstruction
    }

    [SerializeField] private City homeCity;

    [Header("Citizen Carrying Capacity")]
    [SerializeField] private float carriedProduction = 0f;
    [SerializeField] private float maxCarryingCapacity = 10f;

    private struct ConstructionTask
    {
        public GridTile tile;
        public GameObject constructionPrefab;
        public GameObject targetPrefab;
        public ConstructionProject existingProject;
        public bool isRoad;
        public float roadCost;
    }

    private Queue<ConstructionTask> _taskQueue = new Queue<ConstructionTask>();
    private Coroutine _constructionCoroutine;
    private ConstructionProject _activeConstructionProject;
    private ConstructionState _constructionState = ConstructionState.None;
    private City _productionSourceCity;

    private const float RoadProductionCost = 10f;

    public float CarriedProduction => carriedProduction;
    public float MaxCarryingCapacity => maxCarryingCapacity;
    public City HomeCity => homeCity;
    public bool IsBusy => _taskQueue.Count > 0 || _constructionCoroutine != null || (_activeConstructionProject != null && _activeConstructionProject.NeedsCitizenWork);
    public GridTile TargetBuildingTile => _taskQueue.Count > 0 ? _taskQueue.Peek().tile : null;
    public GameObject TargetBuildingPrefab => _taskQueue.Count > 0 ? _taskQueue.Peek().targetPrefab : null;
    public ConstructionState CurrentConstructionState => _constructionState;

    protected override void Awake()
    {
        // Citizens are non-military civilian units
        isMilitary = false;
        strength = 0;
        autoMove = false;
        base.Awake();
    }

    public void SetHomeCity(City city)
    {
        homeCity = city;
    }

    public void StartConstruction(GridTile tile, GameObject constructionPrefab, GameObject targetPrefab = null)
    {
        if (tile == null) return;

        bool isRoad = constructionPrefab == null && targetPrefab == null;
        float roadCost = isRoad ? RoadProductionCost : 0f;

        _taskQueue.Enqueue(new ConstructionTask 
        { 
            tile = tile, 
            constructionPrefab = constructionPrefab, 
            targetPrefab = targetPrefab, 
            existingProject = null,
            isRoad = isRoad,
            roadCost = roadCost
        });
        
        if (_constructionCoroutine == null)
        {
            _constructionCoroutine = StartCoroutine(ConstructionRoutine());
        }
    }

    public void StartConstructionWork(ConstructionProject project)
    {
        if (project == null || project.AssignedTile == null || !project.NeedsCitizenWork || !project.TryAssignCitizen(this)) return;

        _taskQueue.Enqueue(new ConstructionTask
        {
            tile = project.AssignedTile,
            constructionPrefab = null,
            targetPrefab = null,
            existingProject = project,
            isRoad = false,
            roadCost = 0f
        });
        
        if (_constructionCoroutine == null)
        {
            _constructionCoroutine = StartCoroutine(ConstructionRoutine());
        }
    }

    private IEnumerator ConstructionRoutine()
    {
        Debug.Log($"[Citizen] {name} ConstructionRoutine started. Initial tasks: {_taskQueue.Count}");
        while (_taskQueue.Count > 0 || _constructionState != ConstructionState.None)
        {
            // Handle state-based production fetching for roads
            if (_constructionState == ConstructionState.FetchingProduction && _productionSourceCity != null)
            {
                // Move to city to get production
                GridTile cityTile = _productionSourceCity.AssignedTile;
                if (cityTile != null && CurrentTile != cityTile)
                {
                    GridTile nextStep = GetNextStepTowards(CurrentTile, cityTile);
                    if (nextStep != null)
                    {
                        // If the next step is occupied by another unit (including couriers), try to resolve it.
                        Unit other = nextStep.OccupyingUnit != null ? nextStep.OccupyingUnit : (Unit)nextStep.OccupyingCourier;
                        if (other != null && other != this)
                        {
                            if (IsFriendly(other) && other is Courier)
                            {
                                // Attempt to swap/move onto the courier tile (courier swap handled in MoveTo)
                                float swapCost = GetMovementCost(CurrentTile, nextStep);
                                if (currentMovementPoints < swapCost)
                                {
                                    Debug.Log($"[Citizen] {name} waiting for movement to swap with courier {other.name}. Have {currentMovementPoints}, need {swapCost:F2}");
                                }
                                while (currentMovementPoints < swapCost)
                                {
                                    yield return null;
                                }

                                if (MoveTo(nextStep))
                                {
                                    Debug.Log($"[Citizen] {name} swapped/moved onto courier {other.name} at {nextStep.GridPosition}");
                                    yield return new WaitForSeconds(0.4f);
                                }
                                else
                                {
                                    Debug.LogWarning($"[Citizen] {name} failed to swap with courier {other.name} at {nextStep.GridPosition}");
                                    yield return new WaitForSeconds(0.5f);
                                }
                                continue;
                            }

                            if (IsFriendly(other))
                            {
                                Debug.Log($"[Citizen] {name} waiting for friendly unit {other.name} to clear {nextStep.GridPosition}");
                                yield return new WaitForSeconds(0.5f);
                                continue;
                            }

                            Debug.LogWarning($"[Citizen] {name} blocked by non-friendly unit {other.name} at {nextStep.GridPosition}");
                            yield return new WaitForSeconds(0.5f);
                            continue;
                        }

                        float cost = GetMovementCost(CurrentTile, nextStep);
                        if (currentMovementPoints < cost)
                        {
                            Debug.Log($"[Citizen] {name} waiting for movement to city { _productionSourceCity.name }. Have {currentMovementPoints}, needs {cost:F2}");
                        }
                        while (currentMovementPoints < cost)
                        {
                            yield return null;
                        }
                        if (MoveTo(nextStep))
                        {
                            yield return new WaitForSeconds(0.4f);
                        }
                        else
                        {
                            Debug.LogWarning($"[Citizen] {name} failed to MoveTo nextStep towards city {_productionSourceCity.name} at {nextStep.GridPosition}");
                            yield return new WaitForSeconds(0.5f);
                        }
                    }
                    else
                    {
                        Debug.LogError($"[Citizen] {name} cannot find path to city {_productionSourceCity.name}. Clearing production source and skipping task.");
                        _constructionState = ConstructionState.None;
                        _productionSourceCity = null;
                        // Skip this task - no path to city
                        if (_taskQueue.Count > 0) _taskQueue.Dequeue();
                        continue;
                    }
                }
                else if (CurrentTile == cityTile)
                {
                    // At city, load production
                    float neededProduction = Mathf.Min(maxCarryingCapacity, _productionSourceCity.ProductionStockpile);
                    Debug.Log($"[Citizen] {name} at city {_productionSourceCity.name}: stockpile {_productionSourceCity.ProductionStockpile:F1}, neededProduction {neededProduction:F1}, carrying {carriedProduction:F1}");
                    if (neededProduction > 0f)
                    {
                        float spaceAvailable = maxCarryingCapacity - carriedProduction;
                        float toLoad = Mathf.Min(neededProduction, spaceAvailable, RoadProductionCost - carriedProduction);
                        if (toLoad > 0f)
                        {
                            _productionSourceCity.DeductProduction(toLoad);
                            carriedProduction += toLoad;
                            Debug.Log($"[Citizen] {name} loaded {toLoad:F1} production at {_productionSourceCity.name}. Carrying: {carriedProduction}/{maxCarryingCapacity}");
                        }
                        else
                        {
                            Debug.Log($"[Citizen] {name} nothing to load (toLoad==0). spaceAvailable={spaceAvailable:F1}, RoadCost={RoadProductionCost:F1}");
                        }
                    }
                    
                    // Check if we have enough or can't get more
                    if (carriedProduction >= RoadProductionCost)
                    {
                        Debug.Log($"[Citizen] {name} has enough production ({carriedProduction:F1}) to start delivering.");
                        _constructionState = ConstructionState.DeliveringProduction;
                    }
                    else if (_productionSourceCity.ProductionStockpile <= 0f && carriedProduction < RoadProductionCost)
                    {
                        // City has no production, first road is free
                        carriedProduction = RoadProductionCost;
                        Debug.Log($"[Citizen] {name} forced free road (city empty). Setting carriedProduction to {carriedProduction}");
                        _constructionState = ConstructionState.DeliveringProduction;
                    }
                    else
                    {
                        // Wait for more production to accumulate
                        Debug.Log($"[Citizen] {name} waiting for city production to accumulate.");
                        yield return new WaitForSeconds(2f);
                    }
                }
                else
                {
                    // No city tile found
                    Debug.LogWarning($"[Citizen] {name} city {_productionSourceCity.name} has no assigned tile. Clearing and skipping.");
                    _constructionState = ConstructionState.None;
                    _productionSourceCity = null;
                    if (_taskQueue.Count > 0) _taskQueue.Dequeue();
                }
            }
            else if (_constructionState == ConstructionState.FetchingProduction)
            {
                // No city to fetch from - build road for free
                Debug.Log($"[Citizen] {name} building road for free (no city source)");
                carriedProduction = RoadProductionCost; // Give them the production
                _constructionState = ConstructionState.DeliveringProduction;
            }
            else if (_constructionState == ConstructionState.DeliveringProduction)
            {
                // We have enough production, now deliver to road tile
                if (_taskQueue.Count == 0)
                {
                    _constructionState = ConstructionState.None;
                    continue;
                }
                
                ConstructionTask currentTask = _taskQueue.Peek();
                GridTile targetTile = currentTask.tile;
                
                if (CurrentTile != targetTile)
                {
                    GridTile nextStep = GetNextStepTowards(CurrentTile, targetTile);
                    if (nextStep != null)
                    {
                        // If next step is occupied (including couriers), try to resolve by swapping with a friendly Courier
                        Unit other = nextStep.OccupyingUnit != null ? nextStep.OccupyingUnit : (Unit)nextStep.OccupyingCourier;
                        if (other != null && other != this)
                        {
                            if (IsFriendly(other) && other is Courier)
                            {
                                float swapCost = GetMovementCost(CurrentTile, nextStep);
                                if (currentMovementPoints < swapCost)
                                {
                                    Debug.Log($"[Citizen] {name} waiting for movement to swap with courier {other.name}. Have {currentMovementPoints:F2}, need {swapCost:F2}");
                                }
                                while (currentMovementPoints < swapCost)
                                {
                                    yield return null;
                                }

                                if (MoveTo(nextStep))
                                {
                                    Debug.Log($"[Citizen] {name} swapped/moved onto courier {other.name} at {nextStep.GridPosition} before delivering");
                                    yield return new WaitForSeconds(0.4f);
                                }
                                else
                                {
                                    Debug.LogWarning($"[Citizen] {name} failed to swap with courier {other.name} at {nextStep.GridPosition} when delivering");
                                    yield return new WaitForSeconds(0.5f);
                                }
                                continue;
                            }

                            if (IsFriendly(other))
                            {
                                Debug.Log($"[Citizen] {name} waiting for friendly unit {other.name} at {nextStep.GridPosition} before delivering.");
                                yield return new WaitForSeconds(0.5f);
                                continue;
                            }

                            Debug.LogWarning($"[Citizen] {name} blocked by non-friendly unit {other.name} at {nextStep.GridPosition} when delivering.");
                            yield return new WaitForSeconds(0.5f);
                            continue;
                        }

                        float cost = GetMovementCost(CurrentTile, nextStep);
                        if (currentMovementPoints < cost)
                        {
                            Debug.Log($"[Citizen] {name} waiting for movement to deliver. Have {currentMovementPoints:F2}, need {cost:F2}");
                        }
                        while (currentMovementPoints < cost)
                        {
                            yield return null;
                        }
                        if (MoveTo(nextStep))
                        {
                            Debug.Log($"[Citizen] {name} moved to {nextStep.GridPosition} delivering to {targetTile.GridPosition}");
                            yield return new WaitForSeconds(0.4f);
                        }
                        else
                        {
                            Debug.LogWarning($"[Citizen] {name} failed to MoveTo nextStep while delivering to {targetTile.GridPosition}");
                            yield return new WaitForSeconds(0.5f);
                        }
                    }
                    else
                    {
                        Debug.LogError($"[Citizen] {name} cannot find path to road tile at {targetTile.GridPosition}. Aborting delivery.");
                        _constructionState = ConstructionState.None;
                        _taskQueue.Dequeue();
                        continue;
                    }
                }
                else
                {
                    // At road tile, deliver production
                    FinishRoadConstruction();
                    _constructionState = ConstructionState.None;
                }
            }
            else if (_constructionState == ConstructionState.WorkingConstruction)
            {
                // Working on a ConstructionProject
                if (_activeConstructionProject != null)
                {
                    while (_activeConstructionProject != null && _activeConstructionProject.NeedsCitizenWork)
                    {
                        yield return null;
                    }
                    _activeConstructionProject?.ReleaseCitizen(this);
                    _activeConstructionProject = null;
                }
                _constructionState = ConstructionState.None;
            }
            else if (_taskQueue.Count > 0)
            {
                // Normal task processing
                ConstructionTask currentTask = _taskQueue.Peek();
                GridTile targetTile = currentTask.tile;

                // Check if this is a road that needs production fetching
                if (currentTask.isRoad && carriedProduction < currentTask.roadCost)
                {
                    // Need to go fetch production first
                    if (_productionSourceCity == null)
                    {
                        _productionSourceCity = FindHomeCity();
                    }
                    
                    if (_productionSourceCity != null)
                    {
                        _constructionState = ConstructionState.FetchingProduction;
                        continue;
                    }
                    else
                    {
                        // No city, road is free - set state and let next iteration handle it
                        _constructionState = ConstructionState.FetchingProduction;
                        continue;
                    }
                }

                Debug.Log($"[Citizen] {name} moving to task at {targetTile.GridPosition}. Tasks left: {_taskQueue.Count}, Carrying: {carriedProduction}/{maxCarryingCapacity}");
                
                if (CurrentTile == targetTile)
                {
                    FinishConstruction();
                    if (currentTask.existingProject != null)
                    {
                        _constructionState = ConstructionState.WorkingConstruction;
                    }
                }
                else
                {
                    GridTile nextStep = GetNextStepTowards(CurrentTile, targetTile);
                    if (nextStep != null)
                    {
                        Debug.Log($"[Citizen] {name} pathfinding next step {nextStep.GridPosition} towards {targetTile.GridPosition}");
                        // If the next step (or the target tile) is occupied (including couriers), attempt to resolve by swapping with a friendly Courier
                        Unit other = nextStep.OccupyingUnit != null ? nextStep.OccupyingUnit : (Unit)nextStep.OccupyingCourier;
                        if (other != null && other != this)
                        {
                            if (IsFriendly(other) && other is Courier)
                            {
                                float swapCost = GetMovementCost(CurrentTile, nextStep);
                                if (currentMovementPoints < swapCost)
                                {
                                    Debug.Log($"[Citizen] {name} waiting for movement to swap with courier {other.name}. Have {currentMovementPoints:F2}, need {swapCost:F2}");
                                }
                                while (currentMovementPoints < swapCost)
                                {
                                    yield return null;
                                }

                                if (MoveTo(nextStep))
                                {
                                    Debug.Log($"[Citizen] {name} swapped/moved onto courier {other.name} at {nextStep.GridPosition}");
                                    yield return new WaitForSeconds(0.4f);
                                }
                                else
                                {
                                    Debug.LogWarning($"[Citizen] {name} failed to swap with courier {other.name} at {nextStep.GridPosition}");
                                    yield return new WaitForSeconds(0.5f);
                                }
                                continue;
                            }

                            if (IsFriendly(other))
                            {
                                yield return new WaitForSeconds(0.5f);
                                continue;
                            }

                            yield return new WaitForSeconds(0.5f);
                            continue;
                        }

                        float cost = GetMovementCost(CurrentTile, nextStep);
                        if (currentMovementPoints < cost)
                        {
                            Debug.Log($"[Citizen] {name} waiting for movement. Have {currentMovementPoints:F2}, need {cost:F2}");
                        }
                        while (currentMovementPoints < cost)
                        {
                            yield return null;
                        }

                        if (MoveTo(nextStep))
                        {
                            yield return new WaitForSeconds(0.4f);
                        }
                        else if (TryEnterConstructionProjectTile(nextStep, targetTile, cost))
                        {
                            yield return new WaitForSeconds(0.4f);
                        }
                        else
                        {
                            Debug.LogWarning($"[Citizen] {name} could not move to nextStep {nextStep.GridPosition} or enter project tile. Retrying later.");
                            yield return new WaitForSeconds(1.0f);
                        }
                    }
                    else
                    {
                        Unit blockingAtTarget = targetTile.OccupyingUnit != null ? targetTile.OccupyingUnit : (Unit)targetTile.OccupyingCourier;
                        if (blockingAtTarget != null && blockingAtTarget != this && IsFriendly(blockingAtTarget))
                        {
                            yield return new WaitForSeconds(0.5f);
                            continue;
                        }

                        Debug.LogError($"[Citizen] {name} NO PATH FOUND to {targetTile.GridPosition}. Skipping task.");
                        _taskQueue.Dequeue();
                    }
                }
            }
            else
            {
                // No tasks and no active state - exit
                _constructionState = ConstructionState.None;
            }
        }
        Debug.Log($"[Citizen] {name} ConstructionRoutine finished.");
        _constructionCoroutine = null;
    }

    private City FindHomeCity()
    {
        if (homeCity != null && homeCity.AssignedTile != null) return homeCity;
        
        if (AssetRegistry.Instance == null) return null;

        City closestCity = null;
        int bestDistance = int.MaxValue;

        foreach (City city in AssetRegistry.Instance.Cities)
        {
            if (city == null || city.AssignedTile == null) continue;
            
            // Check ownership
            PlayerOwnership myOwnership = GetComponent<PlayerOwnership>();
            PlayerOwnership cityOwnership = city.GetComponent<PlayerOwnership>();
            if (myOwnership != null && cityOwnership != null && myOwnership.OwnerId != cityOwnership.OwnerId) continue;

            int distance = CurrentTile != null ? GetHexDistance(CurrentTile.GridPosition, city.AssignedTile.GridPosition) : int.MaxValue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closestCity = city;
            }
        }

        return closestCity;
    }

    private void FinishRoadConstruction()
    {
        if (_taskQueue.Count == 0) return;
        ConstructionTask task = _taskQueue.Dequeue();
        
        carriedProduction -= RoadProductionCost;
        task.tile.AddRoad();
        Debug.Log($"{name} finished building ROAD at {task.tile.GridPosition} (spent {RoadProductionCost}, remaining {carriedProduction:F1})");
    }

    private void FinishConstruction()
    {
        if (_taskQueue.Count == 0) return;
        ConstructionTask task = _taskQueue.Dequeue();

        if (task.existingProject != null)
        {
            _activeConstructionProject = task.existingProject;
            return;
        }
        
        if (task.isRoad)
        {
            // Road construction is handled by FinishRoadConstruction
            // This shouldn't happen normally, but just in case
            task.tile.AddRoad();
            Debug.Log($"{name} finished building ROAD at {task.tile.GridPosition}");
            return;
        }
        
        if (task.constructionPrefab != null)
        {
            GameObject buildingGO = Instantiate(task.constructionPrefab, GetTileCenter(task.tile) + Vector3.back * 0.2f, Quaternion.identity);
            TileObjectScale.ApplyTo(buildingGO);
            PlayerOwnership.Inherit(buildingGO, this);
            task.tile.OccupyingBuilding = buildingGO;

            ConstructionProject cp = buildingGO.GetComponent<ConstructionProject>();
            if (cp == null && task.constructionPrefab.name.Contains("ConstructionProject"))
            {
                cp = buildingGO.AddComponent<ConstructionProject>();
            }
            if (cp != null) 
            {
                // Remove City script if it was accidentally copied over to the ConstructionProject prefab
                City accidentalCity = buildingGO.GetComponent<City>();
                if (accidentalCity != null) Destroy(accidentalCity);

                cp.AssignedTile = task.tile;
                cp.ResultPrefab = task.targetPrefab;
                
                // Set the required resources based on the target prefab
                float requiredCost = (task.targetPrefab != null && task.targetPrefab.name.Contains("City")) ? 150f : 50f;
                cp.SetRequiredProduction(requiredCost);
                _activeConstructionProject = cp.NeedsCitizenWork ? cp : null;
                Debug.Log($"[Citizen] ConstructionProject setup: object={buildingGO.name}, tile={cp.AssignedTile?.GridPosition.ToString() ?? "NULL"}, registryCount={(AssetRegistry.Instance != null ? AssetRegistry.Instance.ConstructionProjects.Count.ToString() : "NO REGISTRY")}");
            }

            Debug.Log($"{name} finished placing {task.constructionPrefab.name} at {task.tile.GridPosition}");
        }
    }

    private static Vector3 GetTileCenter(GridTile tile)
    {
        if (tile == null) return Vector3.zero;

        SpriteRenderer tileRenderer = tile.GetComponent<SpriteRenderer>();
        if (tileRenderer != null) return tileRenderer.bounds.center;

        return tile.transform.position;
    }

    private GridTile GetNextStepTowards(GridTile start, GridTile target)
    {
        if (start == null || target == null) return null;

        Queue<GridTile> queue = new Queue<GridTile>();
        Dictionary<GridTile, GridTile> cameFrom = new Dictionary<GridTile, GridTile>();
        
        queue.Enqueue(start);
        cameFrom[start] = null;

        while (queue.Count > 0)
        {
            GridTile current = queue.Dequeue();

            if (current == target)
            {
                GridTile step = target;
                while (cameFrom[step] != null && cameFrom[step] != start)
                    step = cameFrom[step];
                return step;
            }

            // Pathfinding: Use centralized neighbor logic
            foreach (var neighbor in SquareGridGenerator.Instance.GetNeighbors(current))
            {
                if (neighbor != null && !cameFrom.ContainsKey(neighbor) && TraversalRules.CanTraverseTile(gameObject, neighbor, target))
                {
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }
        return null;
    }

    public void BuildRoad()
    {
        if (CurrentTile != null && !CurrentTile.HasRoad)
        {
            CurrentTile.AddRoad();
            Debug.Log($"{name} built a road at {CurrentTile.GridPosition}");
        }
    }

    private bool TryEnterConstructionProjectTile(GridTile nextStep, GridTile targetTile, float cost)
    {
        if (nextStep == null || targetTile == null || nextStep != targetTile) return false;
        if (currentMovementPoints < cost) return false;

        ConstructionProject project = targetTile.OccupyingBuilding != null
            ? targetTile.OccupyingBuilding.GetComponent<ConstructionProject>()
            : null;

        if (project == null) return false;
        Unit blockingAtTarget = targetTile.OccupyingUnit != null ? targetTile.OccupyingUnit : (Unit)targetTile.OccupyingCourier;
        if (blockingAtTarget != null && blockingAtTarget != this) return false;

        currentMovementPoints -= cost;
        PlaceOnTile(targetTile);
        return true;
    }
}
