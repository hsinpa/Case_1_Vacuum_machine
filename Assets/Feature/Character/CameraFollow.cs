using Hsinpa.Map;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hsinpa.Character
{
    /// <summary>
    /// Follows a target with a dead zone: while the target stays inside an invisible box in the middle
    /// of the view, the camera holds still. When the target walks past an edge of the box, the camera
    /// eases along just far enough to put the target back on that edge. The view never shows past the
    /// map edges (GridMap2D bounds); on an axis where the map is smaller than the view, it is centred.
    /// <para>Cutscenes: disable this component (e.g. from a Signal Receiver or CutsceneActor event) while
    /// a Timeline animates the camera. On re-enable it starts from wherever the camera was left and
    /// glides back. Script commands use Follow, PanTo and ResetTarget instead.</para>
    /// <para>Pixel perfect: the camera is smoothed at full precision internally, but the transform only
    /// ever lands on whole art pixels. Otherwise the world and the sprites round to the pixel grid on
    /// different frames and the character wobbles by a pixel while the camera moves.</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField, Tooltip("What the camera follows, usually the player character.")]
        Transform _target;

        [SerializeField, Tooltip("Added to the target's position. Characters have their pivot at their feet, so this aims at the body.")]
        Vector2 _targetOffset = new Vector2(0f, 0.5f);

        [SerializeField, Tooltip("Size of the dead zone as a fraction of the view (0.3 = 30% of the view's width).")]
        Vector2 _deadZone = new Vector2(0.3f, 0.2f);

        [SerializeField, Min(0f), Tooltip("Roughly how many seconds the camera takes to catch up. 0 = no smoothing.")]
        float _smoothTime = 0.25f;

        [Header("Map edges")]
        [SerializeField, Tooltip("Keeps the view inside the bounds of GridMap2D.Active.")]
        bool _clampToMap = true;

        GridMap2D _map;
        bool _reportedNoMap;

        /// <summary>The scene's camera follow, for script commands. The last one enabled wins.</summary>
        public static CameraFollow Active { get; private set; }

        Camera _camera;
        PixelPerfectCamera _pixelPerfect;
        Transform _defaultTarget;

        // Smoothed at full precision; the transform gets the pixel-snapped copy.
        Vector3 _position;
        Vector3 _goal;
        Vector3 _velocity;
        Vector3 _written;

        // A pan ends in a hold: the camera stays put until Follow or ResetTarget.
        bool _holding;
        bool _panning;
        Vector3 _panFrom;
        Vector3 _panTo;
        float _panElapsed;
        float _panDuration;

        public Transform Target => _target;
        public bool IsPanning => _panning;

        GridMap2D Map
        {
            get
            {
                if (_map == null) _map = GridMap2D.Active;
                if (_map == null && !_reportedNoMap)
                {
                    // Once, not every frame; clamping is skipped until a map is enabled.
                    Debug.LogError($"{name}: CameraFollow cannot clamp to the map edges, no GridMap2D is enabled in the scene.", this);
                    _reportedNoMap = true;
                }
                else if (_map != null) _reportedNoMap = false;
                return _map;
            }
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _pixelPerfect = GetComponent<PixelPerfectCamera>();
            _defaultTarget = _target;
        }

        void OnEnable()
        {
            Active = this;
            Resync();
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            // Nobody may wait forever on a pan that cannot run.
            _panning = false;
        }

        void Start()
        {
            SnapToTarget();
        }

        // After Update, so the character has already moved this frame.
        void LateUpdate()
        {
            if (transform.position != _written) Resync();   // something else moved the camera

            if (_panning)
            {
                _panElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_panElapsed / _panDuration));
                _position = Vector3.Lerp(_panFrom, _panTo, t);
                if (t >= 1f) _panning = false;
            }
            else if (!_holding && _target != null)
            {
                UpdateGoal();
                _position = _smoothTime > 0f
                    ? Vector3.SmoothDamp(_position, _goal, ref _velocity, _smoothTime)
                    : _goal;
            }

            _position = Clamp(_position);
            Apply();
        }

        /// <summary>Follows a new target with the usual dead zone and smoothing. Ends any pan or hold.</summary>
        public void Follow(Transform target)
        {
            _target = target;
            _holding = false;
            _panning = false;
            _goal = _position;
        }

        /// <summary>Follows the target set in the Inspector again.</summary>
        public void ResetTarget() => Follow(_defaultTarget);

        /// <summary>
        /// Stops following and glides so the view is centred on a world point (as close as the map edges
        /// allow), then holds there until Follow or ResetTarget. Duration 0 cuts straight there.
        /// </summary>
        public void PanTo(Vector3 worldPoint, float duration)
        {
            _holding = true;
            _velocity = Vector3.zero;

            Vector3 destination = Clamp(new Vector3(worldPoint.x, worldPoint.y, _position.z));
            if (duration <= 0f || !isActiveAndEnabled)
            {
                _panning = false;
                _position = destination;
                Apply();
                return;
            }

            _panning = true;
            _panFrom = _position;
            _panTo = destination;
            _panElapsed = 0f;
            _panDuration = duration;
        }

        /// <summary>Centres on the target at once, with no smoothing, e.g. on scene start or after a teleport.</summary>
        public void SnapToTarget()
        {
            if (_target == null) return;

            Vector2 focus = Focus();
            _position = Clamp(new Vector3(focus.x, focus.y, transform.position.z));
            _goal = _position;
            _velocity = Vector3.zero;
            Apply();
        }

        void UpdateGoal()
        {
            Vector2 focus = Focus();
            Vector2 halfBox = Vector2.Scale(_deadZone, HalfViewSize());

            // Box centred on where the camera is heading, so the goal moves only when the target leaves it.
            _goal.x += Overshoot(focus.x - _goal.x, halfBox.x);
            _goal.y += Overshoot(focus.y - _goal.y, halfBox.y);
            _goal.z = _position.z;

            // Pull the goal back too, or SmoothDamp keeps pushing against the edge after the clamp.
            _goal = Clamp(_goal);
        }

        static float Overshoot(float distance, float halfBox)
        {
            if (distance > halfBox) return distance - halfBox;
            if (distance < -halfBox) return distance + halfBox;
            return 0f;
        }

        Vector2 Focus() => (Vector2)_target.position + _targetOffset;

        Vector2 HalfViewSize()
        {
            float halfHeight = _camera.orthographicSize;
            return new Vector2(halfHeight * _camera.aspect, halfHeight);
        }

        Vector3 Clamp(Vector3 position)
        {
            if (!_clampToMap || !TryGetMapRect(out Rect map)) return position;

            Vector2 half = HalfViewSize();
            position.x = ClampAxis(position.x, map.xMin, map.xMax, half.x);
            position.y = ClampAxis(position.y, map.yMin, map.yMax, half.y);
            return position;
        }

        static float ClampAxis(float value, float min, float max, float halfView)
        {
            // A map smaller than the view cannot fill it; centre it instead of jittering between edges.
            if (max - min <= halfView * 2f) return (min + max) * 0.5f;
            return Mathf.Clamp(value, min + halfView, max - halfView);
        }

        bool TryGetMapRect(out Rect rect)
        {
            rect = default;
            GridMap2D map = Map;
            if (map == null) return false;

            // Bounds are fixed once the graph is baked, so read them there rather than recomputing.
            RectInt cells = Application.isPlaying ? map.Graph.Bounds : map.CellBounds;
            Vector3 min = map.Layout.CellToWorld(new Vector3Int(cells.xMin, cells.yMin, 0));
            Vector3 max = map.Layout.CellToWorld(new Vector3Int(cells.xMax, cells.yMax, 0));
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        void Apply()
        {
            Vector3 snapped = _pixelPerfect != null && _pixelPerfect.isActiveAndEnabled
                ? _pixelPerfect.RoundToPixel(_position)
                : _position;

            // Rounding z could move the camera through the sprites' plane; keep it as authored.
            snapped.z = _position.z;
            transform.position = snapped;
            _written = snapped;
        }

        /// <summary>Takes over from wherever the camera is now, e.g. after a Timeline moved it.</summary>
        void Resync()
        {
            _position = transform.position;
            _written = _position;
            _velocity = Vector3.zero;
            if (!_panning) _goal = _position;
        }

        void OnDrawGizmosSelected()
        {
            Camera cam = _camera != null ? _camera : GetComponent<Camera>();
            if (cam == null) return;

            float halfHeight = cam.orthographicSize;
            Vector2 halfView = new Vector2(halfHeight * cam.aspect, halfHeight);
            Vector3 centre = Application.isPlaying ? _goal : transform.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(centre.x, centre.y, 0f), Vector2.Scale(_deadZone, halfView) * 2f);

            if (_target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere((Vector3)((Vector2)_target.position + _targetOffset), 0.15f);
            }
        }
    }
}
