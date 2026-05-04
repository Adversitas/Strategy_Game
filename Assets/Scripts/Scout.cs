using UnityEngine;

/// <summary>
/// A specialized unit with higher mobility (Dexterity).
/// </summary>
public class Scout : Unit
{
    protected override void Awake()
    {
        // Scouts are military units and have a bonus to dexterity
        isMilitary = true;
        dexterity += 1;
        
        // Call base.Awake() to calculate derived stats
        base.Awake();
    }
}
