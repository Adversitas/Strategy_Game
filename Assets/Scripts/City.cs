using UnityEngine;

/// <summary>
/// Handles city-specific logic, including resource stockpiling.
/// </summary>
public class City : MonoBehaviour, IGridTarget
{
    [Header("Production Resources")]
    [SerializeField] private float productionStockpile = 0;
    [SerializeField] private int maxProductionStockpile = 500;
    [SerializeField] private int productionPerTurn = 5;

    [Header("Food Resources")]
    [SerializeField] private float foodStockpile = 0;
    [SerializeField] private int maxFoodStockpile = 500;
    [SerializeField] private int foodPerTurn = 5;

    [Header("Population")]
    [SerializeField] private int citizenCapacity = 1;
    [SerializeField] private int currentCitizens = 0;
    [SerializeField] private float foodToNextCitizen = 50f;

    [Header("Timing")]
    [SerializeField] private float turnDuration = 5f;

    private float _timer;

    [SerializeField] private GridTile assignedTile;
    public GridTile AssignedTile 
    { 
        get => assignedTile; 
        set => assignedTile = value; 
    }

    public float ProductionStockpile => productionStockpile;
    public int MaxProductionStockpile => maxProductionStockpile;
    public float FoodStockpile => foodStockpile;
    public int MaxFoodStockpile => maxFoodStockpile;
    public float FoodToNextCitizen => foodToNextCitizen;
    public int CitizenCapacity => citizenCapacity;
    public int CurrentCitizens => currentCitizens;

    private void Start()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.RegisterCity(this);
    }

    private void OnDestroy()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.UnregisterCity(this);
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        if (_timer >= turnDuration)
        {
            _timer -= turnDuration;
            GenerateResources();
        }
    }

    private void GenerateResources()
    {
        productionStockpile = Mathf.Min(productionStockpile + productionPerTurn, maxProductionStockpile);
        
        // Food grows and unlocks citizen capacity
        foodStockpile += foodPerTurn;
        if (foodStockpile >= foodToNextCitizen)
        {
            foodStockpile -= foodToNextCitizen;
            citizenCapacity++;
            Debug.Log($"City {name} expanded! New citizen capacity: {citizenCapacity}");
        }
    }

    public bool CanAffordProduction(float amount)
    {
        return productionStockpile >= amount;
    }

    public bool CanAffordFood(float amount)
    {
        return foodStockpile >= amount;
    }

    public void DeductProduction(float amount)
    {
        productionStockpile = Mathf.Max(0, productionStockpile - amount);
    }

    public void AddProduction(float amount)
    {
        productionStockpile = Mathf.Min(maxProductionStockpile, productionStockpile + amount);
    }

    public void DeductFood(float amount)
    {
        foodStockpile = Mathf.Max(0, foodStockpile - amount);
    }

    public bool HasCitizenCapacity()
    {
        return currentCitizens < citizenCapacity;
    }

    public void RegisterCitizen()
    {
        currentCitizens++;
    }

    public void UnregisterCitizen()
    {
        currentCitizens = Mathf.Max(0, currentCitizens - 1);
    }
}
