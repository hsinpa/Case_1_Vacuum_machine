# Example Character: Plan

A new, experimental top-down character sprite with **idle and walk cycles in 4 directions**. It is designed from scratch, and no earlier character art is used as a reference.

> **Base rules:** everything here follows [`../README.md`](../README.md), the project art rules. The main one is that **only the 52 palette colours listed there may be used**. If this plan and the base rules disagree, the base rules win.

Status: **planning.** No pixels have been drawn yet.

---

## 1. Unity context

| Source | What it tells us |
|---|---|
| Unity **Aseprite Importer** | `.aseprite` files are imported directly. With *Generate Animation Clips* on, **Aseprite tags become Unity animation clips**, so tag names matter. |
| `Art_assets/Animation/` | The Animator already has 8 states: `character_idle_{front,back,left,right}` and `character_move_{bottom,top,left,right}`. |
| `Feature/Character/PlayerMoveInput.cs` | The Animator is driven by `Axis_X`, `Axis_Y` and `Speed` (0 = idle, 1 = move). |

---

## 2. Brief

| Item | Decision |
|---|---|
| Subject | **A man in a dark blue suit wearing sunglasses.** Cool and composed: he moves with calm confidence and never takes the shades off |
| Must read at 1× | The sunglasses (a dark band across the face with a 1 px glint), the dark suit block with its white shirt V and red tie, and short slicked-back dark hair |
| Canvas | **48 × 48 px** |
| Colour mode | RGB with the project palette loaded. `validate` must report 0 off-palette pixels |
| View | **Top-down RPG, camera ≈ 60° from the ground,** following the style reference supplied by the user (a slim, tall chibi in a classic top-down RPG). The top of the head is visible, and the face sits in the lower half of the head |
| Style (from the reference) | Tall, slim chibi. The head is ≈ 43 % of the height, the shoulders are no wider than the head, thin arms hang close to the body with small hands, and short thin legs stand close together. Heavy dark outline everywhere and flat colours with minimal shading |
| Proportions | The figure is ≈ 44 px tall (y = 2 → 45): head 19 px including hair (rows 2–20), torso 14 px (21–34), legs and shoes 11 px (35–45). Head 16 px wide (x 16–31). The full silhouette, arms included, is 18 px wide (x 15–32) |
| Placement | Feet on row **y = 45**, centred between x = 23 and 24. **No ground shadow.** At least 2 px of side margin is left for arm swing |
| Light | From the top-left. Shadow side is bottom-right |
| Outline | Heavy 1 px dark outline all round, as in the reference: 11 `#1a1f2e` around the suit and shoes, and 47 `#2b222a` around the hair, face and hands. No pure black |
| Shading | 2–3 tones per material (shadow, base, highlight). No dithering and no anti-aliasing |
| Pivot | Bottom-centre (x = 24, y = 45, at the feet) |

### Palette ramps (index numbers from `../README.md`)

| Material | Highlight | Base | Shadow | Outline |
|---|---|---|---|---|
| Skin | 31 `#edaaa3` | 32 `#eecb90` | 43 `#b77862` | 47 `#2b222a` |
| Hair (short, slicked back) | 45 `#533b41` | 46 `#3f333b` | 47 `#2b222a` | 47 `#2b222a` |
| Suit jacket and trousers (dark blue) | 14 `#3d80a3` | 13 `#2f4c6c` | 12 `#1e314b` | 11 `#1a1f2e` |
| Shirt (white) | 17 `#e5efef` | 17 `#e5efef` | 18 `#bac9cd` | — |
| Tie (accent) | — | 37 `#c33c40` | 38 `#8d3649` | — |
| Sunglasses | 17 `#e5efef` (1 px glint) | 11 `#1a1f2e` | — | 11 `#1a1f2e` |
| Shoes | 21 `#414453` | 47 `#2b222a` | 47 `#2b222a` | 11 `#1a1f2e` |

