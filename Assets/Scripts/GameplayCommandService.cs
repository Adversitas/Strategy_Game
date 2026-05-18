using UnityEngine;

/// <summary>
/// Routes player intent through one place so local UI stops mutating simulation
/// state directly. This is the main multiplayer-readiness seam for future RPCs.
/// </summary>
public static class GameplayCommandService
{
    public static bool CanControl(Component actor)
    {
        if (actor == null) return false;

        PlayerOwnership ownership = actor.GetComponent<PlayerOwnership>();
        if (ownership == null)
        {
            PlayerOwnership.EnsureLocalOwner(actor.gameObject);
        }

        if (GameAuthority.Instance == null)
        {
            Debug.LogWarning($"[GameplayCommandService] GameAuthority missing while checking control for {actor.name}. Allowing command locally.");
            return true;
        }

        return GameAuthority.Instance.CanIssueCommands(actor);
    }

    public static bool TryAssignCourierDestination(Courier courier, IGridTarget destination)
    {
        if (courier == null || destination == null || !CanControl(courier)) return false;

        courier.TargetDestination = destination;
        return true;
    }

    public static bool TryQueueConstruction(Citizen citizen, GridTile tile, GameObject constructionPrefab, GameObject targetPrefab = null)
    {
        if (citizen == null || tile == null || !CanControl(citizen)) return false;

        citizen.StartConstruction(tile, constructionPrefab, targetPrefab);
        return true;
    }

    public static bool TryQueueRoad(Citizen citizen, GridTile tile)
    {
        if (citizen == null || tile == null || !CanControl(citizen)) return false;

        tile.IsPendingRoad = true;
        citizen.StartConstruction(tile, null, null);
        return true;
    }

    public static bool TryBuildCityFromMenu(GridTile tile, GameObject cityPrefab)
    {
        return tile != null && cityPrefab != null && GameAuthority.Instance != null;
    }

    public static bool TryQueueCityConstruction(City city, GridTile tile, GameObject constructionPrefab, GameObject targetPrefab = null)
    {
        if (city == null || tile == null || !CanControl(city)) return false;

        city.QueueConstruction(tile, constructionPrefab, targetPrefab);
        return true;
    }

    public static bool TryQueueCityRoad(City city, GridTile tile)
    {
        if (city == null || tile == null || !CanControl(city)) return false;

        city.QueueRoad(tile);
        return true;
    }

    public static bool TryProduceUnit(City city, GameObject prefab, float cost, string unitName, bool isProduction)
    {
        if (city == null)
        {
            Debug.LogWarning($"[GameplayCommandService] Failed to produce {unitName}: city is null.");
            return false;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[GameplayCommandService] Failed to produce {unitName}: prefab is null.");
            return false;
        }

        if (!CanControl(city))
        {
            Debug.LogWarning($"[GameplayCommandService] Failed to produce {unitName}: no control authority for city {city.name}.");
            return false;
        }

        city.QueueUnitProduction(prefab, cost, unitName, isProduction);
        Debug.Log($"[GameplayCommandService] Queued {unitName} for {city.name} with total cost {cost}");
        return true;
    }

    public static bool TrySpawnQueuedUnit(City city, GameObject prefab, string unitName)
    {
        if (city == null || prefab == null)
        {
            Debug.LogWarning($"[GameplayCommandService] Failed to spawn queued {unitName}: city or prefab is null.");
            return false;
        }

        if (city.AssignedTile == null)
        {
            Debug.LogWarning($"[GameplayCommandService] Failed to spawn queued {unitName}: city {city.name} has no assigned tile.");
            return false;
        }

        GridTile spawnTile = FindSpawnTile(city);
        if (spawnTile == null)
        {
            return false;
        }

        GameObject unitGO = Object.Instantiate(prefab);
        unitGO.name = unitName;
        PlayerOwnership.Inherit(unitGO, city);

        Unit unit = unitGO.GetComponent<Unit>();
        if (unit == null)
        {
            Object.Destroy(unitGO);
            return false;
        }

        if (unitName == "Citizen")
        {
            city.RegisterCitizen();
            if (unit is Citizen citizen) citizen.SetHomeCity(city);
        }
        if (unit is Courier courier) courier.OriginCity = city;

        unit.PlaceOnTile(spawnTile);
        Debug.Log($"[GameplayCommandService] Spawned queued {unitName} for {city.name} on tile {spawnTile.GridPosition}");
        return true;
    }

    private static GridTile FindSpawnTile(City city)
    {
        if (city == null || city.AssignedTile == null) return null;

        if (SquareGridGenerator.Instance == null) return null;

        foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(city.AssignedTile))
        {
            if (neighbor != null && neighbor.IsEmpty)
            {
                return neighbor;
            }
        }

        if (!city.AssignedTile.HasUnit) return city.AssignedTile;

        return null;
    }
}
