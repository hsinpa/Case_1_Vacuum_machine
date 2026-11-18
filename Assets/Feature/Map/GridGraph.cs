using System.Collections.Generic;
using UnityEngine;

namespace Hsinpa.Map
{
    /// <summary>
    /// The map as data a pathfinder can search: a fixed rectangle of cells, each with a baked
    /// static walkability flag and an optional dynamic occupant (player, NPC, movable prop).
    /// Pure C#, so it can be built and tested without a scene.
    /// </summary>
    public class GridGraph
    {
        static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
        };

        readonly bool[] _walkable;
        readonly object[] _occupants;

        // Connected-region id per cell for the static layer (0 = blocked). Rebuilt lazily after edits.
        readonly int[] _region;
        int[] _floodQueue;
        bool _regionsDirty = true;

        public RectInt Bounds { get; }
        public int Width => Bounds.width;
        public int Height => Bounds.height;
        public int CellCount => _walkable.Length;

        public GridGraph(RectInt bounds)
        {
            Bounds = bounds;
            _walkable = new bool[bounds.width * bounds.height];
            _occupants = new object[_walkable.Length];
            _region = new int[_walkable.Length];
        }

        #region Indexing

        public bool InBounds(Vector2Int cell) => Bounds.Contains(cell);

        /// <summary>Flat array index for per-cell search data. Cell must be in bounds.</summary>
        public int ToIndex(Vector2Int cell) => (cell.x - Bounds.xMin) + (cell.y - Bounds.yMin) * Bounds.width;

        public Vector2Int ToCell(int index) =>
            new Vector2Int(Bounds.xMin + index % Bounds.width, Bounds.yMin + index / Bounds.width);

        #endregion

        #region Static layer

        /// <summary>Fills the static layer by asking the given collision source about every cell.</summary>
        public void Bake(IGridCollision source)
        {
            for (int i = 0; i < _walkable.Length; i++)
                _walkable[i] = source == null || source.IsWalkable(ToCell(i));
            _regionsDirty = true;
        }

        public void SetStaticWalkable(Vector2Int cell, bool walkable)
        {
            if (!InBounds(cell)) return;

            int index = ToIndex(cell);
            if (_walkable[index] == walkable) return;

            _walkable[index] = walkable;
            _regionsDirty = true;
        }

        /// <summary>Terrain only: in bounds and not blocked by the level. Ignores occupants.</summary>
        public bool IsStaticWalkable(Vector2Int cell) => InBounds(cell) && _walkable[ToIndex(cell)];

        /// <summary>
        /// True when terrain alone allows a route between the two cells. Constant time after the
        /// first call following a bake or edit. Ignores occupants: a true result can still be
        /// blocked by characters, but a false result is final.
        /// </summary>
        public bool AreStaticallyConnected(Vector2Int a, Vector2Int b)
        {
            if (!IsStaticWalkable(a) || !IsStaticWalkable(b)) return false;
            if (_regionsDirty) LabelRegions();
            return _region[ToIndex(a)] == _region[ToIndex(b)];
        }

        // Flood-fills each walkable area with its own id, using 4-directional connectivity
        // to match how characters move.
        void LabelRegions()
        {
            System.Array.Clear(_region, 0, _region.Length);
            _floodQueue ??= new int[_walkable.Length];

            int width = Bounds.width;
            int nextRegion = 1;

            for (int seed = 0; seed < _walkable.Length; seed++)
            {
                if (!_walkable[seed] || _region[seed] != 0) continue;

                int head = 0, tail = 0;
                _region[seed] = nextRegion;
                _floodQueue[tail++] = seed;

                while (head < tail)
                {
                    int i = _floodQueue[head++];
                    int x = i % width;

                    if (x > 0) TryFill(i - 1);
                    if (x < width - 1) TryFill(i + 1);
                    if (i >= width) TryFill(i - width);
                    if (i + width < _walkable.Length) TryFill(i + width);
                }

                nextRegion++;

                void TryFill(int neighbor)
                {
                    if (!_walkable[neighbor] || _region[neighbor] != 0) return;
                    _region[neighbor] = nextRegion;
                    _floodQueue[tail++] = neighbor;
                }
            }

            _regionsDirty = false;
        }

        #endregion

        #region Dynamic layer

        public object GetOccupant(Vector2Int cell) => InBounds(cell) ? _occupants[ToIndex(cell)] : null;

        /// <summary>
        /// Terrain and occupants: true when the asker could stand here right now.
        /// A cell held by the asker itself counts as free.
        /// </summary>
        public bool IsFree(Vector2Int cell, object asker)
        {
            if (!IsStaticWalkable(cell)) return false;
            object occupant = _occupants[ToIndex(cell)];
            return occupant == null || occupant == asker;
        }

        /// <summary>
        /// True when the asker could take a one-cell step (straight or diagonal) from this cell right now.
        /// A diagonal also needs both side cells free, so it never cuts a wall corner, squeezes between
        /// two diagonal blockers, or crosses through another occupant's path.
        /// </summary>
        public bool CanStep(Vector2Int from, Vector2Int direction, object asker)
        {
            if (!IsFree(from + direction, asker)) return false;
            if (direction.x == 0 || direction.y == 0) return true;

            return IsFree(from + new Vector2Int(direction.x, 0), asker)
                && IsFree(from + new Vector2Int(0, direction.y), asker);
        }

        /// <summary>Claims a cell for the owner. Fails if it is blocked or held by someone else.</summary>
        public bool TryOccupy(Vector2Int cell, object owner)
        {
            if (owner == null || !IsFree(cell, owner)) return false;
            _occupants[ToIndex(cell)] = owner;
            return true;
        }

        /// <summary>Releases a cell, but only if the owner actually holds it.</summary>
        public void Release(Vector2Int cell, object owner)
        {
            if (!InBounds(cell)) return;
            int index = ToIndex(cell);
            if (_occupants[index] == owner) _occupants[index] = null;
        }

        #endregion

        #region Neighbours

        /// <summary>
        /// Fills result with the 4-directional neighbours the asker could enter.
        /// Pass ignoreOccupants to plan through characters that may have moved by the time we get there.
        /// </summary>
        public void GetNeighbors(Vector2Int cell, List<Vector2Int> result, object asker = null, bool ignoreOccupants = false)
        {
            result.Clear();
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                bool enterable = ignoreOccupants ? IsStaticWalkable(next) : IsFree(next, asker);
                if (enterable) result.Add(next);
            }
        }

        #endregion
    }
}
