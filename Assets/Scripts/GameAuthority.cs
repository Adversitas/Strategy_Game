using UnityEngine;

/// <summary>
/// Central authority rules for local commands. This is the seam where a future
/// /// multiplayer transport/server can decide who is allowed to issue actions.
/// </summary>
public class GameAuthority : MonoBehaviour
{
    public static GameAuthority Instance { get; private set; }

    [SerializeField] private int localPlayerId = 0;
    [SerializeField] private bool allowCommandsOnUnownedObjects = true;

    public int LocalPlayerId => localPlayerId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("GameAuthority");
        Instance = go.AddComponent<GameAuthority>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool CanIssueCommands(Component target)
    {
        return target != null && CanIssueCommands(target.gameObject);
    }

    public bool CanIssueCommands(GameObject target)
    {
        if (target == null) return false;

        PlayerOwnership ownership = target.GetComponent<PlayerOwnership>();
        if (ownership == null) return allowCommandsOnUnownedObjects;

        return ownership.OwnerId == localPlayerId;
    }
}
