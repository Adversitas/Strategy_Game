using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A centralized registry that tracks all player-owned assets (Cities, Facilities, Units).
/// This provides a "Big List" for other systems to reference without searching the scene.
/// </summary>
public class AssetRegistry : MonoBehaviour
{
    public static AssetRegistry Instance { get; private set; }

    public List<City> Cities { get; private set; } = new List<City>();
    public List<Facility> Facilities { get; private set; } = new List<Facility>();
    public List<Unit> Units { get; private set; } = new List<Unit>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("AssetRegistry");
        Instance = go.AddComponent<AssetRegistry>();
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
    }

    public void RegisterCity(City city)
    {
        if (!Cities.Contains(city)) Cities.Add(city);
    }

    public void UnregisterCity(City city)
    {
        if (Cities.Contains(city)) Cities.Remove(city);
    }

    public void RegisterFacility(Facility facility)
    {
        if (!Facilities.Contains(facility)) Facilities.Add(facility);
    }

    public void UnregisterFacility(Facility facility)
    {
        if (Facilities.Contains(facility)) Facilities.Remove(facility);
    }

    public void RegisterUnit(Unit unit)
    {
        if (!Units.Contains(unit)) Units.Add(unit);
    }

    public void UnregisterUnit(Unit unit)
    {
        if (Units.Contains(unit)) Units.Remove(unit);
    }
}
