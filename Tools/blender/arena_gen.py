"""Procedural themed arenas for Animal Volleyball's 3D toon overhaul.

Each theme = a 16-colour palette (same slot MEANINGS as props_gen.py's beach palette, so shared
meshes such as rocks, terrain and the court recolour per biome for free), a terrain shape, an
optional sea/river on the far side (Blender -X = away from the broadcast camera), a distant
backdrop ring, and its own props. Output: Assets/Art/Arenas/<Theme>/{palette.png, prop_*.fbx}.

Usage (headless):
  blender -b --factory-startup -P Tools/blender/arena_gen.py -- export savanna amazon sahara arctic
  blender -b --factory-startup -P Tools/blender/arena_gen.py -- export all
"""
import math
import os
import random
import sys

import bmesh
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import props_gen as pg  # noqa: E402  (mesh helpers + beach props; its main() doesn't run on import)
from props_gen import (SAND, SAND_D, TRUNK, LEAF, LEAF_D, RED, WHITE, WOOD, ROCK, WATER,  # noqa: E402
                       YELLOW, BLUE, NET, TEAL, ORANGE, WET)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_ROOT = os.path.join(ROOT, "Assets", "Art", "Arenas")


# ---------------------------------------------------------------- terrain / water / backdrop

def terrain(amp, sea, bumps=0.12):
    """Flat pad matching the scoring ground sheet (|x|<16, |y|<22), relief beyond it; with `sea`
    the land slopes into water on the far side (-X)."""
    def height(x, y):
        dx = max(0.0, abs(x) - 16.0)
        dy = max(0.0, abs(y) - 22.0)
        outside = math.hypot(dx, dy)
        dune = (0.7 * math.sin(0.33 * x + 1.3) * math.cos(0.27 * y) + 0.45 * math.sin(0.55 * y + 0.2 * x)
                + 0.5 * noise.noise(Vector((x * bumps, y * bumps, 0.7))))
        h = pg.smoothstep(0.0, 6.0, outside) * (dune * amp + 0.4 + outside * 0.06 * amp)
        if sea:
            s = max(0.0, -x - 18.0)
            h = h * (1.0 - pg.smoothstep(0.0, 8.0, s)) - s * 0.09
        return h

    p = pg.Prop("terrain")
    x0, x1, y0, y1 = -34.0, 34.0, -40.0, 40.0
    nx, ny = 46, 52
    grid = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            x = x0 + (x1 - x0) * i / nx
            y = y0 + (y1 - y0) * j / ny
            grid[i, j] = p.bm.verts.new((x, y, height(x, y)))
    for i in range(nx):
        for j in range(ny):
            a, b, c, d = grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]
            for tri in ((a, b, c), (a, c, d)) if (i + j) % 2 == 0 else ((a, b, d), (b, c, d)):
                f = p.bm.faces.new(tri)
                cc = f.calc_center_median()
                if sea and cc.z < -0.35:
                    slot = WET
                else:
                    nval = noise.noise(Vector((cc.x * 0.21, cc.y * 0.21, 3.1)))
                    slot = SAND_D if nval > 0.25 else SAND
                p.paint([f], slot, smooth=False)
    return p.finish()


def floes():
    """Ice floes drifting on the sea (Arctic)."""
    rng = random.Random(5)
    p = pg.Prop("floes")
    for k in range(26):
        x = rng.uniform(-110, -24)
        y = rng.uniform(-90, 90)
        r = rng.uniform(1.5, 5.0)
        p.cone((x, y, -0.6), (x, y, -0.2), r, r * 0.9, WHITE if k % 3 else TEAL, segs=rng.choice((5, 6, 7)), smooth=False)
    return p.finish()