The tie is an addition for colour contrast, not part of the original request. Drop it if it isn't wanted.

---

## 3. File and layer structure

One file holds every direction and cycle:

```
example/td_chara_example.aseprite
```

Layers, from bottom to top:

| Layer | Purpose |
|---|---|
| `leg_back` | The leg further from the viewer |
| `leg_front` | The leg nearer to the viewer |
| `body` | Torso and clothing |
| `arm_back` / `arm_front` | Arms that swing in the walk cycle |
| `head` | Face and eyes |
| `hair` | Kept separate so the hair can bob a frame later than the head |

The layers exist so animation can **move cels instead of redrawing pixels**. On import the layers are merged per frame.

---

## 4. Animation plan

### Tags, named to match the existing Unity Animator states

| Aseprite tag | Animator state | Frames | Timing |
|---|---|---|---|
| `idle_front` | `character_idle_front` | 2 | 500 / 250 ms |
| `idle_back` | `character_idle_back` | 2 | 500 / 250 ms |
| `idle_left` | `character_idle_left` | 2 | 500 / 250 ms |
| `idle_right` | `character_idle_right` | 2 | 500 / 250 ms |
| `move_bottom` | `character_move_bottom` | 3 (ping-pong) | 150 ms each |
| `move_top` | `character_move_top` | 3 (ping-pong) | 150 ms each |
| `move_left` | `character_move_left` | 3 (ping-pong) | 150 ms each |
| `move_right` | `character_move_right` | 3 (ping-pong) | 150 ms each |

Total: **20 frames** (8 idle + 12 walk, of which 4 walk frames are linked copies of idle frames). Idle tags loop forward, walk tags loop ping-pong.

### Idle (breathing, 2 frames)

1. **Rest (500 ms):** the key pose, shoulders square.
2. **Down (250 ms):** head, hair, body and arms drop 1 px. Legs unchanged. The loop then returns to frame 1.

A third "recover" frame (arms lagging 1 px low) was dropped because it reads the same as the rest pose at this size.

> **Rule: the hair always moves together with the head.** If the hair lags behind the face, the bangs slide over it and the hair reads as a hat.

The long hold on the rest pose is deliberate. Even timing looks like a metronome.

### Walk (3 frames, ping-pong)

Each `move_*` tag holds 3 frames and plays **ping-pong**, so it gives a 4-beat cycle: step A → pass → step B → pass.

1. **Step A (150 ms):** one leg planted, the other lifted 2 px. Head, hair and body drop 1 px. The arm opposite the planted leg swings forward (+2 px), and the other arm stays put.
2. **Pass (150 ms):** the idle **rest** pose. Its cels are **linked** to the idle frame 1 of the same direction, so it is the same image and edits to one apply to both.
3. **Step B (150 ms):** mirror of step A, with the other leg lifted.

A frame cannot sit in two tags that need different neighbours, so the pass frame is a linked copy rather than the idle frame itself.

**Front and back:** the legs step up and down. The lifted leg moves up 2 px, and the forward arm swings down 2 px.

**Left and right:** the legs stride sideways. In step A the near leg moves 2 px forward and the far leg 2 px back. In step B the near leg is 2 px back and the far leg 2 px forward. The far leg always sits 1 px higher (its foot on row 45), so A and B don't read the same. The near arm swings 1 px opposite the near leg.

At 48 px the legs have room for a 2–3 px stride. For front and back, the legs alternate stepping up and down instead of sideways. For left, draw `right` first and then flip it horizontally, fixing any details that must not mirror (for example the hair parting).

---

## 5. Production order

Each step ends with `look` before the next one starts, so mistakes are caught while they are cheap to fix.

