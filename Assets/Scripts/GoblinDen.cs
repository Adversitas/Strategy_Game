using UnityEngine;

/// <summary>
/// Hostile structure that generates production and periodically spawns goblin units.
/// "Hostile to all" is represented by ownerId = -1 via PlayerOwnership.
/// </summary>
public class GoblinDen : MonoBehaviour, IGridTarget
{
    [Header("Production")]
    [SerializeField] private float productionStockpile = 0f;
    [SerializeField] private int productionPerTurn = 5;
    [SerializeField] private float turnDuration = 5f;
    [SerializeField] private float spawnThreshold = 50f;

    [Header("Placement")]
    [SerializeField] private GridTile assignedTile;

    private float _timer;

    public GridTile AssignedTile
    {
        get => assignedTile;
        set => assignedTile = value;
    }

    public float ProductionStockpile => productionStockpile;

    private void Awake()
    {
        EnsureGoblinOwnership(gameObject);
    }

    private void Start()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.RegisterGoblinDen(this);
    }

    private void OnDestroy()
    {
        if (AssetRegistry.Instance != null) AssetRegistry.Instance.UnregisterGoblinDen(this);
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < turnDuration) return;

        _timer -= turnDuration;
        productionStockpile += productionPerTurn;

        if (productionStockpile >= spawnThreshold)
        {
            if (TrySpawnGoblin())
            {
                productionStockpile -= spawnThreshold;
            }
        }
    }

    private bool TrySpawnGoblin()
    {
        GridTile spawnTile = FindSpawnTile();
        if (spawnTile == null) return false;

        // Choose one goblin type uniformly for now.
        int roll = Random.Range(0, 3);
        Unit goblin =
            roll == 0 ? GoblinRogue.Create()
          : roll == 1 ? GoblinAssassin.Create()
          : GoblinBrawler.Create();

        if (goblin == null) return false;

        EnsureGoblinOwnership(goblin.gameObject);
        goblin.PlaceOnTile(spawnTile);
        return true;
    }

    private GridTile FindSpawnTile()
    {
        if (assignedTile == null || SquareGridGenerator.Instance == null) return null;

        foreach (GridTile neighbor in SquareGridGenerator.Instance.GetNeighbors(assignedTile))
        {
            if (neighbor != null && neighbor.IsEmpty) return neighbor;
        }

        return assignedTile.IsEmpty ? assignedTile : null;
    }

    private static void EnsureGoblinOwnership(GameObject target)
    {
        if (target == null) return;

        PlayerOwnership ownership = target.GetComponent<PlayerOwnership>();
        if (ownership == null) ownership = target.AddComponent<PlayerOwnership>();
        ownership.SetOwner(-1);
    }
}

