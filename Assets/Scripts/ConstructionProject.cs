using UnityEngine;

/// <summary>
/// A project in progress that can be completed by delivering production.
/// </summary>
public class ConstructionProject : MonoBehaviour, IGridTarget
{
    [Header("Progress")]
    [SerializeField] private float deliveredProduction = 0f;
    [SerializeField] private float builtProduction = 0f;
    [SerializeField] private float requiredProduction = 100f;
    [SerializeField] private float citizenBuildPerTurn = 5f;
    [SerializeField] private float turnDuration = 5f;

    [Header("Result")]
    [SerializeField] private GameObject resultPrefab;

    private float _timer;
    private bool _isCompleted;
    private Citizen _assignedCitizen;

    [SerializeField] private GridTile assignedTile;
    public GridTile AssignedTile 
    { 
        get => assignedTile; 
        set => assignedTile = value; 
    }

    public float CurrentProduction => builtProduction;
    public float DeliveredProduction => deliveredProduction;
    public float BuiltProduction => builtProduction;
    public float RequiredProduction => requiredProduction;
    public float RemainingProduction => Mathf.Max(0f, requiredProduction - deliveredProduction);
    public float RemainingBuildProduction => Mathf.Max(0f, requiredProduction - builtProduction);
    public float StoredProduction => Mathf.Max(0f, deliveredProduction - builtProduction);
    public bool IsCompleted => _isCompleted;
    public bool NeedsCitizenWork => !_isCompleted && StoredProduction > 0f && RemainingBuildProduction > 0f;
    public Citizen AssignedCitizen => _assignedCitizen;
    public GameObject ResultPrefab 
    {
        get => resultPrefab;
        set => resultPrefab = value;
    }

    private void Awake()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.RegisterConstructionProject(this);
        Debug.Log($"[ConstructionProject] Awake: name={name}, registry={(AssetRegistry.Instance != null ? "OK" : "NULL")}, assignedTile={(assignedTile != null ? assignedTile.GridPosition.ToString() : "NULL")}");
    }

    private void OnDestroy()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.UnregisterConstructionProject(this);
    }

    public bool TryAssignCitizen(Citizen citizen)
    {
        if (citizen == null || _isCompleted) return false;
        if (_assignedCitizen != null && _assignedCitizen != citizen) return false;

        _assignedCitizen = citizen;
        return true;
    }

    public void ReleaseCitizen(Citizen citizen)
    {
        if (_assignedCitizen == citizen)
        {
            _assignedCitizen = null;
        }
    }

    private void Update()
    {
        if (_isCompleted) return;

        _timer += Time.deltaTime;
        if (_timer < turnDuration) return;

        _timer -= turnDuration;
        ProcessCitizenConstructionTurn();
    }

    public void AddProduction(float amount)
    {
        if (_isCompleted || amount <= 0f) return;

        deliveredProduction = Mathf.Min(requiredProduction, deliveredProduction + amount);
        Debug.Log($"[ConstructionProject] Received {amount:F1} production. Delivered: {deliveredProduction:F1}/{requiredProduction:F1}, Built: {builtProduction:F1}/{requiredProduction:F1}");
    }

    public void SetRequiredProduction(float amount)
    {
        requiredProduction = amount;
    }

    private void CompleteProject()
    {
        _isCompleted = true;
        _assignedCitizen = null;

        if (resultPrefab != null && assignedTile != null)
        {
            GameObject resultGO = Instantiate(resultPrefab, GetTileCenter(assignedTile) + Vector3.back * 0.2f, Quaternion.identity);
            TileObjectScale.ApplyTo(resultGO);
            PlayerOwnership.Inherit(resultGO, gameObject);
            PlayerOwnership.EnsureLocalOwner(resultGO);
            assignedTile.OccupyingBuilding = resultGO;
            
            // Assign tile to the resulting building
            City city = resultGO.GetComponent<City>();
            if (city == null && resultPrefab.name.Contains("City"))
            {
                city = resultGO.AddComponent<City>();
            }
            if (city != null) city.AssignedTile = assignedTile;

            Market market = resultGO.GetComponent<Market>();
            if (market != null) market.Initialize(assignedTile);

            Debug.Log($"[ConstructionProject] Completed {resultPrefab.name} at {assignedTile.GridPosition}");
        }

        Destroy(gameObject);
    }

    private void ProcessCitizenConstructionTurn()
    {
        if (assignedTile == null || deliveredProduction <= builtProduction) return;

        Citizen citizen = assignedTile.OccupyingUnit as Citizen;
        if (citizen == null || citizen.CurrentTile != assignedTile) return;

        float toBuild = Mathf.Min(citizenBuildPerTurn, StoredProduction, RemainingBuildProduction);
        if (toBuild <= 0f) return;

        builtProduction += toBuild;
        Debug.Log($"[ConstructionProject] {citizen.name} built {toBuild:F1}. Built: {builtProduction:F1}/{requiredProduction:F1}, Stored: {StoredProduction:F1}");

        if (builtProduction >= requiredProduction)
        {
            CompleteProject();
        }
    }

    private static Vector3 GetTileCenter(GridTile tile)
    {
        if (tile == null) return Vector3.zero;

        SpriteRenderer tileRenderer = tile.GetComponent<SpriteRenderer>();
        if (tileRenderer != null) return tileRenderer.bounds.center;

        return tile.transform.position;
    }
}
