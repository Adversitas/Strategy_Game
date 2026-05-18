using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SquareGridGenerator : MonoBehaviour
{
    public static SquareGridGenerator Instance { get; private set; }

    /// <summary>Fired when a tile is selected. Strategy logic might listen to this.</summary>
    public static event Action<GridTile> OnTileSelected;
    /// <summary>Fired when the build menu is requested (right click on empty tile).</summary>
    public static event Action<GridTile> OnBuildMenuRequested;

    [Header("Grid Size")]
    [SerializeField] private int width = 30;
    [SerializeField] private int height = 20;
    [SerializeField] private float tileSize = 1f;

    [Header("Tile Colors")]
    [SerializeField] private Color lightTileColor = new Color(0.26f, 0.65f, 0.31f);
    [SerializeField] private Color darkTileColor  = new Color(0.2f,  0.57f, 0.27f);

    [Header("Hex Settings")]
    [Range(1.0f, 2.5f)]
    [SerializeField] private float hexVisualScale = 2.0f;

    [Header("Resource Distribution")]
    [Range(0f, 1f)]
    [SerializeField] private float resourceChance = 0.2f;

    [Header("Prefabs")]
    [SerializeField] private GameObject selectionHaloPrefab;
    [SerializeField] private GameObject startingCityPrefab;

    [Header("Starting Setup")]
    [SerializeField] private bool spawnStartingCity = true;
    [SerializeField] private Vector2Int startingCityPosition = new Vector2Int(4, 5);

    // O(1) lookup by grid coordinate
    private readonly Dictionary<Vector2Int, GridTile> _tileLookup = new Dictionary<Vector2Int, GridTile>();

    private Camera _mainCamera;
    private GridTile _selectedTile;
    private Sprite _sharedSquareSprite;
    private Transform _board;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StrategyCameraController ctrl = FindFirstObjectByType<StrategyCameraController>();
        if (ctrl != null) _mainCamera = ctrl.GetComponent<Camera>();
        if (_mainCamera == null) _mainCamera = Camera.main;

        if (startingCityPrefab == null)
        {
            startingCityPrefab = Resources.Load<GameObject>("City");
        }

        ClearGrid();
        GenerateGrid();
        SpawnStartingCity();
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        // Prevent clicking through the UI menus
        if ((BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsMouseOverMenu()) || 
            (CityMenuUI.Instance != null && CityMenuUI.Instance.IsMouseOverMenu()) ||
            (UnitMenuUI.Instance != null && UnitMenuUI.Instance.IsMouseOverMenu()))
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectTileAtMousePosition();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryOpenBuildMenuAtMousePosition();
        }
    }

    [ContextMenu("Regenerate Grid")]
    public void RegenerateGrid()
    {
        ClearGrid();
        GenerateGrid();
    }

    public GridTile GetTileAt(Vector2Int pos)
    {
        _tileLookup.TryGetValue(pos, out GridTile tile);
        return tile;
    }

    private void GenerateGrid()
    {
        EnsureBoard();
        _sharedSquareSprite = CreateHexSprite();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                CreateTile(new Vector2Int(x, y));
            }
        }
    }

    private void CreateTile(Vector2Int gridPos)
    {
        GameObject tileObject = new GameObject($"Tile_{gridPos.x}_{gridPos.y}");
        tileObject.transform.SetParent(_board);

        // Pointy-top Hex positioning
        // Distance between horizontal centers is sqrt(3) * R
        // Distance between vertical centers is 1.5 * R
        float xPos = tileSize * (Mathf.Sqrt(3f) * gridPos.x + Mathf.Sqrt(3f) / 2f * gridPos.y);
        float yPos = tileSize * (1.5f * gridPos.y);
        
        tileObject.transform.localPosition = new Vector3(xPos, yPos, 0f);
        // Pointy-top hex height is 2R, width is sqrt(3)R. 
        // We scale by the visual scale (2.0 is perfect fit).
        tileObject.transform.localScale    = new Vector3(tileSize * hexVisualScale, tileSize * hexVisualScale, 1f); 

        SpriteRenderer sr  = tileObject.AddComponent<SpriteRenderer>();
        sr.sprite           = _sharedSquareSprite;
        sr.sortingOrder     = 0;

        GridTile tile = tileObject.AddComponent<GridTile>();
        // Checkerboard-ish pattern for hexes
        Color tileColor = (gridPos.x % 2 == 0 ^ gridPos.y % 2 == 0) ? lightTileColor : darkTileColor;
        tile.Initialize(gridPos, tileColor);

        if (selectionHaloPrefab != null)
        {
            GameObject halo = Instantiate(selectionHaloPrefab, tileObject.transform);
            halo.name = "SelectionHalo";
            if (halo.TryGetComponent<SpriteRenderer>(out var haloSr))
            {
                haloSr.sprite = _sharedSquareSprite;
            }
            halo.transform.localPosition = Vector3.zero;
            halo.SetActive(false);
        }
        else
        {
            GameObject haloPlaceholder = new GameObject("SelectionHalo");
            haloPlaceholder.transform.SetParent(tileObject.transform);
            haloPlaceholder.transform.localPosition = Vector3.zero;
            haloPlaceholder.SetActive(false);
        }

        // Randomize resources
        TryGenerateResource(tile);

        _tileLookup[gridPos] = tile;
    }

    private void TryGenerateResource(GridTile tile)
    {
        if (UnityEngine.Random.value > resourceChance) return;

        // Pick a random resource type (skipping 'None')
        ResourceType[] resources = { ResourceType.Food, ResourceType.Wood, ResourceType.Stone, ResourceType.Gold };
        ResourceType picked = resources[UnityEngine.Random.Range(0, resources.Length)];
        
        tile.TileResource = picked;

        // Load sprite from Resources/Extra
        string spriteName = picked.ToString(); // e.g. "Food", "Gold"
        Sprite resourceSprite = Resources.Load<Sprite>($"Extra/{spriteName}");

        if (resourceSprite != null)
        {
            GameObject resObj = new GameObject($"Resource_{spriteName}");
            resObj.transform.SetParent(tile.transform);
            resObj.transform.localPosition = new Vector3(0, 0, -0.1f);
            TileObjectScale.ApplyTo(resObj);

            SpriteRenderer sr = resObj.AddComponent<SpriteRenderer>();
            sr.sprite = resourceSprite;
            sr.sortingOrder = 5; // Above tile, below units usually
        }
    }

    public void SelectTile(GridTile tile, bool centerCamera = false)
    {
        if (tile == null) return;

        if (_selectedTile != null)
        {
            _selectedTile.SetSelected(false);
        }

        _selectedTile = tile;
        _selectedTile.SetSelected(true);
        OnTileSelected?.Invoke(tile);
        
        if (centerCamera)
        {
            // Center camera on tile
            Camera.main.transform.position = new Vector3(tile.transform.position.x, tile.transform.position.y, Camera.main.transform.position.z);
        }
    }

    public GridTile GetTileAtMouse()
    {
        if (Camera.main == null || Mouse.current == null) return null;
        
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.back, Vector3.zero); // Plane at z=0 facing the camera
        
        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 worldPos = ray.GetPoint(enter);
            Collider2D hit = Physics2D.OverlapPoint(worldPos);
            if (hit != null)
            {
                return hit.GetComponent<GridTile>();
            }
        }
        return null;
    }

    public List<GridTile> GetNeighbors(GridTile tile)
    {
        List<GridTile> neighbors = new List<GridTile>();
        if (tile == null) return neighbors;

        Vector2Int pos = tile.GridPosition; // q, r axial coordinates
        
        // Pointy-top hex neighbor offsets in axial (q, r)
        Vector2Int[] axialOffsets = {
            new Vector2Int(1, 0),   // E
            new Vector2Int(1, -1),  // NE
            new Vector2Int(0, -1),  // NW
            new Vector2Int(-1, 0),  // W
            new Vector2Int(-1, 1),  // SW
            new Vector2Int(0, 1)    // SE
        };

        foreach (var offset in axialOffsets)
        {
            GridTile neighbor = GetTileAt(pos + offset);
            if (neighbor != null) neighbors.Add(neighbor);
        }
        return neighbors;
    }

    private void TrySelectTileAtMousePosition()
    {
        GridTile tile = GetTileAtMousePosition();
        SelectTile(tile);
    }

    private void TryOpenBuildMenuAtMousePosition()
    {
        GridTile tile = GetTileAtMousePosition();
        if (tile == null) return;

        if (tile.IsEmpty)
        {
            OnBuildMenuRequested?.Invoke(tile);
        }
    }

    private GridTile GetTileAtMousePosition()
    {
        if (_mainCamera == null) return null;
        EnsureBoard();

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        float depth = -_mainCamera.transform.position.z;
        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, depth));
        Vector3 localPos = _board.InverseTransformPoint(worldPos);

        // Convert world to fractional axial coordinates
        float q = (Mathf.Sqrt(3f) / 3f * localPos.x - 1f / 3f * localPos.y) / tileSize;
        float r = (2f / 3f * localPos.y) / tileSize;

        // Hex rounding logic
        Vector2Int gridCoord = HexRound(q, r);

        _tileLookup.TryGetValue(gridCoord, out GridTile tile);
        return tile;
    }

    private Vector2Int HexRound(float q, float r)
    {
        float s = -q - r;
        int qi = Mathf.RoundToInt(q);
        int ri = Mathf.RoundToInt(r);
        int si = Mathf.RoundToInt(s);

        float q_diff = Mathf.Abs(qi - q);
        float r_diff = Mathf.Abs(ri - r);
        float s_diff = Mathf.Abs(si - s);

        if (q_diff > r_diff && q_diff > s_diff)
            qi = -ri - si;
        else if (r_diff > s_diff)
            ri = -qi - si;
        // si is not used directly but it is part of the constraint qi+ri+si=0
        
        return new Vector2Int(qi, ri);
    }

    private void ClearGrid()
    {
        _tileLookup.Clear();
        _selectedTile = null;

        EnsureBoard();

        if (_board != null)
        {
            for (int i = _board.childCount - 1; i >= 0; i--)
            {
                Transform child = _board.GetChild(i);
                if (child.GetComponent<GridTile>() != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    private void SpawnStartingCity()
    {
        if (!spawnStartingCity || startingCityPrefab == null) return;

        GridTile tile = GetTileAt(startingCityPosition);
        if (tile == null || tile.OccupyingBuilding != null) return;

        SpriteRenderer tileRenderer = tile.GetComponent<SpriteRenderer>();
        Vector3 spawnPos = tileRenderer != null ? tileRenderer.bounds.center : tile.transform.position;
        spawnPos.z = -0.1f;

        GameObject cityGO = Instantiate(startingCityPrefab, spawnPos, Quaternion.identity);
        TileObjectScale.ApplyTo(cityGO);
        PlayerOwnership.EnsureLocalOwner(cityGO);

        City city = cityGO.GetComponent<City>();
        if (city == null)
        {
            city = cityGO.AddComponent<City>();
        }

        city.AssignedTile = tile;
        city.AddProduction(200f);
        tile.OccupyingBuilding = cityGO;

        Debug.Log($"[SquareGridGenerator] Spawned starting city at {startingCityPosition}");
    }

    private void EnsureBoard()
    {
        if (_board != null) return;

        _board = transform.Find("Board");
        if (_board == null)
        {
            GameObject go = new GameObject("Board");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            _board = go.transform;
        }
    }

    private static Sprite CreateHexSprite()
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        // Fill transparent
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        // For pointy-top hex, height is 2R. We want 2R to be almost the full size.
        float radius = (size / 2f) - 2f; 

        // Pointy-top hex corners (start at -30 deg for pointy top)
        Vector2[] corners = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle_deg = 60 * i - 30;
            float angle_rad = Mathf.Deg2Rad * angle_deg;
            corners[i] = center + new Vector2(radius * Mathf.Cos(angle_rad), radius * Mathf.Sin(angle_rad));
        }

        // Bounding box for hex is sqrt(3)R wide and 2R high
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pixelPosition = new Vector2(x, y);
                bool isInside = IsPointInPolygon(pixelPosition, corners);
                float edgeDistance = float.MaxValue;
                for (int i = 0; i < 6; i++)
                {
                    Vector2 p1 = corners[i];
                    Vector2 p2 = corners[(i + 1) % 6];
                    edgeDistance = Mathf.Min(edgeDistance, DistanceToSegment(pixelPosition, p1, p2));
                }

                if (isInside)
                {
                    float alpha = Mathf.Clamp01(edgeDistance);
                    float border = Mathf.Clamp01((4.0f - edgeDistance) / 3.0f);
                    Color fill = Color.Lerp(Color.white, new Color(0.9f, 0.9f, 0.9f, 1.0f), border);
                    fill.a = alpha;
                    pixels[y * size + x] = fill;
                }
                else if (edgeDistance < 1.0f)
                {
                    pixels[y * size + x] = new Color(1f, 1f, 1f, 1.0f - edgeDistance);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        
        // Pixels per unit: If radius is 'R' pixels, and we want it to be 1.0 Unity units,
        // then pixelsPerUnit should be 'R'.
        // However, Unity transforms usually work with diameter.
        // If we want the Sprite to be 1x1 in Unity units at scale 1, we set PPU to 'size'.
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), radius);
    }

    private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool result = false;
        int j = polygon.Length - 1;
        for (int i = 0; i < polygon.Length; i++)
        {
            if (polygon[i].y < point.y && polygon[j].y >= point.y || polygon[j].y < point.y && polygon[i].y >= point.y)
            {
                if (polygon[i].x + (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) * (polygon[j].x - polygon[i].x) < point.x)
                {
                    result = !result;
                }
            }
            j = i;
        }
        return result;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        float l2 = (a - b).sqrMagnitude;
        if (l2 == 0.0f) return (p - a).magnitude;
        float t = Mathf.Max(0, Mathf.Min(1, Vector2.Dot(p - a, b - a) / l2));
        Vector2 projection = a + t * (b - a);
        return (p - projection).magnitude;
    }
}
