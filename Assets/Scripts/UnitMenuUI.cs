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
    [SerializeField] private GameObject cityPrefab;
    [SerializeField] private GameObject constructionProjectPrefab;

    private Canvas _canvas;
    private RectTransform _panel;
    private Text _unitNameLabel;
    private Text _statsLabel;
    private Text _inventoryLabel;
    private Text _ordersLabel;
    private GameObject _buildFacilityBtn;
    private GameObject _buildRoadBtn;
    private GameObject _foundCityBtn;
    private GameObject _chooseDestinationBtn;
    private GameObject _toggleRepeatBtn;
    private GameObject _toggleOrderBtn;
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
        if (cityPrefab == null) cityPrefab = Resources.Load<GameObject>("City");
        if (constructionProjectPrefab == null) constructionProjectPrefab = Resources.Load<GameObject>("ConstructionProject");
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
            
            bool isCourier = _selectedUnit is Courier;
            bool isMilitary = _selectedUnit.IsMilitary;
            Debug.Log($"[UnitMenuUI] Selected unit: {_selectedUnit.name}, type={_selectedUnit.GetType().Name}, isCourier={isCourier}, tile={tile.GridPosition}");
            SetActionButtonVisible(_buildFacilityBtn, false);
            SetActionButtonVisible(_buildRoadBtn, false);
            SetActionButtonVisible(_foundCityBtn, false);
            if (_chooseDestinationBtn != null)
            {
                SetActionButtonVisible(_chooseDestinationBtn, isCourier);
                Debug.Log($"[UnitMenuUI] Choose Destination button active={_chooseDestinationBtn.activeSelf}");
            }
            SetActionButtonVisible(_toggleRepeatBtn, isCourier);
            SetActionButtonVisible(_toggleOrderBtn, isMilitary);
            RefreshCourierButtons();
            RefreshOrderButton();

            if (!_isOpen) SlideIn();
        }
        else
        {
            _selectedUnit = null;
            if (_isOpen) SlideOut();
        }
    }

    private void RefreshCourierButtons()
    {
        if (!(_selectedUnit is Courier courier)) return;

        SetButtonLabel(_toggleRepeatBtn, courier.RepeatEnabled ? "REPEAT: ON" : "REPEAT: OFF");
    }

    private void RefreshOrderButton()
    {
        if (_selectedUnit == null || !_selectedUnit.IsMilitary) return;

        SetButtonLabel(_toggleOrderBtn, $"ORDER: {_selectedUnit.CurrentRoamOrderLabel.ToUpper()}");
    }

    private void SetActionButtonVisible(GameObject button, bool visible)
    {
        if (button == null) return;

        button.SetActive(visible);
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.ignoreLayout = !visible;
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

        string ordersText = _selectedUnit.IsMilitary
            ? $"ROAM ORDER: {_selectedUnit.CurrentRoamOrderLabel.ToUpper()}"
            : "ORDERS: AUTOMATED";
        if (_selectedUnit.ActiveOrders.Count > 0)
            ordersText += $"  |  QUEUED: {string.Join(", ", _selectedUnit.ActiveOrders.Select(o => o.orderName))}";
        _ordersLabel.text = ordersText;
    }

    private void BuildFacility()
    {
        if (_selectedUnit is Citizen citizen && facilityPrefab != null)
        {
            PlacementManager.Instance.StartPlacement(facilityPrefab, (tile) => {
                if (GameplayCommandService.TryQueueConstruction(citizen, tile, constructionProjectPrefab, facilityPrefab))
                {
                    Debug.Log($"[UnitMenuUI] Construction order started for {citizen.name} at {tile.GridPosition}");
                }
            });
            SlideOut();
        }
    }

    private void BuildRoad()
    {
        if (_selectedUnit is Citizen citizen)
        {
            PlacementManager.Instance.StartPlacement(null, (tile) => {
                if (GameplayCommandService.TryQueueRoad(citizen, tile))
                {
                    Debug.Log($"[UnitMenuUI] Road construction order started for {citizen.name} at {tile.GridPosition}");
                }
            });
            SlideOut();
        }
    }

    private void FoundCity()
    {
        GameObject prefabToPlace = constructionProjectPrefab != null ? constructionProjectPrefab : cityPrefab;

        if (_selectedUnit is Citizen citizen && prefabToPlace != null)
        {
            PlacementManager.Instance.StartPlacement(cityPrefab, (tile) => {
                if (GameplayCommandService.TryQueueConstruction(citizen, tile, prefabToPlace, cityPrefab))
                {
                    Debug.Log($"[UnitMenuUI] City foundation order started for {citizen.name} at {tile.GridPosition}");
                }
            });
            SlideOut();
        }
    }

    private void ToggleCourierRepeat()
    {
        if (_selectedUnit is not Courier courier) return;

        courier.ToggleRepeat();
        RefreshCourierButtons();
    }

    private void ToggleUnitOrder()
    {
        if (_selectedUnit == null || !_selectedUnit.IsMilitary) return;

        _selectedUnit.CycleRoamOrderMode();
        RefreshOrderButton();
        UpdateStatsUI();
    }

    private void ToggleDestinationMenu()
    {
        Debug.Log($"[UnitMenuUI] Choose Destination clicked. selected={(_selectedUnit != null ? _selectedUnit.name : "NULL")}, type={(_selectedUnit != null ? _selectedUnit.GetType().Name : "NULL")}");
        if (_destinationPanel == null)
        {
            Debug.LogWarning("[UnitMenuUI] Cannot open destination menu because _destinationPanel is null.");
            return;
        }
        bool show = !_destinationPanel.activeSelf;
        Debug.Log($"[UnitMenuUI] Destination panel show={show}");
        _destinationPanel.SetActive(show);
        if (show) PopulateDestinationMenu();
    }

    private void PopulateDestinationMenu()
    {
        if (_destinationContent == null)
        {
            Debug.LogWarning("[UnitMenuUI] _destinationContent is null. Trying to recover it from DestinationPanel.");
            RecoverDestinationContent();
        }

        if (_destinationContent == null)
        {
            Debug.LogError("[UnitMenuUI] Cannot populate destination menu because _destinationContent is still null.");
            return;
        }

        foreach (Transform child in _destinationContent) Destroy(child.gameObject);

        Debug.Log("[UnitMenuUI] Populating courier destination menu...");

        List<IGridTarget> destinations = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<IGridTarget>()
            .Where(t => !(t is Unit))
            .ToList();

        Debug.Log($"[UnitMenuUI] IGridTarget scene scan found {destinations.Count} non-unit target(s).");

        if (AssetRegistry.Instance != null)
        {
            Debug.Log($"[UnitMenuUI] AssetRegistry has {AssetRegistry.Instance.ConstructionProjects.Count} construction project(s).");
            destinations.AddRange(AssetRegistry.Instance.ConstructionProjects);
        }
        else
        {
            Debug.LogWarning("[UnitMenuUI] AssetRegistry.Instance is null while populating destinations.");
        }

        ConstructionProject[] sceneProjects = FindObjectsByType<ConstructionProject>(FindObjectsSortMode.None);
        Debug.Log($"[UnitMenuUI] Direct ConstructionProject scan found {sceneProjects.Length} project(s).");
        foreach (ConstructionProject project in sceneProjects)
        {
            Debug.Log($"[UnitMenuUI] Project candidate: name={project.name}, assignedTile={(project.AssignedTile != null ? project.AssignedTile.GridPosition.ToString() : "NULL")}, position={project.transform.position}");
        }
        destinations.AddRange(sceneProjects);

        int tileBuildingTargets = 0;
        foreach (GridTile tile in FindObjectsByType<GridTile>(FindObjectsSortMode.None))
        {
            IGridTarget buildingTarget = tile.OccupyingBuilding != null
                ? tile.OccupyingBuilding.GetComponents<MonoBehaviour>().OfType<IGridTarget>().FirstOrDefault()
                : null;

            if (buildingTarget != null)
            {
                tileBuildingTargets++;
                Debug.Log($"[UnitMenuUI] Tile target candidate: {buildingTarget.GetType().Name} at {tile.GridPosition}, object={tile.OccupyingBuilding.name}");
                destinations.Add(buildingTarget);
            }
        }

        Debug.Log($"[UnitMenuUI] Tile OccupyingBuilding scan found {tileBuildingTargets} target(s). Candidate total before filtering: {destinations.Count}.");

        destinations = destinations
            .Where(IsValidDestinationCandidate)
            .Distinct()
            .ToList();

        Debug.Log($"[UnitMenuUI] Destination count after filtering/distinct: {destinations.Count}.");

        if (destinations.Count == 0)
        {
            CreateLabel(_destinationContent.gameObject, "NoDest", "NO DESTINATIONS FOUND", 14, TextMuted);
            return;
        }

        foreach (var dest in destinations)
        {
            GridTile tile = GetDestinationTile(dest);
            if (tile == null) continue;

            string label = $"{GetDestinationLabel(dest)} AT ({tile.GridPosition.x}, {tile.GridPosition.y})";
            CreateButton(_destinationContent.gameObject, label, () => {
                if (_selectedUnit is Courier courier)
                {
                    if (GameplayCommandService.TryAssignCourierDestination(courier, dest))
                    {
                        Debug.Log($"[UnitMenuUI] Destination set for {courier.name}");
                    }
                }
                _destinationPanel.SetActive(false);
            });
        }
    }

    private void RecoverDestinationContent()
    {
        if (_destinationPanel == null) return;

        Transform scrollArea = _destinationPanel.transform.Find("ScrollArea");
        Transform content = scrollArea != null ? scrollArea.Find("Content") : null;
        if (content != null)
        {
            _destinationContent = content;
            Debug.Log("[UnitMenuUI] Recovered destination content transform.");
        }
    }

    private bool IsValidDestinationCandidate(IGridTarget destination)
    {
        if (destination == null)
        {
            Debug.LogWarning("[UnitMenuUI] Rejected destination: null target.");
            return false;
        }

        if (destination is Unit)
        {
            Debug.Log($"[UnitMenuUI] Rejected destination: {destination.GetType().Name} is a unit.");
            return false;
        }

        GridTile tile = GetDestinationTile(destination);
        if (tile == null)
        {
            MonoBehaviour behaviour = destination as MonoBehaviour;
            string objectName = behaviour != null ? behaviour.name : destination.GetType().Name;
            Debug.LogWarning($"[UnitMenuUI] Rejected destination: {destination.GetType().Name} '{objectName}' has no resolvable tile.");
            return false;
        }

        Debug.Log($"[UnitMenuUI] Accepted destination: {GetDestinationLabel(destination)} at {tile.GridPosition}.");
        return true;
    }

    private GridTile GetDestinationTile(IGridTarget destination)
    {
        if (destination == null) return null;
        if (destination.AssignedTile != null) return destination.AssignedTile;

        MonoBehaviour behaviour = destination as MonoBehaviour;
        if (behaviour == null) return null;

        foreach (GridTile tile in FindObjectsByType<GridTile>(FindObjectsSortMode.None))
        {
            if (tile.OccupyingBuilding == behaviour.gameObject)
            {
                if (destination is ConstructionProject project)
                {
                    project.AssignedTile = tile;
                }

                return tile;
            }
        }

        if (destination is ConstructionProject constructionProject)
        {
            GridTile nearestTile = FindNearestTile(behaviour.transform.position);
            if (nearestTile != null)
            {
                constructionProject.AssignedTile = nearestTile;
                if (nearestTile.OccupyingBuilding == null)
                {
                    nearestTile.OccupyingBuilding = behaviour.gameObject;
                }
            }

            return nearestTile;
        }

        return null;
    }

    private GridTile FindNearestTile(Vector3 worldPosition)
    {
        GridTile nearestTile = null;
        float nearestDistance = float.MaxValue;

        foreach (GridTile tile in FindObjectsByType<GridTile>(FindObjectsSortMode.None))
        {
            float distance = Vector2.SqrMagnitude((Vector2)(tile.GetVisualCenter() - worldPosition));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTile = tile;
            }
        }

        return nearestTile;
    }

    private string GetDestinationLabel(IGridTarget destination)
    {
        if (destination is ConstructionProject) return "CONSTRUCTION PROJECT";
        return destination.GetType().Name.ToUpper();
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
        _foundCityBtn = CreateButton(panelGO, "FOUND CITY", FoundCity);
        _chooseDestinationBtn = CreateButton(panelGO, "CHOOSE DESTINATION", ToggleDestinationMenu);
        _toggleRepeatBtn = CreateButton(panelGO, "REPEAT: OFF", ToggleCourierRepeat);
        _toggleOrderBtn = CreateButton(panelGO, "ORDER: FREE ROAM", ToggleUnitOrder);

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
        btn.onClick.AddListener(() => {
            Debug.Log($"[UnitMenuUI] Button clicked: {label}");
            action.Invoke();
        });
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
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = 14;
        
        return btnGO;
    }

    private void SetButtonLabel(GameObject button, string label)
    {
        if (button == null) return;

        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = label;
        }

        button.name = label;
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
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = Mathf.Max(10, size - 6);
        t.resizeTextMaxSize = size;

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
