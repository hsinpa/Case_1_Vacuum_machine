# Map

The grid that characters walk on. It answers three questions:

- **Where am I?** Converting between world positions and grid cells.
- **Can I stand here?** Walls from the level, plus whichever characters are standing where.
- **How do I get there?** A* pathfinding between two cells.

Everything lives in the `Hsinpa.Map` namespace (assembly `GridMap`).

---

## Architecture

```
            Scene                                 Pure C# (no scene needed)
 ┌──────────────────────────┐
 │ GridMap2D  (on the Grid) │ ── owns ──►  GridGraph  ◄── searches ──  GridPathfinder
 │  - map size              │                  ▲
 │  - collision tilemap     │                  │ baked from
 └──────────────────────────┘                  │
                                        IGridCollision
                                               ▲
                                     TilemapGridCollision
```

| Piece | Role |
|---|---|
| **`GridMap2D`** | The MonoBehaviour you put on the `Grid` object. It's the entry point: it defines the map's size, converts world ⇄ cell, and creates the graph and pathfinder. `GridMap2D.Active` gives you the current map without wiring a reference. |
| **`GridGraph`** | The map as plain data. Each cell has two layers: **static** (is there a wall?) and **dynamic** (which character, if any, is standing here?). |
| **`GridPathfinder`** | A* search over a `GridGraph`. One instance is shared by everything on the map. |
| **`IGridCollision`** | "Is this cell walkable?" Implement it to add a new kind of blocker. |
| **`TilemapGridCollision`** | The default blocker: reads the collision tilemap. A cell only counts as blocked if the collider covers its middle, so small overhangs from props don't block. |
| **`TileMarkIndicator`** | A small visual helper that blinks a marker on a tile (e.g. the player's click target). |

A few things worth knowing:

- **Baking.** The walls are read from the tilemap once, the first time `Graph` is used. If the level layout changes at runtime, call `Rebake()`. Characters lose their claimed cells when that happens and must claim them again.
- **Occupants.** Characters "claim" a cell with `TryOccupy` and give it back with `Release`. Anything can be an occupant; the graph just stores an `object`.
- **Movement is 4-directional** for pathfinding. Single steps (`CanStep`) also allow diagonals, but never through wall corners.

---

## Usage

### Get the map

```csharp
GridMap2D map = GridMap2D.Active;   // or a serialized reference
```

### World ⇄ cell

```csharp
Vector2Int cell  = map.WorldToCell(transform.position);
Vector3    world = map.CellCenterToWorld(cell);   // snaps to the middle of the cell
```

### Can I walk there?

```csharp
// Walls only
bool walkable = map.IsWalkable(cell);

// Walls AND other characters ("this" = whoever is asking; your own cell counts as free)
bool free = map.Graph.IsFree(cell, this);
```

### Claim and release a cell

```csharp
if (map.Graph.TryOccupy(nextCell, this))
{
    map.Graph.Release(currentCell, this);
    currentCell = nextCell;
}
```

### Find a path

```csharp
var path = new List<Vector2Int>();   // reuse this list, don't allocate every call

PathResult result = map.Pathfinder.FindPath(start, goal, path, asker: this);

if (result == PathResult.Found)
{
    // path = cells to walk through, NOT including start, ending at goal
    foreach (Vector2Int step in path) { /* move to step */ }
}
```

If you only care whether it worked, use `TryFindPath(...)`, which returns a `bool`.

`PathResult` tells you why it failed:

| Result | Meaning |
|---|---|
| `Found` | Path is ready (empty if you're already at the goal). |
| `InvalidEndpoint` | Start is off the map, or the goal is a wall or occupied. |
| `Unreachable` | No route exists, either because walls separate them or characters block the way. |
| `LimitReached` | Search gave up early (see `MaxExpansions`). |

Pass `ignoreOccupants: true` to plan through characters that will probably have moved by the time you get there.

---

## Collision

### How static collision is built

Static collision is the "is there a wall here?" layer. It is built once, in a step called **baking**:

1. `GridMap2D` creates an empty `GridGraph` covering its map rectangle.
2. It wraps the collision tilemap in a `TilemapGridCollision`.
3. `GridGraph.Bake(...)` visits every cell and asks `IsWalkable(cell)`, storing the answer as a `bool`.

`TilemapGridCollision` decides by placing a small box in the **middle half** of the cell and checking whether the tilemap's collider touches it. That way a tree whose collider leaks a few pixels into the next cell doesn't block that cell.

```
 ┌───────────┐
 │           │   The dashed box is the probe (50% of the cell).
 │  ┌ ─ ─ ┐  │   The collider must reach into it to block the cell.
 │  ╎     ╎  │
 │  └ ─ ─ ┘  │
 └───────────┘
```

No collision tilemap assigned? Then every cell inside the map counts as walkable.

After baking, the result is just data. Moving or editing the tilemap later does nothing until you bake again.

### Changing collision at runtime

Choose the method based on what the blocker is:

| Blocker | Use |
|---|---|
| Something that moves (character, pushable box) | **Occupant**: `TryOccupy` / `Release` (see above) |
| A single cell that opens or closes (door, gate, bridge) | `SetStaticWalkable` |
| A big layout change (painted new walls into the tilemap) | Edit the tilemap, then `Rebake()` |

**A door that opens and closes**: the cheapest option. It changes a single cell without touching the tilemap.

```csharp
Vector2Int doorCell = map.WorldToCell(door.transform.position);

map.Graph.SetStaticWalkable(doorCell, false);   // closed
map.Graph.SetStaticWalkable(doorCell, true);    // opened
```

**A pushable box**: it should block like a wall but can move, so make it an occupant.

```csharp
public class PushableBox : MonoBehaviour
{
    Vector2Int _cell;

    void Start()
    {
        _cell = GridMap2D.Active.WorldToCell(transform.position);
        GridMap2D.Active.Graph.TryOccupy(_cell, this);
    }

    public bool TryMoveTo(Vector2Int next)
    {
        var graph = GridMap2D.Active.Graph;
        if (!graph.TryOccupy(next, this)) return false;

        graph.Release(_cell, this);
        _cell = next;
        transform.position = GridMap2D.Active.CellCenterToWorld(next);
        return true;
    }

    void OnDestroy() => GridMap2D.Active?.Graph.Release(_cell, this);
}
```

**Painting new walls into the tilemap**: changes the tilemap itself, so it has to be baked again.

```csharp
collisionTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), wallTile);
map.Rebake();   // re-reads the whole tilemap

// Rebake clears all occupants, so every character/box must call TryOccupy again.
```

> Prefer `SetStaticWalkable` over `Rebake()` for small changes. Rebake reads every cell and forgets every occupant.

### Adding a new kind of blocker

Implement `IGridCollision` and bake with it:

```csharp
class WaterCollision : IGridCollision
{
    readonly Tilemap _water;
    public WaterCollision(Tilemap water) => _water = water;

    public bool IsWalkable(Vector2Int cell) => !_water.HasTile(new Vector3Int(cell.x, cell.y, 0));
}

map.Graph.Bake(new WaterCollision(waterTilemap));
```

Note that `Bake` **replaces** the whole static layer, so the result above ignores the collision tilemap. To keep both, check both sources inside your own `IsWalkable`.

---

## Debugging

Select the `Grid` object in the editor:

- The **green box** shows the map bounds.
- Turn on **Draw Cell States** to show blocked cells in red and occupied cells in yellow.
