using UnityEngine;

[DisallowMultipleComponent]
public class TileObjectScale : MonoBehaviour
{
    [Min(0.01f)]
    [SerializeField] private float scaleMultiplier = 1f;

    public float ScaleMultiplier => scaleMultiplier;

    private void Awake()
    {
        ApplyScale();
    }

    private void OnValidate()
    {
        ApplyScale();
    }

    public void ApplyScale()
    {
        transform.localScale = Vector3.one * scaleMultiplier;
    }

    public static void ApplyTo(GameObject target)
    {
        if (target == null) return;

        TileObjectScale tileScale = target.GetComponent<TileObjectScale>();
        if (tileScale != null)
        {
            tileScale.ApplyScale();
        }
        else
        {
            target.transform.localScale = Vector3.one;
        }
    }
}
