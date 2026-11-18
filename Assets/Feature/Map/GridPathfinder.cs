using System.Collections.Generic;
using UnityEngine;

namespace Hsinpa.Map
{
    public enum PathResult
    {
        /// <summary>Path found (empty when start == goal).</summary>
        Found,
        /// <summary>Start outside the map, or goal blocked by terrain or another occupant.</summary>
        InvalidEndpoint,
        /// <summary>No route exists, by terrain or because occupants seal the goal off.</summary>
        Unreachable,
        /// <summary>Gave up after MaxExpansions cells; a route may still exist.</summary>
        LimitReached,
    }

    /// <summary>
    /// A* over a GridGraph with 4-directional moves of cost 1.
    /// All working memory is sized to the graph once and reused, so a search allocates nothing.
    /// Bound to one graph: create a new pathfinder if GridMap2D rebakes into a new graph.
    /// </summary>
    public class GridPathfinder
    {
        const int NotInHeap = -1;

        readonly GridGraph _graph;

        // Per-cell search state, indexed by GridGraph.ToIndex. A cell's data is valid only when
        // its state equals this search's open or closed mark, which saves clearing the arrays.
        readonly int[] _state;
        readonly int[] _costSoFar; // g
        readonly int[] _total;     // f = g + h
        readonly int[] _parent;
        int _openMark;
        int _closedMark;

        // Indexed binary min-heap: holds each open cell exactly once and lowers its cost in place
        // when a cheaper route is found, so it never needs more than one slot per cell.
        readonly int[] _heap;
        readonly int[] _heapPosition;
        int _heapCount;

        readonly List<Vector2Int> _neighbors = new List<Vector2Int>(4);

        public GridGraph Graph => _graph;

        /// <summary>
        /// Cells a single search may expand before giving up with LimitReached.
        /// Bounds the cost of goals that are walled in by characters, which region labels cannot catch.
        /// </summary>
        public int MaxExpansions { get; set; } = int.MaxValue;

        public GridPathfinder(GridGraph graph)
        {
            _graph = graph;

            int count = graph.CellCount;
            _state = new int[count];
            _costSoFar = new int[count];
            _total = new int[count];
            _parent = new int[count];
            _heap = new int[count];
            _heapPosition = new int[count];
        }

        /// <summary>
        /// Finds a shortest path from start to goal. On Found, path holds the cells to walk through,
        /// excluding start and ending at goal. Cells held by other occupants are treated as walls
        /// unless ignoreOccupants is set.
        /// </summary>
        public PathResult FindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> path,
            object asker = null, bool ignoreOccupants = false)
        {
            path.Clear();

            // The start cell is where the asker already stands, so only its bounds matter.
            if (!_graph.InBounds(start) || !CanEnter(goal, asker, ignoreOccupants)) return PathResult.InvalidEndpoint;
            if (start == goal) return PathResult.Found;

            // Separated by walls: rejected without searching.
            if (!_graph.AreStaticallyConnected(start, goal)) return PathResult.Unreachable;

            BeginSearch();

            int startIndex = _graph.ToIndex(start);
            int goalIndex = _graph.ToIndex(goal);

            Open(startIndex, costSoFar: 0, Heuristic(start, goal), parent: -1);

            int expansions = 0;
            while (_heapCount > 0)
            {
                int current = PopMin();
                _state[current] = _closedMark;

                if (current == goalIndex)
                {
                    BuildPath(goalIndex, startIndex, path);
                    return PathResult.Found;
                }

                if (++expansions > MaxExpansions) return PathResult.LimitReached;

                Vector2Int currentCell = _graph.ToCell(current);
                int nextCost = _costSoFar[current] + 1;

                _graph.GetNeighbors(currentCell, _neighbors, asker, ignoreOccupants);
                foreach (Vector2Int neighborCell in _neighbors)
                {
                    int neighbor = _graph.ToIndex(neighborCell);
                    int state = _state[neighbor];

                    // Manhattan on a unit-cost grid is consistent, so a closed cell is already optimal.
                    if (state == _closedMark) continue;

                    if (state == _openMark)
                    {
                        if (nextCost >= _costSoFar[neighbor]) continue;

                        int heuristic = _total[neighbor] - _costSoFar[neighbor];
                        _costSoFar[neighbor] = nextCost;
                        _total[neighbor] = nextCost + heuristic;
                        _parent[neighbor] = current;
                        SiftUp(_heapPosition[neighbor]);
                    }
                    else
                    {
                        Open(neighbor, nextCost, Heuristic(neighborCell, goal), current);
                    }
                }
            }

            // Region check passed, so it is occupants that seal the goal off.
            return PathResult.Unreachable;
        }

        /// <summary>Convenience wrapper when the reason for failure does not matter.</summary>
        public bool TryFindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> path,
            object asker = null, bool ignoreOccupants = false)
        {
            return FindPath(start, goal, path, asker, ignoreOccupants) == PathResult.Found;
        }

        bool CanEnter(Vector2Int cell, object asker, bool ignoreOccupants)
        {
            return ignoreOccupants ? _graph.IsStaticWalkable(cell) : _graph.IsFree(cell, asker);
        }

        // Manhattan distance: exact on an open 4-directional grid, never an overestimate.
        static int Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        void BeginSearch()
        {
            _heapCount = 0;

            // Two marks per search. Near overflow, reset so stale marks cannot match.
            if (_closedMark >= int.MaxValue - 2)
            {
                System.Array.Clear(_state, 0, _state.Length);
                _closedMark = 0;
            }
            _openMark = _closedMark + 1;
            _closedMark = _openMark + 1;
        }

        void Open(int index, int costSoFar, int heuristic, int parent)
        {
            _state[index] = _openMark;
            _costSoFar[index] = costSoFar;
            _total[index] = costSoFar + heuristic;
            _parent[index] = parent;

            _heapPosition[index] = _heapCount;
            _heap[_heapCount] = index;
            SiftUp(_heapCount++);
        }

        void BuildPath(int goalIndex, int startIndex, List<Vector2Int> path)
        {
            for (int index = goalIndex; index != startIndex; index = _parent[index])
                path.Add(_graph.ToCell(index));
            path.Reverse();
        }

        #region Indexed binary heap

        // Lower f first; on equal f, lower h (= f - g) first, so the search runs straight at the
        // goal instead of fanning out across equally short routes.
        bool Before(int a, int b)
        {
            int fa = _total[a], fb = _total[b];
            if (fa != fb) return fa < fb;
            return fa - _costSoFar[a] < fb - _costSoFar[b];
        }

        void Place(int position, int index)
        {
            _heap[position] = index;
            _heapPosition[index] = position;
        }

        void SiftUp(int position)
        {
            int index = _heap[position];
            while (position > 0)
            {
                int parent = (position - 1) / 2;
                if (!Before(index, _heap[parent])) break;
                Place(position, _heap[parent]);
                position = parent;
            }
            Place(position, index);
        }

        void SiftDown(int position)
        {
            int index = _heap[position];
            while (true)
            {
                int child = position * 2 + 1;
                if (child >= _heapCount) break;
                if (child + 1 < _heapCount && Before(_heap[child + 1], _heap[child])) child++;
                if (!Before(_heap[child], index)) break;
                Place(position, _heap[child]);
                position = child;
            }
            Place(position, index);
        }

        int PopMin()
        {
            int min = _heap[0];
            _heapPosition[min] = NotInHeap;

            if (--_heapCount > 0)
            {
                _heap[0] = _heap[_heapCount];
                SiftDown(0);
            }
            return min;
        }

        #endregion
    }
}