def backdrop(kind):
    """A ring of big distant shapes behind the far side of the court (-X hemisphere)."""
    rng = random.Random(len(kind) * 7)
    p = pg.Prop("backdrop")
    n = 16
    for k in range(n):
        a = math.radians(-85 + 170 * k / (n - 1)) + rng.uniform(-0.05, 0.05)
        r = rng.uniform(95, 140)
        x, y = -math.cos(a) * r, math.sin(a) * r
        s = rng.uniform(0.7, 1.4)
        if kind == "mesas":  # flat-topped savanna hills
            w = rng.uniform(10, 22) * s
            h = rng.uniform(5, 10) * s
            p.cone((x, y, -1), (x, y, h), w, w * 0.7, TEAL if k % 2 else ROCK, segs=9, smooth=False)
        elif kind == "jungle":  # a wall of canopy
            h = rng.uniform(14, 24) * s
            p.cone((x, y, -1), (x, y, h * 0.6), 2.0, 1.5, TRUNK, segs=6)
            for b in range(3):
                p.ellipsoid((x + rng.uniform(-4, 4), y + rng.uniform(-4, 4), h * 0.6 + b * 3),
                            (rng.uniform(8, 12), rng.uniform(8, 12), rng.uniform(5, 7)),
                            LEAF_D if (k + b) % 2 else LEAF, segs=(9, 6), smooth=False)
        elif kind == "pyramids":
            if k % 3 == 0:
                w = rng.uniform(16, 28)
                p.cone((x, y, -1), (x, y, w * 0.95), w, 0.3, SAND_D, segs=4, smooth=False)
            else:  # far dunes between them
                p.ellipsoid((x, y, -2), (rng.uniform(14, 26), rng.uniform(10, 18), rng.uniform(4, 8)), SAND, segs=(10, 6))
        elif kind == "peaks":  # snowy mountains / icebergs
            h = rng.uniform(18, 34) * s
            w = h * rng.uniform(0.6, 0.9)
            p.cone((x, y, -1), (x, y, h), w, 0.5, ROCK, segs=6, smooth=False)
            p.cone((x, y, h * 0.62), (x, y, h + 0.3), w * 0.40, 0.2, WHITE, segs=6, smooth=False)
    return p.finish()


# ---------------------------------------------------------------- savanna

def acacia(seed):
    rng = random.Random(seed)
    p = pg.Prop("acacia")
    lean = Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 3.2))
    p.cone((0, 0, 0), lean, 0.32, 0.22, TRUNK, segs=8)
    top = lean + Vector((0, 0, 0.9))
    for k in range(3):  # forked branches
        a = k / 3 * math.tau + rng.uniform(-0.3, 0.3)
        tip = top + Vector((math.cos(a) * 1.8, math.sin(a) * 1.8, 0.6))
        p.cone(lean, tip, 0.18, 0.08, TRUNK, segs=6)
    # the signature flat umbrella canopy, layered
    p.ellipsoid(top + Vector((0, 0, 0.9)), (3.6, 3.2, 0.55), LEAF, segs=(14, 6), smooth=False)
    p.ellipsoid(top + Vector((0.4, -0.3, 0.45)), (3.0, 2.8, 0.45), LEAF_D, segs=(12, 5), smooth=False)
    return p.finish()


def baobab():
    p = pg.Prop("baobab")
    p.cone((0, 0, 0), (0, 0, 4.2), 1.25, 0.85, TRUNK, segs=10)
    for k in range(6):
        a = k / 6 * math.tau
        base = Vector((math.cos(a) * 0.5, math.sin(a) * 0.5, 4.0))
        tip = base + Vector((math.cos(a) * 1.4, math.sin(a) * 1.4, 1.0))
        p.cone(base, tip, 0.25, 0.08, TRUNK, segs=6)
        p.ellipsoid(tip + Vector((0, 0, 0.2)), (0.6, 0.6, 0.35), LEAF, segs=(8, 5), smooth=False)
    return p.finish()


def termite_mound(seed):
    rng = random.Random(seed)
    p = pg.Prop("termite_mound")
    h = rng.uniform(1.6, 2.6)
    p.cone((0, 0, 0), (0, 0, h), 0.8, 0.12, RED, segs=8)
    for k in range(2):
        a = rng.uniform(0, math.tau)
        b = Vector((math.cos(a) * 0.45, math.sin(a) * 0.45, 0))
        p.cone(b, b + Vector((0, 0, h * 0.6)), 0.45, 0.08, RED, segs=7)
    return p.finish()


