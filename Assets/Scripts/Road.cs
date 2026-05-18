using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws road connections from a tile center to connected neighbor borders.
/// </summary>
public class Road : MonoBehaviour
{
    private const float RoadWidth = 0.075f;
    private const float JunctionRadius = 0.07f;

    private static readonly Color RoadColor = new Color(0.33f, 0.33f, 0.36f, 1f);

    private GridTile _tile;

    public void Configure(GridTile tile)
    {
        _tile = tile;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (_tile == null || SquareGridGenerator.Instance == null) return;

        ClearSegments();

        List<GridTile> connections = new List<GridTile>();
        foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(_tile))
        {
            if (ShouldConnectTo(neighbor))
            {
                connections.Add(neighbor);
            }
        }

        if (connections.Count == 2)
        {
            CreateRoundedCorner(connections[0], connections[1]);
            return;
        }

        foreach (GridTile neighbor in connections)
        {
            CreateSegment(GetTileBorderPoint(neighbor));
        }

        if (connections.Count > 2)
        {
            CreateJunctionCap();
        }
    }

    private bool ShouldConnectTo(GridTile neighbor)
    {
        if (neighbor == null) return false;

        if (_tile != null && _tile.HasRoad)
        {
            return neighbor.HasRoad || neighbor.OccupyingBuilding != null;
        }

        return neighbor.HasRoad;
    }

    private void CreateSegment(Vector3 end)
    {
        GameObject segment = new GameObject("RoadSegment");
        segment.transform.SetParent(transform, false);

        LineRenderer line = segment.AddComponent<LineRenderer>();
        ConfigureLine(line);

        line.positionCount = 2;
        line.SetPosition(0, WithRoadZ(_tile.GetVisualCenter()));
        line.SetPosition(1, WithRoadZ(end));
    }

    private void CreateRoundedCorner(GridTile first, GridTile second)
    {
        GameObject segment = new GameObject("RoadCorner");
        segment.transform.SetParent(transform, false);

        LineRenderer line = segment.AddComponent<LineRenderer>();
        ConfigureLine(line);

        line.positionCount = 3;
        line.SetPosition(0, WithRoadZ(GetTileBorderPoint(first)));
        line.SetPosition(1, WithRoadZ(_tile.GetVisualCenter()));
        line.SetPosition(2, WithRoadZ(GetTileBorderPoint(second)));
    }

    private void ConfigureLine(LineRenderer line)
    {
        line.useWorldSpace = true;
        line.startWidth = RoadWidth;
        line.endWidth = RoadWidth;
        line.numCapVertices = 12;
        line.numCornerVertices = 16;
        line.sortingOrder = 0;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = RoadColor;
        line.endColor = RoadColor;
    }

    private void CreateJunctionCap()
    {
        GameObject cap = new GameObject("RoadJunction");
        cap.transform.SetParent(transform, false);

        LineRenderer line = cap.AddComponent<LineRenderer>();
        ConfigureLine(line);
        line.loop = true;
        line.positionCount = 16;

        Vector3 center = WithRoadZ(_tile.GetVisualCenter());
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = Mathf.PI * 2f * i / line.positionCount;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * JunctionRadius;
            line.SetPosition(i, point);
        }
    }

    private Vector3 GetTileBorderPoint(GridTile neighbor)
    {
        return Vector3.Lerp(_tile.GetVisualCenter(), neighbor.GetVisualCenter(), 0.5f);
    }

    private static Vector3 WithRoadZ(Vector3 position)
    {
        position.z = -0.05f;
        return position;
    }

    private void ClearSegments()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}
