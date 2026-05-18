using UnityEngine;

/// <summary>
/// A Market district that acts as a collection point for Couriers.
/// Any Courier passing through this tile will automatically transfer resources to the OriginCity.
/// </summary>
public class Market : MonoBehaviour, IGridTarget
{
    public GridTile AssignedTile { get; private set; }
    public City OriginCity { get; private set; }

    public void Initialize(GridTile tile)
    {
        AssignedTile = tile;
        transform.position = tile.GetVisualCenter() + new Vector3(0, 0, -0.1f);
        
        // Find the city this market is connected to via roads
        FindOriginCity();
        
        if (AssetRegistry.Instance != null)
            AssetRegistry.Instance.RegisterMarket(this);
    }

    private void OnDestroy()
    {
        if (AssetRegistry.Instance != null)
            AssetRegistry.Instance.UnregisterMarket(this);
    }

    private void FindOriginCity()
    {
        if (AssignedTile == null || SquareGridGenerator.Instance == null) return;

        // Simple BFS to find the nearest City via roads
        System.Collections.Generic.Queue<GridTile> queue = new System.Collections.Generic.Queue<GridTile>();
        System.Collections.Generic.HashSet<GridTile> visited = new System.Collections.Generic.HashSet<GridTile>();

        queue.Enqueue(AssignedTile);
        visited.Add(AssignedTile);

        while (queue.Count > 0)
        {
            GridTile current = queue.Dequeue();

            // Check if there's a city here
            City city = current.OccupyingBuilding != null ? current.OccupyingBuilding.GetComponent<City>() : null;
            if (city != null)
            {
                OriginCity = city;
                Debug.Log($"[Market] Linked to Origin City: {city.name} via road network.");
                return;
            }

            foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(current))
            {
                // Only follow roads (or the current tile which is the market itself)
                if (!visited.Contains(neighbor) && (neighbor.HasRoad || neighbor == AssignedTile))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        Debug.LogWarning($"[Market] Could not find a connected City via roads for Market at {AssignedTile.GridPosition}.");
    }
}