def grass_tuft(seed):
    rng = random.Random(seed)
    p = pg.Prop("grass_tuft")
    for k in range(9):
        a = rng.uniform(0, math.tau)
        lean = Vector((math.cos(a) * 0.25, math.sin(a) * 0.25, rng.uniform(0.5, 0.9)))
        p.cone((0, 0, 0), lean, 0.06, 0.005, YELLOW if k % 3 else LEAF, segs=4, smooth=False)
    return p.finish()


def pond(name, rim_slot=WET, reeds=False):
    p = pg.Prop(name)
    p.ellipsoid((0, 0, -0.02), (4.4, 2.8, 0.1), rim_slot, segs=(16, 4), smooth=False)
    p.ellipsoid((0, 0, 0.0), (3.8, 2.3, 0.08), WATER, segs=(16, 4), smooth=False)
    if reeds:
        rng = random.Random(3)
        for k in range(14):
            a = rng.uniform(0, math.tau)
            b = Vector((math.cos(a) * 3.9, math.sin(a) * 2.5, 0))
            p.cone(b, b + Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), rng.uniform(0.8, 1.5))),
                   0.05, 0.01, LEAF, segs=4, smooth=False)
    return p.finish()


# ---------------------------------------------------------------- amazon

def jungle_tree(seed):
    rng = random.Random(seed)
    p = pg.Prop("jungle_tree")
    h = rng.uniform(10, 14)
    p.cone((0, 0, 0), (0, 0, h), 0.75, 0.45, TRUNK, segs=9)
    for k in range(5):  # buttress roots
        a = k / 5 * math.tau + rng.uniform(-0.2, 0.2)
        rot = Matrix.Rotation(a, 3, "Z")
        p.box(rot @ Vector((1.0, 0, 0.9)), (1.8, 0.14, 1.8), TRUNK, rot=rot @ Matrix.Rotation(math.radians(-35), 3, "Y"))
    for k in range(4):  # canopy blobs
        a = k / 4 * math.tau + rng.uniform(-0.4, 0.4)
        c = Vector((math.cos(a) * 2.4, math.sin(a) * 2.4, h + rng.uniform(-0.5, 1.0)))
        p.ellipsoid(c, (3.6, 3.4, 2.2), LEAF if k % 2 else LEAF_D, segs=(10, 7), smooth=False)
    p.ellipsoid((0, 0, h + 2.0), (3.2, 3.2, 2.4), LEAF, segs=(10, 7), smooth=False)
    for k in range(6):  # hanging vines
        a = rng.uniform(0, math.tau)
        r = rng.uniform(2.0, 4.0)
        top = Vector((math.cos(a) * r, math.sin(a) * r, h - 0.6))
        p.cone(top, top + Vector((0, 0, -rng.uniform(3.0, 6.0))), 0.05, 0.03, LEAF_D, segs=4, smooth=False)
    return p.finish()


def fern(seed):
    rng = random.Random(seed)
    p = pg.Prop("fern")
    for k in range(8):
        a = k / 8 * math.tau + rng.uniform(-0.2, 0.2)
        rot = Matrix.Rotation(a, 3, "Z") @ Matrix.Rotation(math.radians(-rng.uniform(25, 45)), 3, "Y")
        c = rot @ Vector((0.9, 0, 0)) + Vector((0, 0, 0.45))
        p.ellipsoid(c, (1.0, 0.22, 0.03), LEAF if k % 2 else TEAL, rot=rot, segs=(8, 4), smooth=False)
    return p.finish()


