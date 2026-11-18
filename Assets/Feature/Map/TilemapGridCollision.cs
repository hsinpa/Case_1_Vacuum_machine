using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Hsinpa.Map
{
    /// <summary>
    /// A cell is blocked when the tilemap's collider shapes reach into the middle of it.
    /// Tiles that only spill a few pixels into a cell's edge (overhang from a neighbouring prop)
    /// do not block, which a plain Tilemap.HasTile check would get wrong.
    /// </summary>
    class TilemapGridCollision : IGridCollision
    {
        // Portion of the cell, centred, that must be free of collider shapes. 1 = the whole cell.
        const float ProbeSizeRatio = 0.5f;

        readonly TilemapCollider2D _collider;
        readonly GridLayout _grid;
        readonly Vector2 _probeSize;
        readonly List<Collider2D> _hits = new List<Collider2D>();
        ContactFilter2D _filter;

        public TilemapGridCollision(TilemapCollider2D collider)
        {
            _collider = collider;
            _grid = collider.GetComponent<Tilemap>().layoutGrid;
            _probeSize = (Vector2)_grid.cellSize * ProbeSizeRatio;

            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(1 << collider.gameObject.layer);
        }

        public bool IsWalkable(Vector2Int cell)
        {
            Vector2 cellCenter = _grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0)) + _grid.cellSize * 0.5f;

            Physics2D.OverlapBox(cellCenter, _probeSize, 0f, _filter, _hits);
            return !_hits.Contains(_collider);
        }
    }
}
