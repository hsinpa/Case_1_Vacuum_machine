# Character Sprites

Rules for **every** character in a classic top-down RPG style: a tall, slim chibi with a big head, seen from slightly above. Reference sprite: `example/td_chara_girl.aseprite`. Drawing rules (§1–§5) were worked out on the front view; animation (§7–§8) covers front, back and side. All colours come from [palette.md](palette.md).

## 1. Canvas

| Rule | Value |
|---|---|
| Canvas | **48 × 48 px**, RGB, project palette loaded |
| Figure | ≈ 45 px tall: hair top ≈ row 2, shoes on **row 46**, row 47 empty |
| Centre | Between **x = 23 and 24**. Symmetric parts use even widths |
| Ground shadow | **None** |

## 2. Proportions (front)

Rows are for a normal-height figure; keep the ratios for shorter or taller ones.

| Part | Rows (≈) | Width | Notes |
|---|---|---|---|
| Hair / head | 2 – 21 | Hair cap ≈ 20–22 px | Head ≈ **45 %** of figure height; cap slightly wider than face |
| Face | 14 – 21 visible | ≈ 14–16 px | Lower half of the head, under the bangs |
| Torso | 22 – 33 | ≈ 12 px | Slim, **never wider than the head** |
| Lower garment | 34 – 40 | 12–14 px | Skirt may flare slightly at the hem |
| Legs | 41 – 45 | ≈ 3 px each | Thin, 1–2 px gap |
| Shoes | 46 | — | 1 px tall, dark |
| Arms | 23 – 37 | ≈ 3 px | Straight down, close to the body; 2 px hands at rows ≈ 35–36 |

## 3. Face and hair

- **Face shape:** wide and **square** — straight sides, flat bottom, only the **two bottom corners softened 1 px**. Never oval or V-chinned.
- **Skin:** one flat tone, no shading.
- **Eyes:** dark, **2 × 3 px**, ≈ **6 px apart**, 2–3 rows under the bangs.
- **Mouth:** **always present** — a muted **2 px horizontal line** on the row above the chin line.
- **No** nose, eyebrows or blush.
- **Chin line** uses the same colour as the adjacent outline (usually the hair outline), so the face edge reads as one line.
- **Hair:** rounded cap; bangs end **just above the eyes**; side locks frame the face. Long hair: locks fall **in front of the shoulders**, the rest goes on `hair_back`. 3 tones: base, shadow (right side and under bangs), dark outline from the same hue.

## 4. Outline, colour and shading

- **Outline:** 1 px around the silhouette, never pure black — `#1a1f2e` (11) for clothing, legs and shoes; darkest hair tone for hair and face edge.
- **Shading:** flat fills, **one shadow tone** per material (plus an optional, sparing fold highlight). No gradients, dithering or anti-aliasing.
- **Light:** top-left; shadows go right and underneath.
- **Colours:** ≈ **10–12** per character, muted, with **one saturated accent** (e.g. a red sash or tie).
- **Off-palette spec colours:** map each to the **nearest palette entry** and list the mapping. Never add them to the palette.

## 5. Layers

One part per layer, bottom to top. No reference or sketch layer may remain in a saved file.

| Layer | Contents |
|---|---|
| `hair_back` | Long hair behind the body (long hair only) |
| `leg_back` | Leg further from the viewer |
| `leg_front` | Leg nearer the viewer |
| `body` | Torso, clothing, lower garment, accessories (sash, tie) |
| `arm_back` | Arm on the viewer's right |
| `arm_front` | Arm on the viewer's left |
| `head` | Face, eyes, mouth, chin line |
| `hair` | Cap, bangs, side locks in front of the shoulders |

## 6. Files and tags

- One file per character: `td_chara_<name>.aseprite`, every direction as tags.
- Tags (each becomes the Unity clip for the matching Animator state, e.g. `character_idle_front`): `idle_front`, `idle_back`, `idle_left`, `idle_right`, `move_bottom`, `move_top`, `move_left`, `move_right`. All loop **forward**.
- **Right-facing tags are optional:** mirror the left frames (Unity flips them); don't draw separate ones.

## 7. Animation rules (all directions)