def bigleaf(seed):
    rng = random.Random(seed)
    p = pg.Prop("bigleaf")
    for k in range(5):
        a = k / 5 * math.tau + rng.uniform(-0.3, 0.3)
        h = rng.uniform(0.8, 1.6)
        tip = Vector((math.cos(a) * 0.6, math.sin(a) * 0.6, h))
        p.cone((0, 0, 0), tip, 0.04, 0.03, LEAF_D, segs=4)
        rot = Matrix.Rotation(a, 3, "Z") @ Matrix.Rotation(math.radians(-20), 3, "Y")
        p.ellipsoid(tip + rot @ Vector((0.45, 0, 0)), (0.65, 0.45, 0.03), LEAF_D if k % 2 else LEAF, rot=rot,
                    segs=(8, 4), smooth=False)
    return p.finish()


def fallen_log():
    p = pg.Prop("fallen_log")
    p.cone((-2.2, 0, 0.45), (2.2, 0, 0.45), 0.45, 0.4, TRUNK, segs=9)
    for x in (-1.2, 0.3, 1.5):
        p.ellipsoid((x, 0, 0.85), (0.45, 0.3, 0.08), LEAF, segs=(8, 4), smooth=False)
    p.ellipsoid((2.25, 0, 0.45), (0.05, 0.38, 0.38), WOOD, segs=(8, 6))
    return p.finish()


def flower_bush(seed):
    rng = random.Random(seed)
    p = pg.Prop("flower_bush")
    p.ellipsoid((0, 0, 0.45), (0.8, 0.8, 0.55), LEAF_D, segs=(9, 6), smooth=False)
    for k in range(9):
        a = rng.uniform(0, math.tau)
        el = rng.uniform(0.1, 1.2)
        c = Vector((math.cos(a) * math.cos(el) * 0.8, math.sin(a) * math.cos(el) * 0.8, 0.45 + math.sin(el) * 0.55))
        p.ellipsoid(c, (0.13, 0.13, 0.13), (RED, YELLOW, ORANGE)[k % 3], segs=(6, 4))
    return p.finish()


# ---------------------------------------------------------------- sahara

def tent():
    p = pg.Prop("tent")
    p.cone((0, 0, 0), (0, 0, 2.6), 2.6, 0.1, RED, segs=4, smooth=False)
    p.cone((0, 0, 0.9), (0, 0, 1.25), 2.0, 1.85, WHITE, segs=4, caps=False, smooth=False)  # stripe
    p.box((0, -1.55, 0.7), (0.9, 0.2, 1.4), NET)  # doorway shadow
    for sx in (-1, 1):
        p.cone((sx * 2.4, -2.4, 0), (sx * 2.4, -2.4, 1.2), 0.05, 0.04, WOOD, segs=5)
    return p.finish()


def rock_outcrop(seed):
    rng = random.Random(seed)
    p = pg.Prop("rock_outcrop")
    for k in range(4):
        w = rng.uniform(1.0, 2.2)
        h = rng.uniform(1.5, 4.0)
        c = Vector((rng.uniform(-1.5, 1.5), rng.uniform(-1.5, 1.5), 0))
        p.cone(c, c + Vector((0, 0, h)), w, w * 0.55, ROCK, segs=6, smooth=False)
    return p.finish()


def lantern_post():
    p = pg.Prop("lantern_post")
    p.cone((0, 0, 0), (0, 0, 2.2), 0.06, 0.05, WOOD, segs=6)
    p.box((0, 0, 2.35), (0.3, 0.3, 0.4), YELLOW)
    p.cone((0, 0, 2.55), (0, 0, 2.8), 0.25, 0.02, RED, segs=4, smooth=False)
    return p.finish()


# ---------------------------------------------------------------- arctic

def pine_snow(seed):
    rng = random.Random(seed)
    p = pg.Prop("pine_snow")
    h = rng.uniform(4.5, 7.0)
    p.cone((0, 0, 0), (0, 0, h * 0.3), 0.22, 0.18, TRUNK, segs=6)
    for k in range(4):
        z0 = h * (0.2 + 0.2 * k)
        r = (1.8 - 0.38 * k) * h / 6
        p.cone((0, 0, z0), (0, 0, z0 + h * 0.32), r, 0.05, LEAF_D if k % 2 else LEAF, segs=8, smooth=False)
        p.cone((0, 0, z0 + h * 0.20), (0, 0, z0 + h * 0.34), r * 0.42, 0.04, WHITE, segs=8, smooth=False)
    return p.finish()


