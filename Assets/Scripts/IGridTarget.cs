/// <summary>
/// Interface for any object that has a fixed location on the grid and can be targeted by units.
/// </summary>
public interface IGridTarget
{
    GridTile AssignedTile { get; }
}