- **Move cels, never redraw pixels.** Every frame is the rest pose with some layers offset, so volume can't drift and frames can be regenerated. Duplicate frames as **unlinked** copies — linked cels share position, so moving one moves the rest pose too.
- **Hair always moves with the head**, or the bangs slide over the face and read as a hat.
- **Uneven timing:** hold the rest/pass pose longer; equal durations look mechanical.

### Idle — 2 frames per direction

| Frame | Duration | Motion |
|---|---|---|
| 1. Rest | 500 ms | Base pose |
| 2. Dip | 250 ms | Head, hair, body, arms **down 1 px**; legs still |

A third "recover" frame (arms still 1 px low) was dropped: at 48 px it reads the same as rest.

## 8. Walk — 8-frame cycle

### 8.1 Method

1. The idle rest frame is the **pass pose (P)**: legs together, arms at rest.
2. Duplicate it 7 times (unlinked) and apply the offsets below (`+dy` = down, `−dx` = toward facing direction).
3. Tag the 8 frames, direction **forward** (not ping-pong — the 8 frames already loop).

Shared rules:

- **Body bob:** on step frames (A, B), `body`, `head`, `hair`, `hair_back` move **down 1 px**.
- **In-betweens move half as far as the step**, so no part jumps more than 1–2 px per frame.
- **Arms swing opposite the legs.**
- **Legs stay on the ground line:** a lifted foot goes up (`−dy`); legs never bob down.
- **Timing:** step **100 ms**, in-between **70 ms**, pass **90 ms** → **660 ms** per cycle. For a different in-game speed, scale all eight by the same factor so feet don't slide.

### 8.2 Front (`move_bottom`) and back (`move_top`)

Legs step up and down in place. Same offsets for both; the back view just has no `head` or `hair_back` cels.

Order: **P → mB → B → mB → P → mA → A → mA**

| Frame | `leg_back` | `leg_front` | `arm_back` | `arm_front` | body, head, hair, hair_back |
|---|---|---|---|---|---|
| P | — | — | — | — | — |
| mB | — | dy −1 | — | dy +1 | — |
| B | — | dy −2 | — | dy +2 | dy +1 |
| mB | — | dy −1 | — | dy +1 | — |
| P | — | — | — | — | — |
| mA | dy −1 | — | dy +1 | — | — |
| A | dy −2 | — | dy +2 | — | dy +1 |
| mA | dy −1 | — | dy +1 | — | — |

### 8.3 Side (`move_left`)

Legs stride front to back; only `arm_front` is visible.

- **Near/far legs use different shades:** `leg_front` `#3f333b` (lighter), `leg_back` `#2b222a` (darker). Otherwise A and B look identical.
- **Legs really swap:** near leg forward in A, far leg forward in B.
- **The swinging leg lifts 1 px** (far leg in A → B, near leg in B → A). So P1 and P2 differ and are separate copies.

Order (facing left, `−dx` = forward): **A → m1 → P1 → m2 → B → m3 → P2 → m4**

| Frame | `leg_front` (near) | `leg_back` (far) | `arm_front` | body, head, hair |
|---|---|---|---|---|
| A | dx −2 | dx +2 | dx +1, dy +1 | dy +1 |
| m1 | dx −1 | dx +1, dy −1 | dx +1 | — |
| P1 | — | dy −1 | — | — |
| m2 | dx +2 | dx −2, dy −1 | dx −1 | — |
| B | dx +4 | dx −4 | dx −1, dy +1 | dy +1 |
| m3 | dx +2, dy −1 | dx −2 | dx −1 | — |
| P2 | dy −1 | — | — | — |
| m4 | dx −1, dy −1 | dx +1 | dx +1 | — |

Why ±4 in B but ±2 in A: the rest pose already has the near leg 2 px ahead, so B needs ±4 to match A's spread (≈ 6 px with 3 px legs).

## 9. Done checklist

- [ ] `validate`: 0 off-palette, 0 semi-transparent pixels
- [ ] Silhouette reads at 1×: big head, slim body, arms close
- [ ] Face per §3; no ground shadow
- [ ] Layers per §5; no reference layer
- [ ] `idle_*`: 2 frames, 500 / 250 ms, hair moves with head
- [ ] `move_*`: 8 cel-offset frames, forward loop, no jump at loop point
- [ ] Side view: near/far legs shaded differently; A and B look different