def igloo():
    p = pg.Prop("igloo")
    p.ellipsoid((0, 0, 0), (2.2, 2.2, 1.8), WHITE, segs=(14, 10), smooth=False)
    p.ellipsoid((0, -2.0, 0), (0.85, 1.1, 0.95), WHITE, segs=(10, 6), smooth=False)
    p.ellipsoid((0, -3.02, 0.35), (0.5, 0.08, 0.55), NET, segs=(8, 5))
    for z in (0.6, 1.2):  # block seams
        r = math.sqrt(max(0.0, 1 - (z / 1.8) ** 2)) * 2.2 * 1.01
        p.cone((0, 0, z - 0.02), (0, 0, z + 0.02), r, r, BLUE, segs=14, caps=False, smooth=False)
    return p.finish()


def ice_blocks(seed):
    rng = random.Random(seed)
    p = pg.Prop("ice_blocks")
    for k in range(4):
        s = rng.uniform(0.6, 1.2)
        c = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), s / 2 + (0.6 if k == 3 else 0)))
        p.box(c, (s, s * rng.uniform(0.8, 1.2), s), BLUE if k % 2 else TEAL,
              rot=Matrix.Rotation(rng.uniform(0, 1.5), 3, "Z"))
    return p.finish()


def snowman():
    p = pg.Prop("snowman")
    p.ellipsoid((0, 0, 0.55), (0.62, 0.62, 0.58), WHITE, segs=(12, 8))
    p.ellipsoid((0, 0, 1.35), (0.45, 0.45, 0.42), WHITE, segs=(12, 8))
    p.ellipsoid((0, 0, 1.95), (0.32, 0.32, 0.31), WHITE, segs=(12, 8))
    p.cone((0, -0.28, 1.95), (0, -0.62, 1.92), 0.06, 0.005, ORANGE, segs=6)
    for sx in (-1, 1):
        p.ellipsoid((sx * 0.11, -0.28, 2.05), (0.035, 0.03, 0.035), NET, segs=(6, 4))
        p.cone((sx * 0.4, 0, 1.45), (sx * 1.0, 0, 1.8), 0.03, 0.015, TRUNK, segs=4)
    p.cone((0, 0, 1.62), (0, 0, 1.72), 0.36, 0.34, RED, segs=12, caps=False)
    p.box((0.18, -0.34, 1.45), (0.12, 0.05, 0.4), RED)
    return p.finish()


# ---------------------------------------------------------------- themes

