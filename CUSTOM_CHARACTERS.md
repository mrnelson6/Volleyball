# Making a character for Animal Volleyball

Every character in the game is currently drawn by code. This is the path for art drawn by a
human instead: you fill in one template PNG, and the game imports it as a complete character.

Nothing here needs Unity — the artist only ever touches a single PNG.

---

## Part 1 — For the artist

### Getting the template

Ask for the template PNG. The default is `character_template_192x256.png` — **888 × 2040
pixels**, holding two grids:

- **EXAMPLE** (top) — a finished character in all 11 poses. This is what's needed, and it's
  wearing the magenta shirt you're being asked to use. **Don't draw here**; it's ignored on
  import. Copying a cell down into your own grid as a starting point is fine and encouraged.
- **DRAW YOUR CHARACTER IN THESE CELLS** (bottom) — your 11 cells, plus a READ ME reminder.

Each cell is **192 wide × 256 tall**. There's a faint blue silhouette of the pose in each one
showing the proportions and where the body sits — draw over it, it disappears on import.

### The rules

1. **Draw inside the cells only.** Don't move them, don't resize them, don't resize the sheet.
   The importer reads those exact rectangles.
2. **Feet on the ground line** — the dotted yellow line near the bottom of each cell. A
   character floating above it will float in-game too.
3. **Keep the background transparent.** Don't flood-fill it, don't flatten onto white.
4. **Shirt in magenta.** Fill the jersey/shirt with magenta (`#FF00FF`) — like the example does.
   The game swaps it for each team's colour (blue, cyan, red, orange), so one drawing serves all
   four players. Shade with lighter/darker magenta and the shading survives the swap. **Don't
   use magenta anywhere else** (not on skin, hair, or shorts) or it'll change colour on court.
5. **Face the camera, one direction only.** The game mirrors the sprite when the character turns
   around, so draw them front-facing (or facing right) and never draw a left-facing version.
6. **Don't recolour the guides.** The faint blue silhouette, the dotted ground line and the
   centre line are removed automatically by matching their exact colours. Draw over them freely;
   just don't fill something important with those same two blues.

Crisp pixel edges suit the game best (it renders with no texture smoothing), but soft shading
and partial transparency do survive the import if you want them.

### The 11 poses

Left to right, top to bottom:

| Cell | Pose | Used when |
|---|---|---|
| `IDLE` | standing, arms down | not moving |
| `RUN0` | run cycle, one leg forward | running — alternates with RUN1 |
| `RUN1` | run cycle, other leg forward | running |
| `JUMP` | legs tucked, arms up | in the air |
| `SWING` | hitting arm straight overhead | spiking and serving |
| `BUMP` | knees bent, both arms in a low platform | digging a low ball |
| `SET` | both hands raised in front of the forehead | setting overhead |
| `BLOCK` | both arms straight up above the head | blocking at the net |
| `DIVE` | superman layout, arms stretched past the head | diving sideways |
| `DIVEUP` | lying flat, seen from behind (their back, soles of feet) | diving away from camera |
| `DIVEDOWN` | lying flat, seen from the front (their face, hands nearest) | diving toward camera |

Two things worth knowing:

- **`DIVE` is drawn upright but used sideways.** Draw it as if they're standing and reaching
  straight up; the game rotates the whole sprite 90° during the slide, so "up" becomes the
  direction they dive. Head and arms at the top of the cell.
- **`DIVEUP`/`DIVEDOWN` are foreshortened** — a body lying on the sand pointing away from or
  toward the viewer, squashed. These are the two hardest cells; follow the example closely. If
  you leave them blank the character still works, they'll just look wrong on depth-wise dives.

### File format

- **PNG**, 32-bit RGBA (i.e. with an alpha channel). Nothing else — no JPG (it destroys hard
  pixel edges), no PSD, no flattened export.
- **Save at the size you were given (888 × 2040).** Don't scale the file — zoom in your editor
  instead.
- Good free tools: **Piskel** (piskelapp.com, runs in a browser), **LibreSprite**,
  **GraphicsGale**, **Krita**. Paid: **Aseprite** (the standard for this). In Photoshop/GIMP:
  pencil tool, anti-aliasing off, nearest-neighbour for any transform.

Send back the finished PNG. That's it.

---

## Part 2 — Importing it (project side)

**Export a template:** `Volleyball → Custom Characters → Save Template Sheet` (4× detail,
height 1.0), or `Import Sprite Sheet...` for a window with height and detail controls.

- **Height stat** — taller characters need a taller cell. Export at the height you intend to
  give them; the importer prints the exact value to use.
- **Detail** — 1× is the game's native 48×64, which is genuinely uncomfortable to draw by hand.
  4× (192×256) is the default. This changes only how many pixels the artist gets: imported
  sprites are given `34 × detail` pixels-per-unit, so the character occupies exactly the same
  space on court at any detail level. Higher detail will look sharper than the procedural
  animals standing next to it.

**Import a finished sheet:** same window → pick the PNG → type a character id (lowercase
letters/digits, e.g. `otter`) → **Import**. It:

- recovers the detail factor from the sheet's width (no need to tell it),
- slices the 11 drawing cells, stripping the guide colours,
- recolours the magenta shirt into all 4 jersey colours (shading preserved),
- writes 44 sprites to `Assets/Resources/CustomCharacters/` with the importer settings the game
  expects (point filter, uncompressed, scaled px/unit),
- warns about blank cells, floating art, and art clipped at the top edge,
- prints a `CharacterDef` snippet to paste into `CharacterRoster.All`.

**Make it playable:** paste that snippet into `CharacterRoster.All` in
`Assets/Scripts/Player/CharacterDef.cs` and tune the stats. The `height` must stay as printed —
it's derived from the cell height, and a mismatch makes the character's feet sink or float.
`fur`/`furAccent`/`art` only feed the procedural baker and are ignored while custom art exists.

### Notes

- Custom art lives in `Resources/CustomCharacters/`, deliberately **not** the procedural bake
  cache (`Resources/Characters/`) — that folder gets wiped wholesale on an art-version bump and
  by `Force Rebake Character Sprites`, which would eat the artist's work. Custom frames win over
  the bake for a given id, and the baker skips ids that have them.
- A **new** id needs no scene regeneration; character select loads it at runtime.
- Reusing an **existing** roster id (e.g. `fox`) overrides that animal's look — the scenes have
  the old sprites serialised in, so re-run `Volleyball → Build World Tour (Everything)` and
  commit the scenes.
- A partial import falls back to the procedural look rather than mixing the two: it's all 11
  frames or nothing.
