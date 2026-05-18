using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A building constructed on resources that generates production over time.
/// </summary>
public class Facility : MonoBehaviour, IGridTarget
{
    [Header("Resource Generation")]
    [SerializeField] private float productionStockpile = 0;
    [SerializeField] private int maxProductionStockpile = 100;
    [SerializeField] private int productionPerTurn = 10;
    [SerializeField] private float productionSpendThresholdPerTurn = 10f;
    [SerializeField] private float turnDuration = 5f;

    private float _timer;
    [SerializeField] private GridTile assignedTile;
    [SerializeField] private bool isConnectedToCity;

    public GridTile AssignedTile 
    { 
        get => assignedTile; 
        set => assignedTile = value; 
    }

    public bool IsConnectedToCity => isConnectedToCity;

    public float ProductionStockpile => productionStockpile;
    public int MaxProductionStockpile => maxProductionStockpile;
    public float ProductionSpendThresholdPerTurn => productionSpendThresholdPerTurn;

    private void Start()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.RegisterFacility(this);
    }

    private void OnDestroy()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.UnregisterFacility(this);
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= turnDuration)
        {
            _timer = 0;
            
            // Check if connected to a city before producing
            isConnectedToCity = CheckConnectivity();

            if (isConnectedToCity && productionStockpile < maxProductionStockpile)
            {
                productionStockpile += productionPerTurn;
            }
        }
    }

    private bool CheckConnectivity()
    {
        if (assignedTile == null) return false;

        // BFS to find a city via roads
        HashSet<GridTile> visited = new HashSet<GridTile>();
        Queue<GridTile> queue = new Queue<GridTile>();
        
        queue.Enqueue(assignedTile);
        visited.Add(assignedTile);

        // Exception: If we are right next to a city, we are automatically connected
        foreach (var neighbor in SquareGridGenerator.Instance.GetNeighbors(assignedTile))
        {
            if (neighbor.OccupyingBuilding != null && neighbor.OccupyingBuilding.GetComponent<City>() != null)
                return true;
        }

        while (queue.Count > 0)
        {
            GridTile current = queue.Dequeue();

            // Success if we reached a City
            if (current.OccupyingBuilding != null && current.OccupyingBuilding.GetComponent<City>() != null)
            {
                return true;
            }

            // Continue path if the tile has a road (or is the starting tile)
            if (current == assignedTile || current.HasRoad)
            {
                foreach (var neighbor in SquareGridGenerator.Instance.GetNeighbors(current))
                {
                    if (visited.Contains(neighbor)) continue;

                    // Neighbors are valid if they have a road OR contain a city
                    bool isRoad = neighbor.HasRoad;
                    bool isCity = neighbor.OccupyingBuilding != null && neighbor.OccupyingBuilding.GetComponent<City>() != null;

                    if (isRoad || isCity)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return false;
    }

    public void DeductProduction(float amount)
    {
        productionStockpile = Mathf.Max(0, productionStockpile - amount);
    }
}
