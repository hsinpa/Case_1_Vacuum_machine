# Character Sprites: Front View

These rules apply to **every** character. They come from the front-view experiment in `example/`, and `example/td_chara_girl.aseprite` is the approved reference sprite. Only the **front view** has been worked out so far. Side and back views will be added here once they are decided.

The style is a classic top-down RPG: a tall, slim chibi with a big head, seen from slightly above.

All colours must come from the project palette, see [palette.md](palette.md).

## 1. Canvas and placement

| Rule | Value |
|---|---|
| Canvas | **48 × 48 px**, RGB, with the palette from [palette.md](palette.md) loaded |
| Figure height | ≈ 45 px, from the top of the hair (row ≈ 2) to the shoes (row 46) |
| Ground line | Shoes on **row 46**. Row 47 stays empty |
| Horizontal centre | Between **x = 23 and x = 24**. Symmetric parts use even widths so they centre exactly |
| Ground shadow | **None.** Do not draw a shadow under the feet |

## 2. Proportions (front)

Row numbers are a guide for a figure of normal height. Keep the ratios when a character is shorter or taller.

| Part | Rows (≈) | Width | Notes |
|---|---|---|---|
| Hair / head | 2 – 21 | Hair cap ≈ 20–22 px | The head is **≈ 45 % of the figure height**. The hair cap is slightly wider than the face |
| Face | ≈ 14 – 21 visible | ≈ 14–16 px | Sits in the **lower half of the head**, under the hair or bangs |
| Torso | 22 – 33 | ≈ 12 px | **No wider than the head.** Slim |
| Lower garment (skirt / trousers / coat hem) | 34 – 40 | 12–14 px | A skirt may flare slightly toward the hem |
| Legs | 41 – 45 | ≈ 3 px each | Thin, close together, with a 1–2 px gap |
| Shoes | 46 | — | 1 px tall, dark |
| Arms | 23 – 37 | ≈ 3 px | Hang **straight down, close to the body**. Small 2 px hands at about rows 35–36 |

## 3. Face

- **Shape:** wide and **square**, with straight sides and a flat bottom. Only the **two bottom corners are softened by 1 px**. Never taper the face into an oval or V chin.
- **Skin:** flat, a single tone with **no shading** on the face.
- **Eyes:** two dark dots, **2 px wide × 3 px tall**, about **6 px apart**, set low on the face (about 2–3 rows under the bangs).
- **Mouth:** **always present.** It is a simple **2 px horizontal line** in a muted colour, on the row just above the chin line.
- **No** nose, eyebrows or blush.
- **Outline continuity:** the chin line uses the same colour as the outline next to it (usually the hair outline), so the face edge reads as one line and not as stray dots.

## 4. Hair

- A rounded cap on top. The hairline or bangs end **just above the eyes**.
- Side locks frame the face. For long hair, the locks fall **in front of the shoulders** and the rest goes on a separate layer **behind the body**.
- Use 3 tones: base, one shadow tone (on the right / shadow side and under the bangs), and a dark outline from the same hue family.

## 5. Outline, colour and shading

- **Outline:** 1 px, dark, all round the silhouette. Use `#1a1f2e` (11) for clothing, legs and shoes, and the darkest tone of the hair ramp for the hair and face edge. Never pure black.
- **Shading:** flat fills, with **at most one shadow tone** per material (plus an optional lighter fold tone used sparingly). No gradients, no dithering, no anti-aliasing.
- **Light:** from the top-left, so shadow tones go on the right side and the underside.
- **Colour count:** about **10–12 colours** per character.
- **One accent:** keep the character muted, and give it **one saturated accent** (for example a red sash or tie) as the only strong colour.
- **Off-palette specs:** if a design spec names hex colours that are not in [palette.md](palette.md), map each one to the **nearest palette entry** and list the mapping. Never add the spec colour to the palette.

## 6. Layers

Each body part goes on its own layer, so animation moves cels instead of redrawing pixels. From bottom to top:

| Layer | Contents |
|---|---|
| `hair_back` | Long hair behind the body (only if the hair is long) |
| `leg_back` | The leg further from the viewer |
| `leg_front` | The leg nearer to the viewer |
| `body` | Torso, clothing, the lower garment and accessories such as a sash or tie |
| `arm_back` | The arm on the viewer's right |
| `arm_front` | The arm on the viewer's left |
| `head` | Face, eyes, mouth and the chin line |
| `hair` | Hair cap, bangs and the side locks in front of the shoulders |

No reference or sketch layer may be left in a saved file.

## 7. Idle animation (front)

Use **2 frames** per direction, tagged **`idle_front`**, **`idle_back`**, **`idle_left`** and **`idle_right`** (each tag becomes the Unity clip that feeds the matching Animator state, for example `character_idle_front`). The tags loop forward, forever.

| Frame | Duration | Motion |
|---|---|---|
| 1. Rest | 500 ms | The base pose |
| 2. Dip | 250 ms | Head, hair, body and arms all move **down 1 px** together. The legs stay still |

A third "recover" frame (arms still 1 px low) was tried and dropped: at 48 px it reads the same as the rest pose.

- **The hair always moves together with the head.** If the hair lags behind the face, the bangs slide over it and the hair reads as a hat.
- Animate by **moving cels** (`dy = ±1`), never by redrawing pixels, so the frames cannot drift apart.
- Hold the rest pose longest. Even timing looks mechanical.

## 8. File naming

- `td_chara_<name>.aseprite`, one file per character, holding every direction as tags.
- Planned tag names, which match the existing Unity Animator states: `idle_front`, `idle_back`, `idle_left`, `idle_right`, `move_bottom`, `move_top`, `move_left`, `move_right`.

## 9. Checklist before a character is called done

- [ ] `validate` reports 0 off-palette and 0 semi-transparent pixels
- [ ] The silhouette reads at 1×: big head, slim body, arms close to the sides
- [ ] The face is square with softened corners, has 2×3 eyes and a 2 px mouth line, and no shading
- [ ] There is no ground shadow
- [ ] The parts are on the layers listed in §6, and there is no reference layer
- [ ] Each `idle_*` tag has 2 frames at 500 / 250 ms, and the hair moves with the head
