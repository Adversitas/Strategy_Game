using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles city-specific logic, including resource stockpiling.
/// </summary>
public class City : MonoBehaviour, IGridTarget
{
    [System.Serializable]
    public class BuildQueueEntry
    {
        public GridTile targetTile;
        public GameObject constructionPrefab;
        public GameObject resultPrefab;

        public bool IsRoad => constructionPrefab == null && resultPrefab == null;

        public string GetDisplayName()
        {
            if (IsRoad) return "ROAD";
            if (resultPrefab != null) return resultPrefab.name.ToUpper();
            if (constructionPrefab != null) return constructionPrefab.name.ToUpper();
            return "UNKNOWN";
        }
    }

    [System.Serializable]
    public class UnitProductionQueueEntry
    {
        public GameObject unitPrefab;
        public string unitName;
        public float totalCost;
        public float spentProduction;
        public bool usesProduction = true;

        public float RemainingCost => Mathf.Max(0f, totalCost - spentProduction);
        public bool IsComplete => RemainingCost <= 0.001f;
    }

    [Header("Production Resources")]
    [SerializeField] private float productionStockpile = 0;
    [SerializeField] private int maxProductionStockpile = 500;
    [SerializeField] private int productionPerTurn = 5;
    [SerializeField] private float productionSpendThresholdPerTurn = 10f;

    [Header("Food Resources")]
    [SerializeField] private float foodStockpile = 0;
    [SerializeField] private int maxFoodStockpile = 500;
    [SerializeField] private int foodPerTurn = 5;

    [Header("Population")]
    [SerializeField] private int citizenCapacity = 1;
    [SerializeField] private int currentCitizens = 0;
    [SerializeField] private float foodToNextCitizen = 50f;

    [Header("Timing")]
    [SerializeField] private float turnDuration = 5f;

    private float _timer;
    private readonly Queue<BuildQueueEntry> _buildQueue = new Queue<BuildQueueEntry>();
    private readonly Queue<UnitProductionQueueEntry> _unitProductionQueue = new Queue<UnitProductionQueueEntry>();

    [SerializeField] private GridTile assignedTile;
    public GridTile AssignedTile 
    { 
        get => assignedTile; 
        set => assignedTile = value; 
    }

    public float ProductionStockpile => productionStockpile;
    public int MaxProductionStockpile => maxProductionStockpile;
    public float ProductionSpendThresholdPerTurn => productionSpendThresholdPerTurn;
    public float FoodStockpile => foodStockpile;
    public int MaxFoodStockpile => maxFoodStockpile;
    public float FoodToNextCitizen => foodToNextCitizen;
    public int CitizenCapacity => citizenCapacity;
    public int CurrentCitizens => currentCitizens;
    public int QueuedBuildCount => _buildQueue.Count;
    public IReadOnlyCollection<BuildQueueEntry> BuildQueue => _buildQueue;
    public int QueuedUnitCount => _unitProductionQueue.Count;
    public IReadOnlyCollection<UnitProductionQueueEntry> UnitProductionQueue => _unitProductionQueue;

