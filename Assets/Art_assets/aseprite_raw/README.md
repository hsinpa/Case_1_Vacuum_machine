# Aseprite Art Rules

Rules for every `.aseprite` file in this folder. Read the linked documents before drawing or editing anything here.

| Document | What it covers |
|---|---|
| [palette.md](palette.md) | **Mandatory.** The 52-colour project palette, the rules for using it, and the raw palette for importing into Aseprite |
| [character_design.md](character_design.md) | Character sprite rules: canvas, proportions, face, hair, shading, layers, file/tag naming, idle and walk animation, and the done checklist |

## Adding frames: prefer appending

When possible, add new frames at the end of the timeline. Inserting or deleting frames in the middle of a file Unity has already imported has a cost, explained below. Avoid it when you can, and when the change needs it, go ahead and do the cleanup steps afterwards.

### Why

Unity's Aseprite Importer stores one sprite ID per frame in the file's `.meta`. When the file is reimported, each frame gets its sprite ID **by its position in the importer's list, not by its frame number**. A sprite's name (`Frame_N`) is set once, when the ID is first created, and is never updated.

Inserting or deleting a frame in the middle shifts the frames after it:

- an existing sprite ID now holds **a different frame's image**, but keeps its old name. So `Frame_1` can show frame 7, and two sprites can share a name.
- anything that references a sprite (Sprite Library entries, SpriteRenderers, prefabs) points at the ID, so it **silently shows the wrong image**. Unity gives no error.

New frames at the end get fresh IDs, and every existing frame keeps its ID and image.

### When you do restructure the timeline

1. Remove the sprite data from the `.meta`. Set `animatedSpriteImportData: []` and `asepriteLayers: []`, and leave the rest of the file alone.
   - Deleting the whole `.meta` also works, but it gives the file a new GUID and resets its import settings (Pixels Per Unit 25, Point filter, bottom pivot, merged layers). You'd then have to set those again and fix every reference to the file.
2. Reimport. Every sprite gets a new ID, named `Frame_N` after its frame.
3. Re-point everything that references the sprites: the Sprite Library (`raw_tiles/demo_character/*.spriteLib`) and any SpriteRenderer in scenes or prefabs. Match them by frame number.
4. Play each animation clip and check that each frame shows the right image.
