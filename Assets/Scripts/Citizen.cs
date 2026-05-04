using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A non-combatant unit used for labor and expansion.
/// </summary>
public class Citizen : Unit
{
    private struct ConstructionTask
    {
        public GridTile tile;
        public GameObject prefab;
    }

    private Queue<ConstructionTask> _taskQueue = new Queue<ConstructionTask>();
    private Coroutine _constructionCoroutine;

    public GridTile TargetBuildingTile => _taskQueue.Count > 0 ? _taskQueue.Peek().tile : null;
    public GameObject TargetBuildingPrefab => _taskQueue.Count > 0 ? _taskQueue.Peek().prefab : null;

    protected override void Awake()
    {
        // Citizens are non-military civilian units
        isMilitary = false;
        strength = 0;
        base.Awake();
    }

    public void StartConstruction(GridTile tile, GameObject prefab)
    {
        _taskQueue.Enqueue(new ConstructionTask { tile = tile, prefab = prefab });
        
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
                    FinishConstruction(); // This now pops from queue
                    break; 
                }

                GridTile nextStep = GetNextStepTowards(CurrentTile, targetTile);
                if (nextStep != null)
                {
                    float cost = GetMovementCost(CurrentTile, nextStep);
                    
                    while (currentMovementPoints < cost)
                    {
                        yield return new WaitForSeconds(0.5f);
                        ResetMovement();
                    }

                    if (MoveTo(nextStep))
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
        
        if (task.prefab != null)
        {
            GameObject buildingGO = Instantiate(task.prefab, task.tile.transform.position + Vector3.back * 0.2f, Quaternion.identity);
            task.tile.OccupyingBuilding = buildingGO;
            
            Facility facility = buildingGO.GetComponent<Facility>();
            if (facility != null) facility.AssignedTile = task.tile;

            Debug.Log($"{name} finished building {task.prefab.name} at {task.tile.GridPosition}");
        }
        else
        {
            // If no prefab, it's a road
            // Manually trigger the road logic on the tile
            task.tile.AddRoad();
            Debug.Log($"{name} finished building ROAD at {task.tile.GridPosition}");
        }
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
                if (neighbor != null && neighbor.IsEmpty && !cameFrom.ContainsKey(neighbor))
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
}
