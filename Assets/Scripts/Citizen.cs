using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A non-combatant unit used for labor and expansion.
/// </summary>
public class Citizen : Unit
{
    [SerializeField] private City homeCity;

    private struct ConstructionTask
    {
        public GridTile tile;
        public GameObject constructionPrefab;
        public GameObject targetPrefab;
        public ConstructionProject existingProject;
    }

    private Queue<ConstructionTask> _taskQueue = new Queue<ConstructionTask>();
    private Coroutine _constructionCoroutine;
    private ConstructionProject _activeConstructionProject;

    public City HomeCity => homeCity;
    public bool IsBusy => _taskQueue.Count > 0 || _constructionCoroutine != null || (_activeConstructionProject != null && _activeConstructionProject.NeedsCitizenWork);
    public GridTile TargetBuildingTile => _taskQueue.Count > 0 ? _taskQueue.Peek().tile : null;
    public GameObject TargetBuildingPrefab => _taskQueue.Count > 0 ? _taskQueue.Peek().targetPrefab : null;

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

        _taskQueue.Enqueue(new ConstructionTask { tile = tile, constructionPrefab = constructionPrefab, targetPrefab = targetPrefab, existingProject = null });
        
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
            existingProject = project
        });
        
        if (_constructionCoroutine == null)
        {
            _constructionCoroutine = StartCoroutine(ConstructionRoutine());
        }
    }

    private IEnumerator ConstructionRoutine()
    {
        while (_taskQueue.Count > 0)
        {
            ConstructionTask currentTask = _taskQueue.Peek();
            GridTile targetTile = currentTask.tile;

            Debug.Log($"[Citizen] {name} moving to task at {targetTile.GridPosition}. Tasks left: {_taskQueue.Count}");
            
            while (targetTile != null)
            {
                if (CurrentTile == targetTile)
                {
                    FinishConstruction();
                    if (_activeConstructionProject != null)
                    {
                        while (_activeConstructionProject != null && _activeConstructionProject.NeedsCitizenWork)
                        {
                            yield return null;
                        }

                        _activeConstructionProject?.ReleaseCitizen(this);
                        _activeConstructionProject = null;
                    }
                    break; 
                }

                GridTile nextStep = GetNextStepTowards(CurrentTile, targetTile);
                if (nextStep != null)
                {
                    if (nextStep == targetTile && targetTile.OccupyingUnit != null && targetTile.OccupyingUnit != this && IsFriendly(targetTile.OccupyingUnit))
                    {
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }

                    float cost = GetMovementCost(CurrentTile, nextStep);
                    
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
                        yield return new WaitForSeconds(1.0f);
                    }
                }
                else
                {
                    if (targetTile.OccupyingUnit != null && targetTile.OccupyingUnit != this && IsFriendly(targetTile.OccupyingUnit))
                    {
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }

                    Debug.LogError($"[Citizen] {name} NO PATH FOUND to {targetTile.GridPosition}. Skipping task.");
                    _taskQueue.Dequeue();
                    break;
                }
            }
        }
        _constructionCoroutine = null;
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
        else
        {
            // If no prefab, it's a road
            // Manually trigger the road logic on the tile
            task.tile.AddRoad();
            Debug.Log($"{name} finished building ROAD at {task.tile.GridPosition}");
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
                bool isTarget = neighbor == target;
                bool canTraverse = neighbor != null && (neighbor.IsEmpty || isTarget);

                if (canTraverse && !cameFrom.ContainsKey(neighbor))
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
        if (targetTile.OccupyingUnit != null && targetTile.OccupyingUnit != this) return false;

        currentMovementPoints -= cost;
        PlaceOnTile(targetTile);
        return true;
    }
}