THEMES = {
    "savanna": {
        "folder": "Savanna",
        "palette": [(0.86, 0.72, 0.42), (0.74, 0.60, 0.34), (0.45, 0.33, 0.22), (0.50, 0.62, 0.25),
                    (0.36, 0.48, 0.20), (0.78, 0.42, 0.25), (0.97, 0.95, 0.90), (0.66, 0.50, 0.32),
                    (0.70, 0.60, 0.48), (0.35, 0.55, 0.60), (0.93, 0.80, 0.45), (0.30, 0.50, 0.85),
                    (0.12, 0.12, 0.14), (0.66, 0.52, 0.48), (0.95, 0.55, 0.20), (0.55, 0.45, 0.30)],
        "terrain": dict(amp=0.5, sea=False), "backdrop": "mesas",
        "props": {"acacia": lambda: acacia(2), "acacia_b": lambda: acacia(9), "baobab": baobab,
                  "termite_mound": lambda: termite_mound(4), "grass_tuft": lambda: grass_tuft(1),
                  "waterhole": lambda: pond("waterhole")},
    },
    "amazon": {
        "folder": "Amazon",
        "palette": [(0.40, 0.55, 0.24), (0.30, 0.44, 0.19), (0.40, 0.28, 0.18), (0.22, 0.62, 0.26),
                    (0.10, 0.42, 0.19), (0.92, 0.25, 0.30), (0.97, 0.97, 0.92), (0.55, 0.40, 0.25),
                    (0.45, 0.50, 0.42), (0.28, 0.50, 0.40), (0.98, 0.80, 0.20), (0.25, 0.55, 0.90),
                    (0.12, 0.12, 0.14), (0.20, 0.70, 0.55), (0.98, 0.55, 0.15), (0.32, 0.36, 0.22)],
        "terrain": dict(amp=0.8, sea=True), "backdrop": "jungle",
        "props": {"jungle_tree": lambda: jungle_tree(3), "jungle_tree_b": lambda: jungle_tree(8),
                  "fern": lambda: fern(2), "bigleaf": lambda: bigleaf(5), "fallen_log": fallen_log,
                  "flower_bush": lambda: flower_bush(7)},
        "water": True,
    },
    "sahara": {
        "folder": "Sahara",
        "palette": [(0.95, 0.74, 0.46), (0.86, 0.63, 0.38), (0.55, 0.38, 0.22), (0.35, 0.62, 0.28),
                    (0.22, 0.48, 0.22), (0.82, 0.26, 0.24), (0.97, 0.94, 0.86), (0.70, 0.52, 0.32),
                    (0.80, 0.58, 0.40), (0.25, 0.60, 0.75), (0.98, 0.85, 0.40), (0.20, 0.40, 0.80),
                    (0.12, 0.12, 0.14), (0.25, 0.70, 0.70), (0.95, 0.55, 0.20), (0.75, 0.58, 0.40)],
        "terrain": dict(amp=2.0, sea=False, bumps=0.08), "backdrop": "pyramids",
        "props": {"date_palm": lambda: pg.palm(4), "tent": tent, "rock_outcrop": lambda: rock_outcrop(6),
                  "oasis": lambda: pond("oasis", rim_slot=LEAF, reeds=True), "lantern_post": lantern_post},
    },
    "arctic": {
        "folder": "Arctic",
        "palette": [(0.93, 0.96, 1.00), (0.82, 0.88, 0.96), (0.40, 0.30, 0.22), (0.20, 0.42, 0.35),
                    (0.12, 0.32, 0.28), (0.85, 0.25, 0.25), (1.00, 1.00, 1.00), (0.60, 0.45, 0.30),
                    (0.55, 0.60, 0.68), (0.14, 0.32, 0.52), (0.98, 0.82, 0.25), (0.62, 0.85, 0.95),
                    (0.12, 0.12, 0.14), (0.52, 0.78, 0.90), (0.98, 0.55, 0.15), (0.75, 0.85, 0.95)],
        "terrain": dict(amp=0.7, sea=True), "backdrop": "peaks",
        "props": {"pine_snow": lambda: pine_snow(1), "pine_snow_b": lambda: pine_snow(6), "igloo": igloo,
                  "ice_blocks": lambda: ice_blocks(2), "snowman": snowman, "floes": floes},
        "water": True,
    },
}


def export_theme(key):
    t = THEMES[key]
    out = os.path.join(OUT_ROOT, t["folder"])
    os.makedirs(out, exist_ok=True)
    pg.write_palette_png(os.path.join(out, "palette.png"), t["palette"])

    builders = {
        "terrain": lambda: terrain(**t["terrain"]),
        "backdrop": lambda: backdrop(t["backdrop"]),
        "court": pg.court,
        "rock_a": lambda: pg.rock(3, "rock_a"),
        "rock_b": lambda: pg.rock(11, "rock_b"),
    }
    if t.get("water"):
        builders["ocean"] = pg.ocean
    builders.update(t["props"])

    for name, make in builders.items():
        pg.clear_scene()
        obj = make()
        obj.name = name
        pg.export_mesh(obj, os.path.join(out, f"prop_{name}.fbx"))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv or argv[0] != "export":
        print(__doc__)
        return
    keys = list(THEMES) if argv[1:] in ([], ["all"]) else argv[1:]
    for k in keys:
        export_theme(k)


if __name__ == "__main__":
    main()
