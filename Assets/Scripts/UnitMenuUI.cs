using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// A menu that pops up at the bottom when a unit is selected, showing stats, inventory, and orders.
/// </summary>
public class UnitMenuUI : MonoBehaviour
{
    public static UnitMenuUI Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject facilityPrefab;

    private Canvas _canvas;
    private RectTransform _panel;
    private Text _unitNameLabel;
    private Text _statsLabel;
    private Text _inventoryLabel;
    private Text _ordersLabel;
    private GameObject _buildFacilityBtn;
    private GameObject _buildRoadBtn;

    
    private Unit _selectedUnit;
    private bool _isOpen;
    private Coroutine _slideCoroutine;

    private const float SlideDuration = 0.2f;

    // Palette (matching the game's aesthetic)
    private static readonly Color PanelBg = new Color(0.1f, 0.1f, 0.12f, 0.95f); // Dark slate
    private static readonly Color TextWhite = new Color(0.9f, 0.9f, 1f, 1f);
    private static readonly Color TextMuted = new Color(0.6f, 0.6f, 0.7f, 1f);
    private static readonly Color HighlightColor = new Color(1f, 0.8f, 0.3f, 1f); // Gold-ish for stats

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<UnitMenuUI>() != null) return;
        new GameObject("UnitMenuUI").AddComponent<UnitMenuUI>();
    }

    private void Awake()
    {
        Instance = this;
        if (facilityPrefab == null) facilityPrefab = Resources.Load<GameObject>("Facility");
        BuildUI();
        HideImmediate();
    }

    private void OnEnable()
    {
        SquareGridGenerator.OnTileSelected += HandleTileSelected;
    }

    private void OnDisable()
    {
        SquareGridGenerator.OnTileSelected -= HandleTileSelected;
    }

    private void Update()
    {
        if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SlideOut();

        if (_isOpen && _selectedUnit != null)
        {
            UpdateUI();
        }
        else if (_isOpen)
        {
            // The selected unit was destroyed.
            SlideOut();
        }
    }

    private void HandleTileSelected(GridTile tile)
    {
        // Couriers are tracked separately on the tile so they don't block friendly units.
        Unit unitOnTile = tile == null ? null : tile.HasUnit ? tile.OccupyingUnit : tile.OccupyingCourier;
        if (unitOnTile != null)
        {
            _selectedUnit = unitOnTile;
            _unitNameLabel.text = _selectedUnit.name.Replace("(Clone)", "").ToUpper();
            
            // Debug log to help troubleshoot build conditions
            if (_selectedUnit.CurrentTile != null)
            {
                Debug.Log($"[UnitMenuUI] Selected {_selectedUnit.name} at {_selectedUnit.CurrentTile.GridPosition}. " +
                          $"Resource: {_selectedUnit.CurrentTile.TileResource}, Building: {(_selectedUnit.CurrentTile.OccupyingBuilding != null ? _selectedUnit.CurrentTile.OccupyingBuilding.name : "None")}");
            }
            
            if (!_isOpen) SlideIn();
        }
        else
        {
            // If we selected something else (empty tile or building), close the unit menu
            if (_isOpen) SlideOut();
        }
    }

    private void UpdateUI()
    {
        if (_selectedUnit == null) return;

        // Stats Update
        _statsLabel.text = $"HP: {_selectedUnit.CurrentHealth}/{_selectedUnit.MaxHealth}\n" +
                           $"ATK: {_selectedUnit.Attack}\n" +
                           $"MOVE: {_selectedUnit.CurrentMovementPoints:F1}/{_selectedUnit.MaxMovementPoints:F1}\n" +
                           $"MORALE: {_selectedUnit.Morale}";

        // Inventory Update
        string invText = "INVENTORY:\n";
        if (_selectedUnit.Inventory.Count > 0)
        {
            foreach (var item in _selectedUnit.Inventory)
            {
                invText += $"- {item.itemName}\n";
            }
        }
        else
        {
            invText += "[EMPTY]";
        }
        _inventoryLabel.text = invText;

        // Orders Update
        string ordersText = "ACTIVE ORDERS:\n";
        if (_selectedUnit.ActiveOrders.Count > 0)
        {
            foreach (var order in _selectedUnit.ActiveOrders)
            {
                ordersText += $"- {order.orderName}\n";
            }
        }
        else
        {
            ordersText += "[NO ORDERS]";
        }
        _ordersLabel.text = ordersText;

        // Facility Building Logic
        if (_buildFacilityBtn != null)
        {
            bool isCitizen = _selectedUnit is Citizen;
            _buildFacilityBtn.SetActive(isCitizen);

            if (isCitizen)
            {
                bool onResource = _selectedUnit.CurrentTile != null && _selectedUnit.CurrentTile.TileResource != ResourceType.None;
                bool tileEmpty = _selectedUnit.CurrentTile != null && _selectedUnit.CurrentTile.OccupyingBuilding == null;
                
                Button btn = _buildFacilityBtn.GetComponent<Button>();
                Text btnText = _buildFacilityBtn.GetComponentInChildren<Text>();
                
                if (!onResource)
                {
                    btn.interactable = false;
                    btnText.text = "NO RESOURCE";
                    _buildFacilityBtn.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                }
                else if (!tileEmpty)
                {
                    btn.interactable = false;
                    btnText.text = "OCCUPIED";
                    _buildFacilityBtn.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                }
                else
                {
                    btn.interactable = true;
                    btnText.text = "BUILD FACILITY";
                    _buildFacilityBtn.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.4f, 1.0f); // Bright blue when valid
                }
            }
        }

        // Road Building Logic
        if (_buildRoadBtn != null)
        {
            bool isCitizen = _selectedUnit is Citizen;
            _buildRoadBtn.SetActive(isCitizen);

            if (isCitizen)
            {
                bool hasRoad = _selectedUnit.CurrentTile != null && _selectedUnit.CurrentTile.HasRoad;
                
                Button btn = _buildRoadBtn.GetComponent<Button>();
                Text btnText = _buildRoadBtn.GetComponentInChildren<Text>();
                
                if (hasRoad)
                {
                    btn.interactable = false;
                    btnText.text = "ROAD EXISTS";
                    _buildRoadBtn.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                }
                else
                {
                    btn.interactable = true;
                    btnText.text = "BUILD ROAD";
                    _buildRoadBtn.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 1.0f); // Slate grey when valid
                }
            }
        }
    }

    private void BuildFacility()
    {
        if (_selectedUnit == null || _selectedUnit.CurrentTile == null) return;
        
        if (_selectedUnit.CurrentTile.OccupyingBuilding != null)
        {
            Debug.LogWarning("[UnitMenuUI] Cannot build: Tile already has a building.");
            return;
        }

        GameObject facilityGO;
        
        if (facilityPrefab != null)
        {
            // Get the visual center of the tile
            SpriteRenderer tileRenderer = _selectedUnit.CurrentTile.GetComponent<SpriteRenderer>();
            Vector3 spawnPos = tileRenderer != null ? tileRenderer.bounds.center : _selectedUnit.CurrentTile.transform.position;
            spawnPos.z = -0.1f;
            
            facilityGO = Instantiate(facilityPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("[UnitMenuUI] Facility prefab is NULL. Spawning a fallback cube. Make sure you have a prefab named 'Facility' in a 'Resources' folder!");
            
            // Fallback: Create a simple cube so the user can see it's working
            facilityGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facilityGO.name = "FallbackFacility";
            facilityGO.transform.position = _selectedUnit.CurrentTile.transform.position + new Vector3(0, 0, -0.1f);
            facilityGO.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        }

        Facility facility = facilityGO.GetComponent<Facility>();
        if (facility == null) facility = facilityGO.AddComponent<Facility>();

        facility.AssignedTile = _selectedUnit.CurrentTile;
        PlayerOwnership.Inherit(facilityGO, _selectedUnit);
        _selectedUnit.CurrentTile.OccupyingBuilding = facilityGO;

        Debug.Log($"[UnitMenuUI] Citizen {_selectedUnit.name} successfully built a Facility on {_selectedUnit.CurrentTile.GridPosition}");
    }

    private void BuildRoad()
    {
        if (_selectedUnit is Citizen citizen)
        {
            citizen.BuildRoad();
        }
    }


    private void SlideIn()
    {
        _isOpen = true;
        _panel.gameObject.SetActive(true);
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(AnimatePanel(0f));
    }

    private void SlideOut()
    {
        _isOpen = false;
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(AnimatePanel(-_panel.rect.height, true));
    }

    private IEnumerator AnimatePanel(float targetY, bool deactivate = false)
    {
        float startY = _panel.anchoredPosition.y;
        float elapsed = 0;
        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / SlideDuration;
            _panel.anchoredPosition = new Vector2(0, Mathf.Lerp(startY, targetY, t));
            yield return null;
        }
        _panel.anchoredPosition = new Vector2(0, targetY);
        if (deactivate) _panel.gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        _panel.anchoredPosition = new Vector2(0, -_panel.rect.height);
        _panel.gameObject.SetActive(false);
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("UnitMenuCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = new GameObject("UnitPanel");
        panelGO.transform.SetParent(_canvas.transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        
        // Anchor to the BOTTOM
        _panel.anchorMin = new Vector2(0.5f, 0);
        _panel.anchorMax = new Vector2(0.5f, 0);
        _panel.pivot = new Vector2(0.5f, 0);
        _panel.sizeDelta = new Vector2(950, 200);
        
        panelGO.AddComponent<Image>().color = PanelBg;

        // Layout
        HorizontalLayoutGroup hlg = panelGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 15, 15);
        hlg.spacing = 30;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = true;

        // Sections
        _unitNameLabel = CreateLabel(panelGO, "UnitName", "UNIT", 22, HighlightColor, FontStyle.Bold);
        _unitNameLabel.alignment = TextAnchor.UpperLeft;
        
        _statsLabel = CreateLabel(panelGO, "StatsLabel", "Stats...", 14, TextWhite);
        _statsLabel.alignment = TextAnchor.UpperLeft;
        
        _inventoryLabel = CreateLabel(panelGO, "InventoryLabel", "Inventory...", 14, TextMuted);
        _inventoryLabel.alignment = TextAnchor.UpperLeft;
        
        _ordersLabel = CreateLabel(panelGO, "OrdersLabel", "Orders...", 14, TextMuted);
        _ordersLabel.alignment = TextAnchor.UpperLeft;

        _buildFacilityBtn = CreateButton(panelGO, "BUILD FACILITY", BuildFacility);
        _buildFacilityBtn.SetActive(false);

        _buildRoadBtn = CreateButton(panelGO, "BUILD ROAD", BuildRoad);
        _buildRoadBtn.SetActive(false);
    }


    private GameObject CreateButton(GameObject parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = new GameObject(label, typeof(RectTransform));
        btnGO.transform.SetParent(parent.transform, false);
        
        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.preferredHeight = 50;
        le.preferredWidth = 150;
        
        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 1f); // Slightly different blue for unit actions
        
        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(action);
        
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        
        Text t = textGO.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 14;
        t.color = TextWhite;
        t.alignment = TextAnchor.MiddleCenter;
        
        return btnGO;
    }

    private Text CreateLabel(GameObject parent, string name, string text, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft;
        
        // Add layout element to help the HorizontalLayoutGroup
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 150;
        
        return t;
    }

    public bool IsMouseOverMenu()
    {
        if (!_isOpen || _panel == null || Mouse.current == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(_panel, Mouse.current.position.ReadValue(), null);
    }
}
