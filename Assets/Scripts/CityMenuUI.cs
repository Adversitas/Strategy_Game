using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// A menu that pops up when a city is selected, allowing for unit/building production.
/// </summary>
public class CityMenuUI : MonoBehaviour
{
    public static CityMenuUI Instance { get; private set; }
    
    [Header("Prefabs")]
    [SerializeField] private GameObject scoutPrefab;
    [SerializeField] private GameObject citizenPrefab;

    private Canvas _canvas;
    private RectTransform _panel;
    private Text _cityNameLabel;
    private Text _productionLabel;
    private Text _foodLabel;
    private Text _citizenLabel;
    private GameObject _produceScoutBtn;
    private GameObject _produceCitizenBtn;
    private City _currentCity;
    private bool _isOpen;
    private Coroutine _slideCoroutine;

    private const float SlideDuration = 0.2f;

    // Palette (slightly different from Build Menu to distinguish)
    private static readonly Color PanelBg = new Color(0.15f, 0.15f, 0.2f, 0.95f); // Deep blue-ish
    private static readonly Color TextWhite = new Color(0.9f, 0.9f, 1f, 1f);
    private static readonly Color TextMuted = new Color(0.6f, 0.6f, 0.7f, 1f);

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

        if (_isOpen && _currentCity != null)
        {
            _productionLabel.text = $"PRODUCTION: {Mathf.FloorToInt(_currentCity.ProductionStockpile)} / {_currentCity.MaxProductionStockpile}";
            _foodLabel.text = $"FOOD: {Mathf.FloorToInt(_currentCity.FoodStockpile)} / {Mathf.FloorToInt(_currentCity.FoodToNextCitizen)} (NEXT SLOT)";
            _citizenLabel.text = $"CITIZENS: {_currentCity.CurrentCitizens} / {_currentCity.CitizenCapacity}";
            
            // Grey out production if not enough resources
            if (_produceScoutBtn != null)
            {
                bool canAfford = _currentCity.CanAffordProduction(20);
                Button btn = _produceScoutBtn.GetComponent<Button>();
                btn.interactable = canAfford;
                _produceScoutBtn.GetComponent<Image>().color = canAfford ? ButtonNormal : new Color(0.1f, 0.1f, 0.1f, 0.5f);
            }

            if (_produceCitizenBtn != null)
            {
                // Citizens now cost 10 Production and require capacity
                bool canAfford = _currentCity.CanAffordProduction(10);
                bool hasCapacity = _currentCity.HasCitizenCapacity();
                Button btn = _produceCitizenBtn.GetComponent<Button>();
                btn.interactable = canAfford && hasCapacity;
                _produceCitizenBtn.GetComponent<Image>().color = (canAfford && hasCapacity) ? ButtonNormal : new Color(0.1f, 0.1f, 0.1f, 0.5f);
                
                // Update text to show status
                Text btnText = _produceCitizenBtn.GetComponentInChildren<Text>();
                if (!hasCapacity) btnText.text = "CITIZEN (WAIT FOR FOOD)";
                else btnText.text = "PRODUCE CITIZEN (10 PROD)";
            }
        }
    }

    private void HandleTileSelected(GridTile tile)
    {
        // Only cities open this menu; facilities, markets and construction sites have no City component.
        City city = tile != null && tile.HasBuilding ? tile.OccupyingBuilding.GetComponent<City>() : null;
        if (city != null)
        {
            _currentCity = city;
            if (_currentCity.AssignedTile == null)
            {
                _currentCity.AssignedTile = tile;
            }
            _cityNameLabel.text = "CITY"; // We could pull a name from the building later
            if (!_isOpen) SlideIn();
        }
        else
        {
            _currentCity = null;
            if (_isOpen) SlideOut();
        }
    }

    private void ProduceScout()
    {
        // Scouts cost 20 Production
        ProduceUnit(scoutPrefab, 20, "Scout", true);
    }

    private void ProduceCitizen()
    {
        // Citizens cost 10 Production and count towards capacity
        if (_currentCity != null && !_currentCity.HasCitizenCapacity())
        {
            Debug.LogWarning("Cannot produce Citizen: No available capacity slots. Wait for Food growth.");
            return;
        }
        ProduceUnit(citizenPrefab, 10, "Citizen", true);
    }

    private void ProduceUnit(GameObject prefab, float cost, string unitName, bool isProduction)
    {
        if (_currentCity == null || prefab == null) return;

        bool canAfford = isProduction ? _currentCity.CanAffordProduction(cost) : _currentCity.CanAffordFood(cost);
        Debug.Log($"[CityMenuUI] Produce {unitName} clicked. City: {_currentCity.name}, Prefab: {prefab.name}, CanAfford: {canAfford}");
        if (!canAfford) return;

        // Go through the city's production queue so the unit inherits the city's owner,
        // gets its home city set, and spawns on a free neighbouring tile.
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
        _panel.anchoredPosition = new Vector2(-_panel.rect.width, 0);
        _panel.gameObject.SetActive(false);
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("CityMenuCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 101; // Just above build menu

        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = new GameObject("CityPanel");
        panelGO.transform.SetParent(_canvas.transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        
        // Anchor to the LEFT side
        _panel.anchorMin = new Vector2(0, 0);
        _panel.anchorMax = new Vector2(0, 1);
        _panel.pivot = new Vector2(0, 0.5f);
        _panel.sizeDelta = new Vector2(350, 0);
        
        panelGO.AddComponent<Image>().color = PanelBg;

        VerticalLayoutGroup vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 40, 40);
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandHeight = false;

        _cityNameLabel = CreateLabel(panelGO, "CityName", "CITY", 24, TextWhite, FontStyle.Bold);
        _productionLabel = CreateLabel(panelGO, "ProductionLabel", "PRODUCTION: 0 / 500", 16, TextWhite);
        _foodLabel = CreateLabel(panelGO, "FoodLabel", "FOOD: 0 / 500", 16, TextWhite);
        _citizenLabel = CreateLabel(panelGO, "CitizenLabel", "CITIZENS: 0 / 5", 14, TextMuted);
        CreateLabel(panelGO, "ProductionHeader", "PRODUCTION", 14, TextMuted);

        _produceScoutBtn = CreateButton(panelGO, "PRODUCE SCOUT (20)", ProduceScout);
        _produceCitizenBtn = CreateButton(panelGO, "PRODUCE CITIZEN (10)", ProduceCitizen);

        // Placeholder for list removed
    }

    // Palette copied from BuildMenuUI for consistency
    private static readonly Color ButtonNormal = new Color(0.15f, 0.15f, 0.15f, 1f);
    private static readonly Color ButtonHighlight = new Color(0.25f, 0.25f, 0.25f, 1f);

    private GameObject CreateButton(GameObject parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = new GameObject(label, typeof(RectTransform));
        btnGO.transform.SetParent(parent.transform, false);
        
        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.preferredHeight = 50;
        le.flexibleHeight = 0;
        
        Image img = btnGO.AddComponent<Image>();
        img.color = ButtonNormal;
        
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
        return t;
    }

    public bool IsMouseOverMenu()
    {
        if (!_isOpen || _panel == null || Mouse.current == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(_panel, Mouse.current.position.ReadValue(), null);
    }
}
