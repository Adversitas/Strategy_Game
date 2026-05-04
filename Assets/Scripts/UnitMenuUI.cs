using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private GameObject _chooseDestinationBtn;
    private GameObject _destinationPanel;
    private Transform _destinationContent;
    
    private Unit _selectedUnit;
    private bool _isOpen;
    private Coroutine _slideCoroutine;

    private const float SlideDuration = 0.2f;

    // Palette
    private static readonly Color PanelBg         = new Color(0.05f, 0.05f, 0.05f, 0.9f);
    private static readonly Color TextWhite        = new Color(0.95f, 0.95f, 1f, 1f);
    private static readonly Color TextMuted        = new Color(0.6f, 0.6f, 0.7f, 1f);
    private static readonly Color HighlightColor   = new Color(1f, 0.85f, 0.4f, 1f);
    private static readonly Color ButtonNormal     = new Color(0.2f, 0.2f, 0.25f, 1f);

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
        _isOpen = false;
        HideImmediate();
    }

    private void OnEnable()
    {
        SquareGridGenerator.OnTileSelected += UpdateSelection;
    }

    private void OnDisable()
    {
        SquareGridGenerator.OnTileSelected -= UpdateSelection;
    }

    private void Update()
    {
        if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SlideOut();

        if (_isOpen && _selectedUnit != null)
        {
            UpdateStatsUI();
        }
    }

    private void UpdateSelection(GridTile tile)
    {
        if (tile != null && tile.OccupyingUnit != null)
        {
            _selectedUnit = tile.OccupyingUnit;
            _unitNameLabel.text = _selectedUnit.name.ToUpper();
            UpdateStatsUI();
            
            bool isCitizen = _selectedUnit is Citizen;
            if (_buildFacilityBtn != null) _buildFacilityBtn.SetActive(isCitizen);
            if (_buildRoadBtn != null) _buildRoadBtn.SetActive(isCitizen);
            if (_chooseDestinationBtn != null) _chooseDestinationBtn.SetActive(_selectedUnit is Courier);

            if (!_isOpen) SlideIn();
        }
        else
        {
            _selectedUnit = null;
            if (_isOpen) SlideOut();
        }
    }

    private void UpdateStatsUI()
    {
        if (_selectedUnit == null) return;

        string stats = $"HP: <color=#ff5555>{_selectedUnit.CurrentHealth}/{_selectedUnit.MaxHealth}</color>  |  " +
                       $"MP: <color=#55ff55>{_selectedUnit.CurrentMovementPoints:F1}/{_selectedUnit.MaxMovementPoints:F0}</color>  |  " +
                       $"STR: {_selectedUnit.Strength}  DEX: {_selectedUnit.Dexterity}";
        _statsLabel.text = stats;

        string invText = "INVENTORY: ";
        if (_selectedUnit.Inventory.Count > 0)
            invText += string.Join(", ", _selectedUnit.Inventory.Select(i => i.itemName));
        else
            invText += "EMPTY";
        _inventoryLabel.text = invText;

        string ordersText = "ACTIVE ORDERS: ";
        if (_selectedUnit.ActiveOrders.Count > 0)
            ordersText += string.Join(", ", _selectedUnit.ActiveOrders.Select(o => o.orderName));
        else
            ordersText += "NONE";
        _ordersLabel.text = ordersText;
    }

    private void BuildFacility()
    {
        if (_selectedUnit is Citizen citizen && facilityPrefab != null)
        {
            PlacementManager.Instance.StartPlacement(facilityPrefab, (tile) => {
                citizen.StartConstruction(tile, facilityPrefab);
                Debug.Log($"[UnitMenuUI] Construction order started for {citizen.name} at {tile.GridPosition}");
            });
            SlideOut();
        }
    }

    private void BuildRoad()
    {
        if (_selectedUnit is Citizen citizen)
        {
            PlacementManager.Instance.StartPlacement(null, (tile) => {
                tile.IsPendingRoad = true;
                citizen.StartConstruction(tile, null);
                Debug.Log($"[UnitMenuUI] Road construction order started for {citizen.name} at {tile.GridPosition}");
            });
            SlideOut();
        }
    }

    private void ToggleDestinationMenu()
    {
        if (_destinationPanel == null) return;
        bool show = !_destinationPanel.activeSelf;
        _destinationPanel.SetActive(show);
        if (show) PopulateDestinationMenu();
    }

    private void PopulateDestinationMenu()
    {
        if (_destinationContent == null) return;
        foreach (Transform child in _destinationContent) Destroy(child.gameObject);

        IGridTarget[] destinations = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<IGridTarget>()
            .Where(t => !(t is Unit))
            .ToArray();

        if (destinations.Length == 0)
        {
            CreateLabel(_destinationContent.gameObject, "NoDest", "NO DESTINATIONS FOUND", 14, TextMuted);
            return;
        }

        foreach (var dest in destinations)
        {
            GridTile tile = dest.AssignedTile;
            if (tile == null) continue;

            string label = $"{dest.GetType().Name.ToUpper()} AT ({tile.GridPosition.x}, {tile.GridPosition.y})";
            CreateButton(_destinationContent.gameObject, label, () => {
                if (_selectedUnit is Courier courier)
                {
                    courier.TargetDestination = dest;
                    Debug.Log($"[UnitMenuUI] Destination set for {courier.name}");
                }
                _destinationPanel.SetActive(false);
            });
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
        if (_destinationPanel != null) _destinationPanel.SetActive(false);
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(AnimatePanel(-200f, true));
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
        if (_panel != null)
        {
            _panel.anchoredPosition = new Vector2(0, -200);
            _panel.gameObject.SetActive(false);
        }
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("UnitMenuCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = new GameObject("UnitPanel");
        panelGO.transform.SetParent(_canvas.transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0, 0);
        _panel.anchorMax = new Vector2(1, 0);
        _panel.pivot = new Vector2(0.5f, 0);
        _panel.sizeDelta = new Vector2(-100, 180);
        _panel.anchoredPosition = Vector2.zero;
        panelGO.AddComponent<Image>().color = PanelBg;

        HorizontalLayoutGroup hlg = panelGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 15, 15);
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childForceExpandWidth = false;

        _unitNameLabel = CreateLabel(panelGO, "UnitName", "UNIT", 26, HighlightColor, FontStyle.Bold);
        _statsLabel = CreateLabel(panelGO, "Stats", "STATS", 18, TextWhite);
        _inventoryLabel = CreateLabel(panelGO, "Inventory", "INV", 16, TextWhite);
        _ordersLabel = CreateLabel(panelGO, "Orders", "ORDERS", 16, TextWhite);

        _buildFacilityBtn = CreateButton(panelGO, "BUILD FACILITY", BuildFacility);
        _buildRoadBtn = CreateButton(panelGO, "BUILD ROAD", BuildRoad);
        _chooseDestinationBtn = CreateButton(panelGO, "CHOOSE DESTINATION", ToggleDestinationMenu);

        // Destination Panel
        GameObject destPanelGO = new GameObject("DestinationPanel");
        destPanelGO.transform.SetParent(_canvas.transform, false);
        _destinationPanel = destPanelGO;
        RectTransform destRt = destPanelGO.AddComponent<RectTransform>();
        destRt.anchorMin = destRt.anchorMax = new Vector2(0.5f, 0);
        destRt.pivot = new Vector2(0.5f, 0);
        destRt.anchoredPosition = new Vector2(0, 210);
        destRt.sizeDelta = new Vector2(400, 300);
        destPanelGO.AddComponent<Image>().color = PanelBg;

        GameObject scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(destPanelGO.transform, false);
        RectTransform scrollRt = scrollGO.AddComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one; scrollRt.sizeDelta = new Vector2(-20, -20);
        scrollGO.AddComponent<Mask>();
        scrollGO.AddComponent<Image>().color = new Color(0,0,0,0.01f);
        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        _destinationContent = contentGO.transform;
        RectTransform contentRt = contentGO.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1); contentRt.sizeDelta = Vector2.zero;
        sr.content = contentRt;

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 5;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _destinationPanel.SetActive(false);
    }

    private GameObject CreateButton(GameObject parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = new GameObject(label, typeof(RectTransform));
        btnGO.transform.SetParent(parent.transform, false);
        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.preferredHeight = 50; le.preferredWidth = 150;
        btnGO.AddComponent<Image>().color = ButtonNormal;
        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(action);
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        Text t = textGO.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 14 * 5; // Crisp Text Hack
        t.color = TextWhite;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        textGO.transform.localScale = Vector3.one * 0.2f;
        
        return btnGO;
    }

    private Text CreateLabel(GameObject parent, string name, string text, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        
        // Crisp Text Hack
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size * 5;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        go.transform.localScale = Vector3.one * 0.2f;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 200;
        return t;
    }

    public bool IsMouseOverMenu()
    {
        if (!_isOpen || _panel == null || Mouse.current == null) return false;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        bool overPanel = RectTransformUtility.RectangleContainsScreenPoint(_panel, mousePos, null);
        bool overDest = _destinationPanel != null && _destinationPanel.activeSelf && 
                       RectTransformUtility.RectangleContainsScreenPoint(_destinationPanel.GetComponent<RectTransform>(), mousePos, null);
        return overPanel || overDest;
    }
}
