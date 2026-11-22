using UnityEditor;
using UnityEngine;

namespace Hsinpa.Map.Editor
{
    /// <summary>
    /// Adds a "Mouse Cell" readout to the GridMap2D inspector: while the map is selected, it shows the
    /// grid cell under the mouse in the Scene view, for writing cutscene commands like walk_to.
    /// </summary>
    [CustomEditor(typeof(GridMap2D))]
    class GridMap2DEditor : UnityEditor.Editor
    {
        bool _hasCell;
        Vector2Int _cell;

        void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        void OnSceneGUI(SceneView view)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseLeaveWindow)
            {
                SetCell(false, default);
                return;
            }
            if (e.type != EventType.MouseMove && e.type != EventType.MouseDrag) return;

            var map = (GridMap2D)target;
            if (map == null || map.Layout == null) return;

            // Cast onto the grid's plane, as click-to-move does, so it works for any camera angle.
            var plane = new Plane(map.transform.forward, map.transform.position);
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!plane.Raycast(ray, out float distance))
            {
                SetCell(false, default);
                return;
            }

            SetCell(true, map.WorldToCell(ray.GetPoint(distance)));
        }

        void SetCell(bool hasCell, Vector2Int cell)
        {
            if (hasCell == _hasCell && cell == _cell) return;
            _hasCell = hasCell;
            _cell = cell;
            Repaint();   // only when the cell changes, not on every mouse move
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mouse Cell", EditorStyles.boldLabel);

            if (!_hasCell)
            {
                EditorGUILayout.HelpBox("Move the mouse over the Scene view.", MessageType.None);
                return;
            }

            var map = (GridMap2D)target;
            bool inMap = map.CellBounds.Contains(_cell);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector2IntField("Cell", _cell);
                EditorGUILayout.Toggle("Inside Map", inMap);

                // The live graph only exists in Play mode; in edit mode it would be a stale bake.
                if (Application.isPlaying && inMap)
                {
                    EditorGUILayout.Toggle("Walkable", map.IsWalkable(_cell));
                    object occupant = map.Graph.GetOccupant(_cell);
                    EditorGUILayout.TextField("Occupant", occupant is Object o ? o.name : occupant?.ToString() ?? "-");
                }
            }
        }
    }
}
