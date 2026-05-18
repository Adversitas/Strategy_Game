using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Handles the "Ghost" building preview and confirmation logic for AOE2-style placement.
/// </summary>
public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<PlacementManager>() != null) return;
        new GameObject("PlacementManager").AddComponent<PlacementManager>();
    }

    [Header("Colors")]
    [SerializeField] private Color validColor = new Color(1, 1, 1, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1, 0, 0, 0.5f);

    private GameObject _ghostObject;
    private GameObject _originalPrefab;
    private Action<GridTile> _onPlacedCallback;
    private bool _isPlacing;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Starts the placement mode with a specific building prefab.
    /// </summary>
    public void StartPlacement(GameObject prefab, Action<GridTile> onPlaced)
    {
        Debug.Log($"[PlacementManager] StartPlacement called with prefab: {(prefab != null ? prefab.name : "NULL (ROAD)")}");
        
        if (_isPlacing) CancelPlacement();

        _originalPrefab = prefab;
        _onPlacedCallback = onPlaced;
        _isPlacing = true;

        if (prefab != null)
        {
            // Create ghost from prefab
            _ghostObject = Instantiate(prefab);
            TileObjectScale.ApplyTo(_ghostObject);
            
            // Disable components that shouldn't run on a ghost
            if (_ghostObject.TryGetComponent(out Collider c)) c.enabled = false;
            if (_ghostObject.TryGetComponent(out Collider2D c2)) c2.enabled = false;
            
            // Disable all MonoBehaviours to stop logic
            MonoBehaviour[] scripts = _ghostObject.GetComponentsInChildren<MonoBehaviour>();
            foreach(var script in scripts) script.enabled = false;
        }
        else
        {
            // Create a procedural road ghost
            _ghostObject = new GameObject("RoadGhost");
            SpriteRenderer sr = _ghostObject.AddComponent<SpriteRenderer>();
            
            // Try to find a hex sprite from the grid generator
            if (SquareGridGenerator.Instance != null)
            {
                // We'll peek at a tile to get the sprite
                GridTile sampleTile = FindFirstObjectByType<GridTile>();
                if (sampleTile != null) sr.sprite = sampleTile.GetComponent<SpriteRenderer>().sprite;
            }
            
            sr.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            sr.sortingOrder = 100;
            TileObjectScale.ApplyTo(_ghostObject);
        }
        
        SetGhostColor(validColor);
    }

    private GridTile _currentHoveredTile;

    public void SetHoveredTile(GridTile tile)
    {
        if (!_isPlacing) return;
        _currentHoveredTile = tile;
    }

    private void Update()
    {
        if (!_isPlacing || _ghostObject == null) return;

        // Cancel on Escape
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelPlacement();
            return;
        }

        // Manual Raycast to avoid OnMouseEnter discrepancies
        GridTile hoveredTile = null;
        Camera[] allCams = Camera.allCameras.OrderByDescending(c => c.depth).ToArray();
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 1. Try to find the camera with the StrategyCameraController first
        Camera cam = allCams.FirstOrDefault(c => c.GetComponent("StrategyCameraController") != null);
        
        // 2. If not found, fall back to the highest depth camera
        if (cam == null) cam = allCams.FirstOrDefault(c => c.enabled);

        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(mousePos);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray);
            
            if (hit.collider != null)
            {
                hoveredTile = hit.collider.GetComponent<GridTile>();
                if (hoveredTile != null && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[PlacementManager] Hit via {cam.name} at {hoveredTile.GridPosition}");
                }
            }
        }

        if (hoveredTile != null)
        {
            if (!_ghostObject.activeSelf) _ghostObject.SetActive(true);
            
            // Update position
            Vector3 pos = hoveredTile.transform.position;
            pos.z = -1.0f;
            _ghostObject.transform.position = pos;

            // Check validity
            bool isValid = IsPlacementValid(hoveredTile);
            SetGhostColor(isValid ? validColor : invalidColor);

            // Confirm on Click
            if (isValid && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ConfirmPlacement(hoveredTile);
            }
        }
        else
        {
            if (_ghostObject.activeSelf) _ghostObject.SetActive(false);
        }
    }

    private bool IsPlacementValid(GridTile tile)
    {
        if (tile == null) return false;
        
        bool noBuilding = tile.OccupyingBuilding == null;
        
        // If placing a building (prefab exists)
        if (_originalPrefab != null)
        {
            bool isCity = _originalPrefab.GetComponent<City>() != null || _originalPrefab.name.Contains("City") || _originalPrefab.name.Contains("ConstructionProject");
            bool hasResource = tile.TileResource != ResourceType.None;
            
            if (isCity)
            {
                // Cities can be placed anywhere (except where there is a building or pending road)
                return noBuilding && !tile.IsPendingRoad;
            }
            else
            {
                // Facilities require a resource
                return noBuilding && hasResource && !tile.IsPendingRoad;
            }
        }
        else
        {
            // If placing a road (no prefab)
            return noBuilding && !tile.HasRoad && !tile.IsPendingRoad && HasAdjacentRoadOrDistrict(tile);
        }
    }

    private bool HasAdjacentRoadOrDistrict(GridTile tile)
    {
        if (tile == null || SquareGridGenerator.Instance == null) return false;

        foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(tile))
        {
            if (neighbor == null) continue;

            bool hasRoadConnection = neighbor.HasRoad || neighbor.IsPendingRoad;
            bool hasDistrictConnection = neighbor.OccupyingBuilding != null;

            if (hasRoadConnection || hasDistrictConnection)
            {
                return true;
            }
        }

        return false;
    }

    private void SetGhostColor(Color color)
    {
        if (_ghostObject == null) return;
        
        // Ensure ghost is always in front and visible
        Renderer[] renderers = _ghostObject.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.material.color = color;
        }

        SpriteRenderer[] sprites = _ghostObject.GetComponentsInChildren<SpriteRenderer>();
        foreach (var s in sprites)
        {
            s.color = color;
            s.sortingOrder = 100; // Force to top
        }
    }

    private void ConfirmPlacement(GridTile tile)
    {
        _onPlacedCallback?.Invoke(tile);
        
        // If shift is held, keep placing (don't stop placement)
        if (!Keyboard.current.shiftKey.isPressed)
        {
            StopPlacement();
        }
        else
        {
            Debug.Log("[PlacementManager] Shift held, continuing placement...");
        }
    }

    public void CancelPlacement()
    {
        StopPlacement();
    }

    private void StopPlacement()
    {
        _isPlacing = false;
        if (_ghostObject != null) Destroy(_ghostObject);
        _ghostObject = null;
    }

    private Camera GetGameplayCamera()
    {
        // Try to find the camera with the controller first
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in cams)
        {
            if (c.name.Contains("Strategy") || c.GetComponent("StrategyCameraController") != null)
                return c;
        }
        
        return Camera.main;
    }
}
