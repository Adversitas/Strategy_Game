using UnityEngine;

[DisallowMultipleComponent]
public class PlayerOwnership : MonoBehaviour
{
    [SerializeField] private int ownerId = 0;

    public int OwnerId => ownerId;

    public void SetOwner(int newOwnerId)
    {
        ownerId = newOwnerId;
    }

    public static void Inherit(GameObject target, Component source)
    {
        if (target == null || source == null) return;
        Inherit(target, source.gameObject);
    }

    public static void Inherit(GameObject target, GameObject source)
    {
        if (target == null || source == null) return;

        PlayerOwnership sourceOwnership = source.GetComponent<PlayerOwnership>();
        if (sourceOwnership == null) return;

        PlayerOwnership targetOwnership = target.GetComponent<PlayerOwnership>();
        if (targetOwnership == null)
        {
            targetOwnership = target.AddComponent<PlayerOwnership>();
        }

        targetOwnership.SetOwner(sourceOwnership.OwnerId);
    }

    public static void EnsureLocalOwner(GameObject target)
    {
        if (target == null) return;

        PlayerOwnership ownership = target.GetComponent<PlayerOwnership>();
        if (ownership == null)
        {
            ownership = target.AddComponent<PlayerOwnership>();
        }

        if (GameAuthority.Instance != null)
        {
            ownership.SetOwner(GameAuthority.Instance.LocalPlayerId);
        }
    }
}
