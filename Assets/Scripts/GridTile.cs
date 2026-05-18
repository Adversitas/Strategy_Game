using UnityEngine;

public enum TerrainType
{
    Plains,
    Forest,
    Mountain,
    Water
}

public enum ResourceType
{
    None,
    Food,
    Wood,
    Stone,
    Gold
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class GridTile : MonoBehaviour
{
    [SerializeField] private Color baseColor = new Color(0.22f, 0.6f, 0.28f);

    private SpriteRenderer _spriteRenderer;
    private GameObject _selectionHalo;
    private Road _road;
    private GameObject _occupyingBuilding;

    public Vector2Int GridPosition { get; private set; }
    public GameObject OccupyingBuilding
    {
        get => _occupyingBuilding;
        set
        {
            _occupyingBuilding = value;
            RefreshRoadVisual();
            RefreshAdjacentRoadVisuals();
        }
    }
    public Unit OccupyingUnit { get; set; }
    public bool HasRoad { get; private set; }
    public bool IsPendingRoad { get; set; }

    
    public bool HasUnit => OccupyingUnit != null;
    public bool HasBuilding => OccupyingBuilding != null;
    public bool IsEmpty => !HasUnit && !HasBuilding;
    
    [Header("Tile Data")]
    [SerializeField] private TerrainType tileTerrain = TerrainType.Plains;
    [SerializeField] private ResourceType tileResource = ResourceType.None;
    
    public TerrainType TileTerrain { get => tileTerrain; set => tileTerrain = value; }
    public ResourceType TileResource { get => tileResource; set => tileResource = value; }

    private Vector3 _baseScale;

    public void Initialize(Vector2Int gridPosition, Color color)
    {
        GridPosition = gridPosition;
        baseColor = color;
        _baseScale = transform.localScale;
        EnsureComponents();
        _spriteRenderer.color = baseColor;
    }

    public void SetSelected(bool selected)
    {
        EnsureComponents();
        
        if (_selectionHalo != null)
        {
            _selectionHalo.SetActive(selected);
        }
        else
        {
            Debug.LogWarning($"GridTile at {GridPosition} is missing 'SelectionHalo' child.", this);
        }
        
        // Ensure the highlighted tile renders on top of its neighbors
        _spriteRenderer.sortingOrder = selected ? 1 : 0;
    }

    private void EnsureComponents()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        if (_selectionHalo == null)
        {
            Transform halo = transform.Find("SelectionHalo");
            if (halo != null)
            {
                _selectionHalo = halo.gameObject;
            }
        }
    }

    public void AddRoad()
    {
        if (HasRoad) return;

        HasRoad = true;
        IsPendingRoad = false;
        RefreshRoadVisual();
        RefreshAdjacentRoadVisuals();
    }

    public Vector3 GetVisualCenter()
    {
        EnsureComponents();
        if (_spriteRenderer != null) return _spriteRenderer.bounds.center;

        return transform.position;
    }

    private void RefreshRoadVisual()
    {
        bool shouldRenderRoad = HasRoad || (HasBuilding && HasAdjacentRoad());

        if (!shouldRenderRoad)
        {
            if (_road != null)
            {
                Destroy(_road.gameObject);
                _road = null;
            }
            return;
        }

        if (_road == null)
        {
            GameObject roadObj = new GameObject("RoadVisual");
            roadObj.transform.SetParent(transform);
            roadObj.transform.localPosition = new Vector3(0, 0, -0.05f);
            TileObjectScale.ApplyTo(roadObj);
            _road = roadObj.AddComponent<Road>();
        }

        if (_road != null)
        {
            _road.Configure(this);
        }
    }

    private void RefreshAdjacentRoadVisuals()
    {
        if (SquareGridGenerator.Instance == null) return;

        foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(this))
        {
            if (neighbor != null)
            {
                neighbor.RefreshRoadVisual();
            }
        }
    }

    private bool HasAdjacentRoad()
    {
        if (SquareGridGenerator.Instance == null) return false;

        foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(this))
        {
            if (neighbor != null && neighbor.HasRoad)
            {
                return true;
            }
        }

        return false;
    }

    private void OnMouseEnter()
    {
        Debug.Log($"[GridTile] Mouse entered: {GridPosition}");
        PlacementManager.Instance.SetHoveredTile(this);
    }

    private void OnMouseExit()
    {
        PlacementManager.Instance.SetHoveredTile(null);
    }
}
