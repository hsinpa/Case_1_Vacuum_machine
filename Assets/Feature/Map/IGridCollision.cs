using UnityEngine;

namespace Hsinpa.Map
{
    /// <summary>
    /// Answers whether a grid cell can be entered. Implement this for any new kind of blocker.
    /// </summary>
    public interface IGridCollision
    {
        bool IsWalkable(Vector2Int cell);
    }
}
