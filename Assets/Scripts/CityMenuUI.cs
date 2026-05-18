using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// A menu that pops up when a building is selected, allowing for unit production or stats view.
/// </summary>
public class CityMenuUI : MonoBehaviour
{
    public static CityMenuUI Instance { get; private set; }
    
    [Header("Prefabs")]
    [SerializeField] private GameObject scoutPrefab;
    [SerializeField] private GameObject citizenPrefab;
    [SerializeField] private GameObject courierPrefab;
    [SerializeField] private GameObject facilityPrefab;
    [SerializeField] private GameObject marketPrefab;
    [SerializeField] private GameObject cityPrefab;
    [SerializeField] private GameObject constructionProjectPrefab;

    private Canvas _canvas;
    private RectTransform _panel;
    private Text _cityNameLabel;
    private Text _productionLabel;
    private Text _foodLabel;
    private Text _citizenLabel;
    private Text _queueLabel;
    private GameObject _produceScoutBtn;
    private GameObject _produceCitizenBtn;
    private GameObject _produceCourierBtn;
    private GameObject _queueFacilityBtn;
    private GameObject _queueMarketBtn;
    private GameObject _queueRoadBtn;
    private GameObject _queueCityBtn;
    
    private City _currentCity;
    private Facility _currentFacility;
    private bool _isOpen;
    private Coroutine _slideCoroutine;

    private const float SlideDuration = 0.2f;

