using UnityEngine;

/// <summary>
/// Base class for unit orders or policies that can be assigned to a unit.
/// </summary>
[System.Serializable]
public class Order
{
    public string orderName = "No Order";
    public string description = "The unit has no current orders.";
}