    private void Start()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.RegisterCity(this);
    }

    private void OnDestroy()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.UnregisterCity(this);
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        AssignQueuedBuilds();

        if (_timer >= turnDuration)
        {
            _timer -= turnDuration;
            GenerateResources();
            ProcessUnitProductionQueue();
        }
    }

    private void GenerateResources()
    {
        productionStockpile = Mathf.Min(productionStockpile + productionPerTurn, maxProductionStockpile);
        
        // Food grows and unlocks citizen capacity
        foodStockpile += foodPerTurn;
        if (foodStockpile >= foodToNextCitizen)
        {
            foodStockpile -= foodToNextCitizen;
            citizenCapacity++;
            Debug.Log($"City {name} expanded! New citizen capacity: {citizenCapacity}");
        }
    }

    public bool CanAffordProduction(float amount)
    {
        return productionStockpile >= amount;
    }

    public bool CanAffordFood(float amount)
    {
        return foodStockpile >= amount;
    }

    public void DeductProduction(float amount)
    {
        productionStockpile = Mathf.Max(0, productionStockpile - amount);
    }

    public void AddProduction(float amount)
    {
        productionStockpile = Mathf.Min(maxProductionStockpile, productionStockpile + amount);
    }

    public void DeductFood(float amount)
    {
        foodStockpile = Mathf.Max(0, foodStockpile - amount);
    }

    public bool HasCitizenCapacity()
    {
        return currentCitizens < citizenCapacity;
    }

    public void RegisterCitizen()
    {
        currentCitizens++;
    }

    public void UnregisterCitizen()
    {
        currentCitizens = Mathf.Max(0, currentCitizens - 1);
    }

    public void QueueConstruction(GridTile tile, GameObject constructionPrefab, GameObject resultPrefab = null)
    {
        if (tile == null) return;

        BuildQueueEntry job = new BuildQueueEntry
        {
            targetTile = tile,
            constructionPrefab = constructionPrefab,
            resultPrefab = resultPrefab
        };

        _buildQueue.Enqueue(job);

        Debug.Log($"[City] {name} queued {job.GetDisplayName()} at {tile.GridPosition}. Queue size: {_buildQueue.Count}");
    }

    public void QueueRoad(GridTile tile)
    {
        if (tile == null) return;

        tile.IsPendingRoad = true;
        QueueConstruction(tile, null, null);
    }

    public void QueueUnitProduction(GameObject prefab, float totalCost, string unitName, bool usesProduction = true)
    {
        if (prefab == null || totalCost <= 0f) return;

        _unitProductionQueue.Enqueue(new UnitProductionQueueEntry
        {
            unitPrefab = prefab,
            totalCost = totalCost,
            unitName = unitName,
            usesProduction = usesProduction,
            spentProduction = 0f
        });

        Debug.Log($"[City] {name} queued unit {unitName} for cost {totalCost}. Unit queue size: {_unitProductionQueue.Count}");
    }

    private void AssignQueuedBuilds()
    {
        if (AssetRegistry.Instance == null) return;

        while (true)
        {
            Citizen availableCitizen = FindClosestIdleCitizen();
            if (availableCitizen == null) return;

            ConstructionProject priorityProject = FindPriorityConstructionProject(availableCitizen);
            if (priorityProject == null) break;

            availableCitizen.StartConstructionWork(priorityProject);
            Debug.Log($"[City] {name} reassigned {availableCitizen.name} to stocked construction project at {priorityProject.AssignedTile.GridPosition}");
        }

        if (_buildQueue.Count == 0) return;

        while (_buildQueue.Count > 0)
        {
            Citizen availableCitizen = FindClosestIdleCitizen();
            if (availableCitizen == null) return;

            BuildQueueEntry nextJob = _buildQueue.Peek();
            if (!IsBuildTargetStillValid(nextJob))
            {
                if (nextJob.IsRoad && nextJob.targetTile != null)
                {
                    nextJob.targetTile.IsPendingRoad = false;
                }

                _buildQueue.Dequeue();
                continue;
            }

            _buildQueue.Dequeue();
            availableCitizen.StartConstruction(nextJob.targetTile, nextJob.constructionPrefab, nextJob.resultPrefab);
            Debug.Log($"[City] {name} assigned {nextJob.GetDisplayName()} at {nextJob.targetTile.GridPosition} to {availableCitizen.name}");
        }
    }

    private Citizen FindClosestIdleCitizen()
    {
        if (assignedTile == null || AssetRegistry.Instance == null) return null;

        Citizen bestCitizen = null;
        int bestDistance = int.MaxValue;

        foreach (Citizen citizen in AssetRegistry.Instance.Units.OfType<Citizen>())
        {
            if (citizen == null || citizen.IsBusy || citizen.CurrentTile == null) continue;
            if (citizen.HomeCity == null && citizen.CurrentTile == assignedTile)
            {
                citizen.SetHomeCity(this);
            }
            if (citizen.HomeCity != this) continue;

            int distance = GetHexDistance(assignedTile.GridPosition, citizen.CurrentTile.GridPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCitizen = citizen;
            }
        }

        return bestCitizen;
    }

    private ConstructionProject FindPriorityConstructionProject(Citizen citizen)
    {
        if (citizen == null || citizen.CurrentTile == null || AssetRegistry.Instance == null) return null;

        ConstructionProject bestProject = null;
        int bestDistance = int.MaxValue;

        foreach (ConstructionProject project in AssetRegistry.Instance.ConstructionProjects)
        {
            if (project == null || project.AssignedTile == null || !project.NeedsCitizenWork) continue;
            if (!IsFriendlyConstructionProject(project)) continue;
            if (project.AssignedCitizen != null && project.AssignedCitizen != citizen) continue;
            if (project.AssignedTile.OccupyingUnit is Citizen assignedCitizen && assignedCitizen != citizen) continue;

            int distance = GetHexDistance(citizen.CurrentTile.GridPosition, project.AssignedTile.GridPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestProject = project;
            }
        }

        return bestProject;
    }

    private bool IsFriendlyConstructionProject(ConstructionProject project)
    {
        if (project == null) return false;

        PlayerOwnership cityOwnership = GetComponent<PlayerOwnership>();
        PlayerOwnership projectOwnership = project.GetComponent<PlayerOwnership>();

        if (cityOwnership == null || projectOwnership == null)
        {
            return cityOwnership == null && projectOwnership == null;
        }

        return cityOwnership.OwnerId == projectOwnership.OwnerId;
    }

    private static bool IsBuildTargetStillValid(BuildQueueEntry job)
    {
        if (job == null || job.targetTile == null) return false;

        if (job.IsRoad)
        {
            return !job.targetTile.HasRoad && job.targetTile.OccupyingBuilding == null;
        }

        return job.targetTile.OccupyingBuilding == null;
    }

    private static int GetHexDistance(Vector2Int a, Vector2Int b)
    {
        int dq = a.x - b.x;
        int dr = a.y - b.y;
        int ds = (-a.x - a.y) - (-b.x - b.y);
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
    }

    private void ProcessUnitProductionQueue()
    {
        if (_unitProductionQueue.Count == 0) return;

        UnitProductionQueueEntry currentEntry = _unitProductionQueue.Peek();
        if (currentEntry == null || currentEntry.unitPrefab == null)
        {
            _unitProductionQueue.Dequeue();
            return;
        }

        float spendCap = Mathf.Max(0f, productionSpendThresholdPerTurn);
        float available = currentEntry.usesProduction ? productionStockpile : foodStockpile;
        float toSpend = Mathf.Min(spendCap, currentEntry.RemainingCost, available);

        if (toSpend > 0f)
        {
            if (currentEntry.usesProduction) DeductProduction(toSpend);
            else DeductFood(toSpend);

            currentEntry.spentProduction += toSpend;
            Debug.Log($"[City] {name} spent {toSpend:F1} toward {currentEntry.unitName}. Progress: {currentEntry.spentProduction:F1}/{currentEntry.totalCost:F1}");
        }

        if (!currentEntry.IsComplete) return;

        if (!GameplayCommandService.TrySpawnQueuedUnit(this, currentEntry.unitPrefab, currentEntry.unitName))
        {
            Debug.LogWarning($"[City] {name} finished paying for {currentEntry.unitName}, but no spawn tile is available yet.");
            return;
        }

        _unitProductionQueue.Dequeue();
    }
}