1. **Confirm the brief.** Settle the open questions in §8.
2. **Concept.** Generate the concept sheet and walk storyboard from §9. Import them as a reference layer, and remove that layer before export.
3. **Set up the document.** Create the 48×48 RGB file, load the palette from `../README.md`, and create the layers from §3.
4. **Front key pose.** Silhouette → flat colours → shading → outline. Check the silhouette at 1× and confirm the character reads.
5. **Side (right) and back key poses.** Keep the head size, eye line and foot row identical to the front pose.
6. **Rig check.** Make sure each limb sits on its own layer and nothing is baked together.
7. **Idle cycles** for 4 directions, then **walk cycles** for 4 directions. Review each with `look` op `filmstrip` and `onion`.
8. **Tags and timing**, as in §4.
9. **Review:** `validate` (palette, alpha, orphan pixels) plus a pixel-critic pass. Fix every issue found.
10. **Unity hookup.** Save the file, let the Aseprite Importer generate the clips, and point the 8 Animator states at them. Check that `Axis_X`, `Axis_Y` and `Speed` play the right clip in Play Mode.

---

## 6. Definition of done

- [ ] 0 off-palette pixels and 0 semi-transparent pixels (`validate`)
- [ ] Silhouette reads at 1× in all 4 directions
- [ ] Head size, eye line and foot row are consistent across all 20 frames
- [ ] No reference layer is left in the saved file
- [ ] All 8 tags exist with the names from §4
- [ ] Walk and idle loops have no pop at the loop point
- [ ] The character plays correctly in the Unity Animator in Play Mode

---

## 7. Out of scope (for now)

Run, attack, interact and vacuum-use animations, and diagonal directions. These can be added later as extra tags in the same file.

---

## 8. Decisions

| Question | Answer |
|---|---|
| Who is the character? | A man in a dark blue suit wearing sunglasses |
| Idle length | 2 frames (changed from 3) |
| Concept first? | Yes. See §9 |
| Tie? | Proposed as a red accent. Not yet confirmed |

---

## 9. Concept references

The image model decides the **design** (look, proportions, poses). The pixel art is then drawn by hand over that reference. The reference is never downscaled and used as the pixels.

Save the generated images in this folder:

- `example/concept_sheet.png`: the front, right-side and back views
- `example/storyboard_walk.png`: the 4 walk frames, side view

### Prompt A: concept sheet

```
Character: girl in a black dress with a red sash, front-facing idle.

Hair (rows 2–30):
- Long, straight, ash-beige hair reaching the chest, about row 30.
- Rounded cap on top, 22 px wide, slightly wider than the face.
- Straight bangs ending just above the eyes, with one longer
  strand hanging down the center between the eyes.
- Two side locks 3–4 px wide fall in front of the shoulders and
  frame the face; the rest falls behind the body.
- Base #B8A088, shadow #8F7868 at the edges and under the bangs,
  outline #5A4838.

Face (rows 10–20):
- Very pale, almost white: #F0E8E0, with no shading.
- Eyes: two dark vertical dots, 2 px wide x 3 px tall, #2A2228,
  about 6 px apart, set low on the face.
- No nose, mouth, eyebrows or blush.

Body (rows 22–46):
- Black long-sleeved dress, knee length: base #24202A, lighter
  fold tone #3A3440, used sparingly.
- Small pale V at the neckline (2–3 px, skin color).
- Slim torso, 12 px wide. Arms hang straight at the sides, 3 px
  wide, with 2 px pale hands showing at about rows 35–36.
- Red sash: wraps the waist at rows 31–33, then drapes diagonally
  down across the skirt to about row 40, getting wider toward the
  bottom, like a tied cloth with a hanging tail. Base #C82828,
  shadow #8A1818. It is the only saturated color on the sprite.
- Skirt flares slightly, 14 px wide at the hem (row 40).
- Legs: thin, dark tights (#2A2630), rows 41–45, with a 1–2 px gap.
- Shoes: black, row 46, 1 px tall.

Overall: muted and desaturated except the red; 10–12 colors total;
1 px dark outline; flat fills with one shadow tone at most.
```
