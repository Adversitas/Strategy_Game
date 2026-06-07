using UnityEngine;

public static class TraversalRules
{
    public static bool CanTraverseTile(GameObject source, GridTile tile, GridTile target = null)
    {
        if (tile == null) return false;

        if (tile == target)
        {
            return !tile.HasBuilding || IsFriendlyStructure(source, tile.OccupyingBuilding);
        }

        if (tile.HasBuilding)
        {
            return IsFriendlyStructure(source, tile.OccupyingBuilding);
        }

        return tile.HasRoad;
    }

    public static bool IsFriendlyStructure(GameObject source, GameObject structure)
    {
        if (source == null || structure == null) return false;

        PlayerOwnership sourceOwnership = source.GetComponent<PlayerOwnership>();
        PlayerOwnership structureOwnership = structure.GetComponent<PlayerOwnership>();

        if (sourceOwnership == null || structureOwnership == null)
        {
            return sourceOwnership == null && structureOwnership == null;
        }

        return sourceOwnership.OwnerId == structureOwnership.OwnerId;
    }
}
