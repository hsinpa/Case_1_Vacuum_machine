using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hsinpa.Map
{
    /// <summary>
    /// The playable area of a scene: a rectangle of cells centred on this object,
    /// minus whatever the collision tilemap blocks. Always sits on the Grid object.
    /// Owns the GridGraph that movement and pathfinding read from.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class GridMap2D : MonoBehaviour, IGridCollision
    {
        [SerializeField, Range(1, 100)]
        private int gridSizeX = 1;

        [SerializeField, Range(1, 100)]
        private int gridSizeY = 1;

        [SerializeField]
        private TilemapCollider2D collisionTilemap;

        [Header("Debug")]
        [SerializeField, Tooltip("Draw blocked (red) and occupied (yellow) cells when selected. Probes physics every frame in edit mode.")]
        private bool drawCellStates = false;

        GridLayout _layout;
        GridGraph _graph;
        GridPathfinder _pathfinder;

        /// <summary>
        /// The map agents use when none is assigned to them. Set while enabled, so it is ready by
        /// any Start. With two maps enabled at once (e.g. additive scenes), assign agents explicitly.
        /// </summary>
        public static GridMap2D Active { get; private set; }

        void OnEnable()
        {
            if (Active != null && Active != this)
                Debug.LogWarning($"Two GridMap2D enabled ({Active.name}, {name}); {name} becomes Active. Assign maps on agents explicitly.", this);
            Active = this;
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        public GridLayout Layout
        {
            get
            {
                if (_layout == null) _layout = GetComponent<Grid>();
                return _layout;
            }
        }

        /// <summary>
        /// The cells inside the map. Max is exclusive, as with any RectInt.
        /// Fixed once the graph is baked; read it from Graph.Bounds at runtime.
        /// </summary>
        public RectInt CellBounds
        {
            get
            {
                Vector3Int centerCell = Layout.WorldToCell(transform.position);
                var size = new Vector2Int(gridSizeX, gridSizeY);
                var min = new Vector2Int(centerCell.x - size.x / 2, centerCell.y - size.y / 2);
                return new RectInt(min, size);
            }
        }

        /// <summary>
        /// Baked on first access, so it is ready whichever Start runs first.
        /// </summary>
        public GridGraph Graph
        {
            get
            {
                if (_graph == null) Rebake();
                return _graph;
            }
        }

        /// <summary>
        /// One pathfinder shared by every agent on this map. Searches run synchronously on the
        /// main thread, so sharing is safe and the working memory exists only once.
        /// </summary>
        public GridPathfinder Pathfinder
        {
            get
            {
                if (_pathfinder == null || _pathfinder.Graph != Graph) _pathfinder = new GridPathfinder(Graph);
                return _pathfinder;
            }
        }

        /// <summary>
        /// Rebuilds the static layer from the collision tilemap. Call after the level layout changes.
        /// Occupants are dropped, so characters must re-occupy their cells afterwards.
        /// </summary>
        public void Rebake()
        {
            _graph = BuildGraph();
            _pathfinder = null;
        }

        public bool Contains(Vector2Int cell) => Graph.InBounds(cell);

        /// <summary>Static terrain only; see GridGraph.IsFree for occupant-aware checks.</summary>
        public bool IsWalkable(Vector2Int cell) => Graph.IsStaticWalkable(cell);

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3Int cell = Layout.WorldToCell(worldPosition);
            return new Vector2Int(cell.x, cell.y);
        }

        public Vector3 CellCenterToWorld(Vector2Int cell)
        {
            return Layout.CellToWorld(new Vector3Int(cell.x, cell.y, 0)) + Layout.cellSize * 0.5f;
        }

        GridGraph BuildGraph()
        {
            var graph = new GridGraph(CellBounds);

            if (collisionTilemap == null)
            {
                graph.Bake(null);
                return graph;
            }

            // The probe is a physics query, so the collider shapes must be current before we read them.
            if (collisionTilemap.hasTilemapChanges) collisionTilemap.ProcessTilemapChanges();
            Physics2D.SyncTransforms();

            graph.Bake(new TilemapGridCollision(collisionTilemap));
            return graph;
        }

        private void OnDrawGizmosSelected()
        {
            if (Layout == null) return;

            // Drawn from CellBounds, so the outline is exactly the cells the character may enter.
            RectInt bounds = CellBounds;
            Vector3 min = Layout.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));
            Vector3 max = Layout.CellToWorld(new Vector3Int(bounds.xMax, bounds.yMax, 0));

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube((min + max) * 0.5f, max - min);

            if (drawCellStates) DrawCellStates();
        }

        void DrawCellStates()
        {
            // In edit mode there is no live graph, so bake a throwaway one to preview the result.
            GridGraph graph = Application.isPlaying ? Graph : BuildGraph();
            Vector3 size = Layout.cellSize * 0.9f;

            for (int i = 0; i < graph.CellCount; i++)
            {
                Vector2Int cell = graph.ToCell(i);
                Color? color = !graph.IsStaticWalkable(cell) ? Color.red
                    : graph.GetOccupant(cell) != null ? Color.yellow
                    : (Color?)null;
                if (color == null) continue;

                Gizmos.color = color.Value * new Color(1, 1, 1, 0.35f);
                Gizmos.DrawCube(CellCenterToWorld(cell), size);
            }
        }
    }
}
