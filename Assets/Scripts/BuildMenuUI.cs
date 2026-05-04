using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// A simplistic, minimal right-side build menu.
/// </summary>
public class BuildMenuUI : MonoBehaviour
{
    public static BuildMenuUI Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject cityPrefab;

    private Canvas         _canvas;
    private RectTransform  _panel;
    private Text           _coordLabel;
    private GameObject     _buildCityBtn;

    private GridTile  _currentTile;
    private bool      _isOpen;
    private Coroutine _slideCoroutine;

    private const float SlideDuration = 0.2f;

    // Minimalistic Palette
    private static readonly Color PanelBg         = new Color(0.05f, 0.05f, 0.05f, 0.9f);
    private static readonly Color ButtonNormal     = new Color(0.2f, 0.2f, 0.25f, 1f);
    private static readonly Color ButtonHighlight  = new Color(0.3f, 0.3f, 0.4f, 1f);
    private static readonly Color TextWhite        = new Color(0.95f, 0.95f, 1f, 1f);
    private static readonly Color TextMuted        = new Color(0.6f, 0.6f, 0.7f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<BuildMenuUI>() != null) return;
        new GameObject("BuildMenuUI").AddComponent<BuildMenuUI>();
    }

    private void Awake()
    {
        Instance = this;

        // Ensure EventSystem exists, otherwise UI clicks won't work
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        if (cityPrefab == null) cityPrefab = Resources.Load<GameObject>("City");
        BuildUI();
        HideImmediate();
    }

    private void OnEnable()
    {
        SquareGridGenerator.OnBuildMenuRequested += HandleBuildMenuRequest;
        SquareGridGenerator.OnTileSelected      += HandleTileSelected;
    }

    private void OnDisable()
    {
        SquareGridGenerator.OnBuildMenuRequested -= HandleBuildMenuRequest;
        SquareGridGenerator.OnTileSelected      -= HandleTileSelected;
    }

    private void Update()
    {
        if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SlideOut();
    }

    private void HandleBuildMenuRequest(GridTile tile)
    {
        _currentTile = tile;
        _coordLabel.text = $"TILE {tile.GridPosition.x}, {tile.GridPosition.y}";

        if (_buildCityBtn != null) 
        {
            bool hasPrefab = cityPrefab != null;
            bool isEmpty = tile.IsEmpty;
            Debug.Log($"[BuildMenuUI] Request for tile {tile.GridPosition}. Prefab assigned: {hasPrefab}, Tile empty: {isEmpty}");
            _buildCityBtn.SetActive(hasPrefab && isEmpty);
        }
        else
        {
            Debug.LogWarning("[BuildMenuUI] _buildCityBtn is null!");
        }

        if (!_isOpen) SlideIn();
    }

    private void HandleTileSelected(GridTile tile)
    {
        if (_isOpen) SlideOut();
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
        _slideCoroutine = StartCoroutine(AnimatePanel(_panel.rect.width, true));
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
        _panel.anchoredPosition = new Vector2(_panel.rect.width, 0);
        _panel.gameObject.SetActive(false);
    }

    private void BuildCity()
        {
            if (_currentTile == null || cityPrefab == null || !_currentTile.IsEmpty) return;
            
            // Get the visual center of the tile's sprite
            SpriteRenderer tileRenderer = _currentTile.GetComponent<SpriteRenderer>();
            Vector3 spawnPos = tileRenderer != null ? tileRenderer.bounds.center : _currentTile.transform.position;
            
            spawnPos.z = -0.1f; // Keep it slightly in front to avoid Z-fighting
            
            GameObject cityGO = Instantiate(cityPrefab, spawnPos, Quaternion.identity);
            City city = cityGO.GetComponent<City>();
            if (city == null) city = cityGO.AddComponent<City>();
            
            city.AssignedTile = _currentTile;
            _currentTile.OccupyingBuilding = cityGO;
            SlideOut();
        }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("BuildMenuCanvas");
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

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(_canvas.transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        _panel.anchorMin = new Vector2(1, 0);
        _panel.anchorMax = new Vector2(1, 1);
        _panel.pivot = new Vector2(1, 0.5f);
        _panel.sizeDelta = new Vector2(400, 0);
        
        panelGO.AddComponent<Image>().color = PanelBg;

        VerticalLayoutGroup vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 40, 40);
        vlg.spacing = 20;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;

        _coordLabel = CreateLabel(panelGO, "Header", "TILE 0, 0", 18, TextMuted);
        
        // Buttons
        _buildCityBtn = CreateButton(panelGO, "BUILD CITY", BuildCity);
        
        // Simple Close
        CreateButton(panelGO, "CLOSE", SlideOut);
    }

    private Text CreateLabel(GameObject parent, string name, string text, int size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        
        // Crisp Text Hack: Multiply size by 5, scale by 0.2
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size * 5; 
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        go.transform.localScale = Vector3.one * 0.2f;
        
        return t;
    }

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
        t.fontSize = 18 * 5; // Crisp Text Hack
        t.color = TextWhite;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        textGO.transform.localScale = Vector3.one * 0.2f;
        
        return btnGO;
    }

    /// <summary>
    /// Checks if the mouse is currently over the build menu panel.
    /// </summary>
    public bool IsMouseOverMenu()
    {
        if (!_isOpen || _panel == null || Mouse.current == null) return false;
        
        return RectTransformUtility.RectangleContainsScreenPoint(
            _panel, 
            Mouse.current.position.ReadValue(), 
            null // null for ScreenSpaceOverlay
        );
    }
}