    // Palette
    private static readonly Color PanelBg         = new Color(0.05f, 0.05f, 0.07f, 0.92f);
    private static readonly Color TextWhite        = new Color(0.95f, 0.95f, 1f, 1f);
    private static readonly Color TextMuted        = new Color(0.6f, 0.6f, 0.7f, 1f);
    private static readonly Color HighlightColor   = new Color(1f, 0.85f, 0.4f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<CityMenuUI>() != null) return;
        new GameObject("CityMenuUI").AddComponent<CityMenuUI>();
    }

    private void Awake()
    {
        Instance = this;
        if (scoutPrefab == null) scoutPrefab = Resources.Load<GameObject>("Scout");
        if (citizenPrefab == null) citizenPrefab = Resources.Load<GameObject>("Citizen");
        if (courierPrefab == null) courierPrefab = Resources.Load<GameObject>("Courier");
        if (facilityPrefab == null) facilityPrefab = Resources.Load<GameObject>("Facility");
        if (marketPrefab == null) marketPrefab = Resources.Load<GameObject>("Market");
        if (cityPrefab == null) cityPrefab = Resources.Load<GameObject>("City");
        if (constructionProjectPrefab == null) constructionProjectPrefab = Resources.Load<GameObject>("ConstructionProject");
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

        if (_isOpen)
        {
            if (_currentCity != null) UpdateCityUI();
            else if (_currentFacility != null) UpdateFacilityUI();
        }
    }

    private void UpdateCityUI()
    {
        _productionLabel.text = $"PRODUCTION: {Mathf.FloorToInt(_currentCity.ProductionStockpile)} / {_currentCity.MaxProductionStockpile}";
        _foodLabel.text = $"FOOD: {Mathf.FloorToInt(_currentCity.FoodStockpile)} / {Mathf.FloorToInt(_currentCity.FoodToNextCitizen)} (NEXT SLOT)";
        _citizenLabel.text = $"CITIZENS: {_currentCity.CurrentCitizens} / {_currentCity.CitizenCapacity}";
        _queueLabel.text = $"BUILD QUEUE: {_currentCity.QueuedBuildCount}  |  UNIT QUEUE: {_currentCity.QueuedUnitCount}  |  SPEND/TURN: {Mathf.FloorToInt(_currentCity.ProductionSpendThresholdPerTurn)}";
        
        SetProductionButtonsActive(true);

        if (_produceScoutBtn != null)
        {
            _produceScoutBtn.GetComponent<Button>().interactable = scoutPrefab != null;
            _produceScoutBtn.GetComponent<Image>().color = scoutPrefab != null ? ButtonNormal : new Color(0.1f, 0.1f, 0.1f, 0.5f);
        }

        if (_produceCitizenBtn != null)
        {
            bool hasCapacity = _currentCity.HasCitizenCapacity();
            Button btn = _produceCitizenBtn.GetComponent<Button>();
            bool canQueue = citizenPrefab != null && hasCapacity;
            btn.interactable = canQueue;
            _produceCitizenBtn.GetComponent<Image>().color = canQueue ? ButtonNormal : new Color(0.1f, 0.1f, 0.1f, 0.5f);
        }

        if (_produceCourierBtn != null)
        {
            _produceCourierBtn.GetComponent<Button>().interactable = courierPrefab != null;
            _produceCourierBtn.GetComponent<Image>().color = courierPrefab != null ? ButtonNormal : new Color(0.1f, 0.1f, 0.1f, 0.5f);
        }
    }

    private void UpdateFacilityUI()
    {
        _productionLabel.text = $"STOCKPILE: {Mathf.FloorToInt(_currentFacility.ProductionStockpile)} / {_currentFacility.MaxProductionStockpile}";
        _foodLabel.text = $"LINKED TO CITY: {(_currentFacility.IsConnectedToCity ? "YES" : "NO")}";
        _citizenLabel.text = "";
        _queueLabel.text = "";
        SetProductionButtonsActive(false);
    }

    private void SetProductionButtonsActive(bool active)
    {
        if (_produceScoutBtn != null) _produceScoutBtn.SetActive(active);
        if (_produceCitizenBtn != null) _produceCitizenBtn.SetActive(active);
        if (_produceCourierBtn != null) _produceCourierBtn.SetActive(active);
        if (_queueFacilityBtn != null) _queueFacilityBtn.SetActive(active);
        if (_queueMarketBtn != null) _queueMarketBtn.SetActive(active);
        if (_queueRoadBtn != null) _queueRoadBtn.SetActive(active);
        if (_queueCityBtn != null) _queueCityBtn.SetActive(active);
    }

    private void HandleTileSelected(GridTile tile)
    {
        if (tile != null && tile.OccupyingBuilding != null)
        {
            _currentCity = tile.OccupyingBuilding.GetComponent<City>();
            _currentFacility = tile.OccupyingBuilding.GetComponent<Facility>();

            if (_currentCity != null)
            {
                if (_currentCity.AssignedTile == null) _currentCity.AssignedTile = tile;
                _cityNameLabel.text = "CITY"; 
                if (!_isOpen) SlideIn();
                return;
            }
            else if (_currentFacility != null)
            {
                if (_currentFacility.AssignedTile == null) _currentFacility.AssignedTile = tile;
                _cityNameLabel.text = "FACILITY"; 
                if (!_isOpen) SlideIn();
                return;
            }
        }
        
        _currentCity = null;
        _currentFacility = null;
        if (_isOpen) SlideOut();
    }

    private void ProduceScout() => ProduceUnit(scoutPrefab, 20, "Scout", true);
    private void ProduceCitizen()
    {
        if (_currentCity != null && !_currentCity.HasCitizenCapacity()) return;
        ProduceUnit(citizenPrefab, 10, "Citizen", true);
    }
    private void ProduceCourier() => ProduceUnit(courierPrefab, 15, "Courier", true);
    private void QueueFacility()
    {
        if (_currentCity == null || facilityPrefab == null || constructionProjectPrefab == null) return;
        City city = _currentCity;

        PlacementManager.Instance.StartPlacement(facilityPrefab, (tile) =>
        {
            if (GameplayCommandService.TryQueueCityConstruction(city, tile, constructionProjectPrefab, facilityPrefab))
            {
                Debug.Log($"[CityMenuUI] Queued facility at {tile.GridPosition} from {city.name}");
            }
        });
        SlideOut();
    }

    private void QueueRoad()
    {
        if (_currentCity == null) return;
        City city = _currentCity;

        PlacementManager.Instance.StartPlacement(null, (tile) =>
        {
            if (GameplayCommandService.TryQueueCityRoad(city, tile))
            {
                Debug.Log($"[CityMenuUI] Queued road at {tile.GridPosition} from {city.name}");
            }
        });
        SlideOut();
    }

    private void QueueMarket()
    {
        if (_currentCity == null || marketPrefab == null || constructionProjectPrefab == null) return;
        City city = _currentCity;

        PlacementManager.Instance.StartPlacement(marketPrefab, (tile) =>
        {
            if (GameplayCommandService.TryQueueCityConstruction(city, tile, constructionProjectPrefab, marketPrefab))
            {
                Debug.Log($"[CityMenuUI] Queued market at {tile.GridPosition} from {city.name}");
            }
        });
        SlideOut();
    }

    private void QueueFoundCity()
    {
        if (_currentCity == null || cityPrefab == null) return;
        City city = _currentCity;

        GameObject constructionPrefab = constructionProjectPrefab != null ? constructionProjectPrefab : cityPrefab;
        PlacementManager.Instance.StartPlacement(cityPrefab, (tile) =>
        {
            if (GameplayCommandService.TryQueueCityConstruction(city, tile, constructionPrefab, cityPrefab))
            {
                Debug.Log($"[CityMenuUI] Queued city foundation at {tile.GridPosition} from {city.name}");
            }
        });
        SlideOut();
    }

    private void ProduceUnit(GameObject prefab, float cost, string unitName, bool isProduction)
    {
        GameplayCommandService.TryProduceUnit(_currentCity, prefab, cost, unitName, isProduction);
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
        _slideCoroutine = StartCoroutine(AnimatePanel(-_panel.rect.width, true));
    }

    private IEnumerator AnimatePanel(float targetX, bool deactivate = false)
    {
        float startX = _panel.anchoredPosition.x;
        float elapsed = 0;
        while (elapsed < SlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / SlideDuration;
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, t), 0);
            yield return null;
        }
        _panel.anchoredPosition = new Vector2(targetX, 0);
        if (deactivate) _panel.gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        if (_panel != null)
        {
            _panel.anchoredPosition = new Vector2(-_panel.rect.width, 0);
            _panel.gameObject.SetActive(false);
        }
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("CityMenuCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 101;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = new GameObject("CityPanel");
        panelGO.transform.SetParent(_canvas.transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0, 0);
        _panel.anchorMax = new Vector2(0, 1);
        _panel.pivot = new Vector2(0, 0.5f);
        _panel.sizeDelta = new Vector2(300, 0);
        panelGO.AddComponent<Image>().color = PanelBg;

        GameObject scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(panelGO.transform, false);
        RectTransform scrollRt = scrollGO.AddComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.sizeDelta = Vector2.zero;
        scrollGO.AddComponent<Mask>();
        scrollGO.AddComponent<Image>().color = new Color(0,0,0,0.01f);
        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        RectTransform contentRt = contentGO.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.sizeDelta = new Vector2(0, 0);
        sr.content = contentRt;

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 40, 40);
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _cityNameLabel = CreateLabel(contentGO, "CityName", "CITY", 28, HighlightColor, FontStyle.Bold);
        _productionLabel = CreateLabel(contentGO, "ProductionLabel", "PRODUCTION", 18, TextWhite);
        _foodLabel = CreateLabel(contentGO, "FoodLabel", "FOOD", 18, TextWhite);
        _citizenLabel = CreateLabel(contentGO, "CitizenLabel", "CITIZENS", 16, TextMuted);
        _queueLabel = CreateLabel(contentGO, "QueueLabel", "BUILD QUEUE: 0", 16, TextMuted);
        
        _produceScoutBtn = CreateButton(contentGO, "PRODUCE SCOUT (20)", ProduceScout);
        _produceCitizenBtn = CreateButton(contentGO, "PRODUCE CITIZEN (10)", ProduceCitizen);
        _produceCourierBtn = CreateButton(contentGO, "PRODUCE COURIER (15)", ProduceCourier);
        _queueFacilityBtn = CreateButton(contentGO, "QUEUE FACILITY", QueueFacility);
        _queueMarketBtn = CreateButton(contentGO, "QUEUE MARKET", QueueMarket);
        _queueRoadBtn = CreateButton(contentGO, "QUEUE ROAD", QueueRoad);
        _queueCityBtn = CreateButton(contentGO, "QUEUE CITY", QueueFoundCity);
    }

    private static readonly Color ButtonNormal     = new Color(0.2f, 0.2f, 0.25f, 1f);
    private static readonly Color ButtonHighlight  = new Color(0.3f, 0.3f, 0.4f, 1f);

    private GameObject CreateButton(GameObject parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = new GameObject(label, typeof(RectTransform));
        btnGO.transform.SetParent(parent.transform, false);
        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.preferredHeight = 50;
        le.flexibleHeight = 0;
        btnGO.AddComponent<Image>().color = ButtonNormal;
        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(action);
        ColorBlock cb = btn.colors;
        cb.normalColor = ButtonNormal;
        cb.highlightedColor = ButtonHighlight;
        cb.pressedColor = Color.black;
        btn.colors = cb;
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        Text t = textGO.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 16;
        t.color = TextWhite;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 11;
        t.resizeTextMaxSize = 16;
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
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = Mathf.Max(10, size - 6);
        t.resizeTextMaxSize = size;

        return t;
    }

    public bool IsMouseOverMenu()
    {
        if (!_isOpen || _panel == null || Mouse.current == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(_panel, Mouse.current.position.ReadValue(), null);
    }
}
