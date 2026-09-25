"""The remaining arena themes for arena_gen.py: Outback, Himalaya, Black Forest, Rockies and the six
fantasy courts (Volcano, Lunar, Atlantis, Cloud Kingdom, Graveyard, Neon Rooftop).

Same conventions as arena_gen.py: palette slot MEANINGS are shared (0 ground, 1 ground dark,
2 trunk, 3 leaf, 4 leaf dark, 5 red, 6 white, 7 wood, 8 rock, 9 water, 10 yellow, 11 blue,
12 net/dark, 13 teal, 14 orange, 15 wet). A 4th palette value < 1 makes that slot self-lit
(lava, neon, lanterns, moon) — see the Volleyball/Stylized shader.
"""
import math
import random

from mathutils import Matrix, Vector

import props_gen as pg
from props_gen import (SAND, SAND_D, TRUNK, LEAF, LEAF_D, RED, WHITE, WOOD, ROCK, WATER,
                       YELLOW, BLUE, NET, TEAL, ORANGE, WET)

GLOW = 0.25  # palette alpha for self-lit slots


# ---------------------------------------------------------------- backdrops

def _ring(n, rmin, rmax, seed, spread=85):
    rng = random.Random(seed)
    for k in range(n):
        a = math.radians(-spread + 2 * spread * k / (n - 1)) + rng.uniform(-0.05, 0.05)
        r = rng.uniform(rmin, rmax)
        yield rng, -math.cos(a) * r, math.sin(a) * r


def backdrop_monolith():
    p = pg.Prop("backdrop")
    # the great red rock: a long rounded loaf on the horizon
    p.ellipsoid((-120, 10, 0), (45, 18, 16), RED, segs=(18, 9), smooth=False)
    for rng, x, y in _ring(12, 100, 150, 3):
        if abs(y) < 40 and x < -100:
            continue
        p.ellipsoid((x, y, -2), (rng.uniform(10, 20), rng.uniform(8, 14), rng.uniform(3, 7)), SAND_D, segs=(9, 5), smooth=False)
    return p.finish()


def backdrop_peaks(height=1.0, snow=0.62, seed=4, n=16):
    p = pg.Prop("backdrop")
    for rng, x, y in _ring(n, 95, 150, seed):
        h = rng.uniform(22, 40) * height
        w = h * rng.uniform(0.55, 0.85)
        p.cone((x, y, -1), (x, y, h), w, 0.5, ROCK, segs=6, smooth=False)
        p.cone((x, y, h * snow), (x, y, h + 0.3), w * (1 - snow) * 1.05, 0.2, WHITE, segs=6, smooth=False)
    return p.finish()


def backdrop_firs():
    p = pg.Prop("backdrop")
    for rng, x, y in _ring(40, 70, 120, 8, spread=88):
        h = rng.uniform(10, 20)
        p.cone((x, y, -1), (x, y, h), h * 0.3, 0.2, LEAF_D, segs=6, smooth=False)
    for rng, x, y in _ring(8, 130, 160, 9):
        p.ellipsoid((x, y, -4), (rng.uniform(25, 40), rng.uniform(20, 30), rng.uniform(12, 20)), LEAF, segs=(10, 6), smooth=False)
    return p.finish()


def backdrop_volcano():
    p = pg.Prop("backdrop")
    # the big one, straight across the court from the camera
    p.cone((-140, 0, -2), (-140, 0, 55), 75, 14, ROCK, segs=12, smooth=False)
    p.cone((-140, 0, 52), (-140, 0, 56), 13, 11, ORANGE, segs=12, smooth=False)  # glowing crater rim
    for k in range(5):  # lava streams down the flank
        a = math.radians(-30 + 15 * k)
        top = Vector((-140 + math.cos(a) * 12, math.sin(a) * 12, 52))
        bot = Vector((-140 + math.cos(a) * 60, math.sin(a) * 60, 3))
        p.cone(top, bot, 1.6, 3.0, ORANGE, segs=5, smooth=False)
    for rng, x, y in _ring(10, 90, 130, 2):
        if abs(y) < 50:
            continue
        h = rng.uniform(10, 22)
        p.cone((x, y, -1), (x, y, h), h * 0.9, 1.0, SAND_D, segs=7, smooth=False)
    return p.finish()


def backdrop_moon():
    p = pg.Prop("backdrop")
    for rng, x, y in _ring(14, 100, 150, 6):
        p.ellipsoid((x, y, -3), (rng.uniform(15, 30), rng.uniform(12, 22), rng.uniform(4, 9)), SAND_D, segs=(10, 5), smooth=False)
    # Earth hanging in the black sky
    p.ellipsoid((-230, 60, 70), (26, 26, 26), BLUE, segs=(20, 12))
    for k in range(5):
        a = k * 1.3
        c = Vector((-230 + 22 * math.cos(a) * 0.3 + 18, 60 + 24 * math.sin(a) * 0.7, 70 + 14 * math.cos(a * 1.7)))
        p.ellipsoid(c, (7, 9, 6), LEAF, segs=(8, 5))
    return p.finish()


def backdrop_reef():
    p = pg.Prop("backdrop")
    for rng, x, y in _ring(14, 80, 130, 5):
        # rock arches and sunken temple blocks on the seabed
        if rng.random() < 0.4:
            w = rng.uniform(6, 10)
            p.cone((x, y - w, -1), (x, y - w, 14), 2.5, 2.0, ROCK, segs=7, smooth=False)
            p.cone((x, y + w, -1), (x, y + w, 14), 2.5, 2.0, ROCK, segs=7, smooth=False)
            p.box((x, y, 15), (5, w * 2 + 6, 3), ROCK)
        else:
            p.ellipsoid((x, y, -2), (rng.uniform(10, 20), rng.uniform(8, 16), rng.uniform(5, 12)), TEAL, segs=(9, 6), smooth=False)
    # the drowned temple
    p.box((-110, 0, 6), (30, 50, 12), WHITE)
    p.cone((-110, 0, 12), (-110, 0, 24), 30, 0.5, WHITE, segs=4, smooth=False)
    for k in range(6):
        p.cone((-94, -20 + k * 8, 0), (-94, -20 + k * 8, 12), 1.4, 1.4, WHITE, segs=8)
    return p.finish()


def backdrop_clouds():
    p = pg.Prop("backdrop")
    for rng, x, y in _ring(10, 90, 140, 12):
        # floating islands: grassy top, rock underside
        r = rng.uniform(8, 16)
        z = rng.uniform(8, 30)
        p.cone((x, y, z - r * 1.3), (x, y, z), 0.5, r, ROCK, segs=7, smooth=False)
        p.cone((x, y, z), (x, y, z + 1.2), r, r * 0.95, LEAF, segs=7, smooth=False)
        if rng.random() < 0.5:
            p.cone((x, y, z + 1), (x, y, z + r * 0.9), 1.4, 0.3, WHITE, segs=6)
            p.cone((x, y, z + r * 0.9), (x, y, z + r * 1.3), 2.0, 0.05, BLUE, segs=6, smooth=False)
    # a rainbow arch across the far sky
    cols = [RED, ORANGE, YELLOW, LEAF, BLUE]
    for i, c in enumerate(cols):
        rr = 90 - i * 3.5
        for k in range(18):
            a0 = math.pi * k / 18
            a1 = math.pi * (k + 1) / 18
            p.cone((-170, math.cos(a0) * rr, math.sin(a0) * rr - 10),
                   (-170, math.cos(a1) * rr, math.sin(a1) * rr - 10), 1.8, 1.8, c, segs=5)
    return p.finish()


def backdrop_graveyard():
    p = pg.Prop("backdrop")
    for rng, x, y in _ring(14, 90, 140, 13):
        p.ellipsoid((x, y, -3), (rng.uniform(18, 30), rng.uniform(14, 24), rng.uniform(6, 12)), LEAF_D, segs=(10, 5), smooth=False)
        if rng.random() < 0.6:  # dead tree silhouette on the hilltop
            b = Vector((x, y, rng.uniform(4, 8)))
            p.cone(b, b + Vector((0, 0, 7)), 0.5, 0.15, NET, segs=5)
            for k in range(3):
                a = rng.uniform(0, math.tau)
                s = b + Vector((0, 0, 3 + k * 1.3))
                p.cone(s, s + Vector((math.cos(a) * 3, math.sin(a) * 3, 2)), 0.25, 0.05, NET, segs=4)
    p.ellipsoid((-240, -50, 90), (22, 22, 22), YELLOW, segs=(20, 12))  # the big full moon (self-lit)
    return p.finish()


def backdrop_skyline():
    p = pg.Prop("backdrop")
    rng = random.Random(21)
    for k in range(60):
        a = math.radians(-88 + 176 * k / 59)
        r = rng.uniform(60, 130)
        x, y = -math.cos(a) * r, math.sin(a) * r
        w = rng.uniform(6, 14)
        h = rng.uniform(15, 60)
        p.box((x, y, h / 2 - 2), (w, w * rng.uniform(0.8, 1.4), h), NET if k % 3 else ROCK)
        d = Vector((-x, -y, 0)).normalized()  # the face looking back at the court
        face_rot = Matrix.Rotation(math.atan2(d.y, d.x), 3, "Z")
        for row in range(int(h / 6)):  # lit windows (self-lit)
            if rng.random() < 0.55:
                z = 3 + row * 6
                p.box(Vector((x, y, z)) + d * (w * 0.52), (0.3, w * 0.7, 1.4),
                      YELLOW if rng.random() < 0.7 else TEAL, rot=face_rot)
        if rng.random() < 0.25:  # antenna with a red light
            p.cone((x, y, h - 2), (x, y, h + 6), 0.2, 0.1, ROCK, segs=4)
            p.ellipsoid((x, y, h + 6.3), (0.6, 0.6, 0.6), RED, segs=(6, 4))
    return p.finish()


EXTRA_BACKDROPS = {
    "monolith": backdrop_monolith,
    "himalaya": lambda: backdrop_peaks(height=1.6, snow=0.5, seed=4, n=18),
    "firs": backdrop_firs,
    "rockies": lambda: backdrop_peaks(height=1.1, snow=0.7, seed=17),
    "volcano": backdrop_volcano,
    "moon": backdrop_moon,
    "reef": backdrop_reef,
    "clouds": backdrop_clouds,
    "graveyard": backdrop_graveyard,
    "skyline": backdrop_skyline,
}


# ---------------------------------------------------------------- outback

def eucalyptus(seed):
    rng = random.Random(seed)
    p = pg.Prop("eucalyptus")
    lean = Vector((rng.uniform(-0.8, 0.8), rng.uniform(-0.8, 0.8), 5.5))
    p.cone((0, 0, 0), lean, 0.30, 0.15, WHITE, segs=7)  # pale ghost-gum bark
    for k in range(4):
        a = k / 4 * math.tau + rng.uniform(-0.4, 0.4)
        base = lean * rng.uniform(0.55, 0.85)
        tip = base + Vector((math.cos(a) * 1.8, math.sin(a) * 1.8, 1.6))
        p.cone(base, tip, 0.12, 0.05, WHITE, segs=5)
        p.ellipsoid(tip + Vector((0, 0, 0.5)), (1.4, 1.3, 0.9), LEAF if k % 2 else LEAF_D, segs=(8, 6), smooth=False)
    return p.finish()


def windmill():
    p = pg.Prop("windmill")
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.cone((sx * 1.0, sy * 1.0, 0), (sx * 0.15, sy * 0.15, 8), 0.07, 0.05, ROCK, segs=4)
    hub = Vector((0, -0.35, 8.3))
    p.ellipsoid(hub, (0.35, 0.35, 0.35), ROCK, segs=(8, 5))
    for k in range(12):
        a = k / 12 * math.tau
        rot = Matrix.Rotation(a, 3, "Y")
        p.box(hub + rot @ Vector((1.1, -0.05, 0)), (1.6, 0.04, 0.35), WHITE if k % 2 else RED, rot=rot)
    p.box((0, 1.0, 8.3), (0.08, 1.6, 0.9), ROCK)  # tail vane
    p.cone((1.8, 0.5, 0), (1.8, 0.5, 1.2), 1.1, 1.1, ROCK, segs=10)  # water tank
    return p.finish()


# ---------------------------------------------------------------- himalaya

def prayer_flags(seed):
    rng = random.Random(seed)
    p = pg.Prop("prayer_flags")
    p.cone((-5, 0, 0), (-5, 0, 4.5), 0.08, 0.06, WOOD, segs=5)
    p.cone((5, 0, 0), (5, 0, 4.5), 0.08, 0.06, WOOD, segs=5)
    cols = [BLUE, WHITE, RED, LEAF, YELLOW]
    for k in range(16):
        t = (k + 0.5) / 16
        x = -5 + 10 * t
        z = 4.4 - math.sin(t * math.pi) * 1.1
        p.box((x, 0, z - 0.3), (0.45, 0.03, 0.55), cols[k % 5],
              rot=Matrix.Rotation(rng.uniform(-0.2, 0.2), 3, "X"))
    return p.finish()


def stupa():
    p = pg.Prop("stupa")
    p.box((0, 0, 0.5), (4.0, 4.0, 1.0), WHITE)
    p.box((0, 0, 1.3), (3.2, 3.2, 0.6), WHITE)
    p.ellipsoid((0, 0, 1.6), (1.6, 1.6, 1.4), WHITE, segs=(14, 8))
    p.box((0, 0, 3.1), (1.0, 1.0, 0.6), YELLOW)
    p.cone((0, 0, 3.4), (0, 0, 5.6), 0.55, 0.05, YELLOW, segs=8)
    for sy in (-1, 1):  # painted eyes
        p.ellipsoid((0.0 + 0.25 * sy, -0.52, 3.15), (0.14, 0.03, 0.07), NET, segs=(6, 4))
    return p.finish()


# ---------------------------------------------------------------- black forest / rockies

def fir(seed):
    rng = random.Random(seed)
    p = pg.Prop("fir")
    h = rng.uniform(8, 12)
    p.cone((0, 0, 0), (0, 0, h * 0.25), 0.3, 0.25, TRUNK, segs=6)
    for k in range(5):
        z0 = h * (0.15 + 0.16 * k)
        r = (2.3 - 0.4 * k) * h / 10
        p.cone((0, 0, z0), (0, 0, z0 + h * 0.3), r, 0.05, LEAF_D if k % 2 else LEAF, segs=8, smooth=False)
    return p.finish()


def cabin(roof_slot=RED):
    p = pg.Prop("cabin")
    p.box((0, 0, 1.2), (4.2, 3.4, 2.4), WOOD)
    for z in (0.4, 1.0, 1.6, 2.2):  # log seams
        p.box((0, -1.71, z), (4.25, 0.05, 0.08), TRUNK)
    # two slabs meeting in a ridge along Y: each rises toward the middle
    p.box((-1.05, 0, 3.05), (2.9, 3.9, 0.18), roof_slot, rot=Matrix.Rotation(math.radians(-38), 3, "Y"))
    p.box((1.05, 0, 3.05), (2.9, 3.9, 0.18), roof_slot, rot=Matrix.Rotation(math.radians(38), 3, "Y"))
    p.box((0, -1.72, 0.9), (0.9, 0.06, 1.8), TRUNK)       # door
    p.box((1.3, -1.72, 1.5), (0.8, 0.06, 0.7), YELLOW)    # warm window
    p.box((1.4, 0.6, 3.6), (0.5, 0.5, 1.4), ROCK)         # chimney
    return p.finish()


def mushroom(seed):
    rng = random.Random(seed)
    p = pg.Prop("mushroom")
    for k in range(3):
        c = Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 0))
        h = rng.uniform(0.4, 0.9)
        p.cone(c, c + Vector((0, 0, h)), 0.09, 0.08, WHITE, segs=6)
        cap = c + Vector((0, 0, h))
        r = h * 0.55
        p.ellipsoid(cap, (r, r, r * 0.55), RED, segs=(10, 6))
        for d in range(4):
            a = d * 1.6 + k
            p.ellipsoid(cap + Vector((math.cos(a) * r * 0.6, math.sin(a) * r * 0.6, r * 0.42)), (0.05, 0.05, 0.02), WHITE, segs=(5, 3))
    return p.finish()


def boulder(seed):
    rng = random.Random(seed)
    p = pg.Prop("boulder")
    for k in range(3):
        s = rng.uniform(1.0, 2.2)
        c = Vector((rng.uniform(-1.5, 1.5), rng.uniform(-1.5, 1.5), s * 0.5))
        p.ellipsoid(c, (s, s * 0.85, s * 0.8), ROCK, segs=(7, 5), smooth=False)
    return p.finish()


# ---------------------------------------------------------------- volcano

def obsidian_spikes(seed):
    rng = random.Random(seed)
    p = pg.Prop("obsidian_spikes")
    for k in range(6):
        b = Vector((rng.uniform(-1.4, 1.4), rng.uniform(-1.4, 1.4), 0))
        tip = b + Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), rng.uniform(1.5, 3.5)))
        p.cone(b, tip, rng.uniform(0.3, 0.6), 0.02, NET if k % 2 else ROCK, segs=5, smooth=False)
    return p.finish()


def lava_rock(seed):
    rng = random.Random(seed)
    p = pg.Prop("lava_rock")
    s = rng.uniform(1.2, 1.8)
    p.ellipsoid((0, 0, s * 0.4), (s, s * 0.8, s * 0.65), ROCK, segs=(8, 5), smooth=False)
    for k in range(4):  # glowing cracks
        a = k * 1.7
        p.box((math.cos(a) * s * 0.55, math.sin(a) * s * 0.45, s * 0.55), (s * 0.9, 0.08, 0.06), ORANGE,
              rot=Matrix.Rotation(a, 3, "Z"))
    return p.finish()


def charred_tree(seed):
    rng = random.Random(seed)
    p = pg.Prop("charred_tree")
    h = rng.uniform(3.5, 5.0)
    p.cone((0, 0, 0), (0, 0, h), 0.25, 0.08, NET, segs=6)
    for k in range(4):
        a = rng.uniform(0, math.tau)
        s = Vector((0, 0, h * rng.uniform(0.4, 0.8)))
        p.cone(s, s + Vector((math.cos(a) * 1.3, math.sin(a) * 1.3, 0.9)), 0.1, 0.02, NET, segs=4)
    return p.finish()


# ---------------------------------------------------------------- lunar

def crater(seed):
    rng = random.Random(seed)
    p = pg.Prop("crater")
    r = rng.uniform(2.5, 4.0)
    p.cone((0, 0, 0), (0, 0, 0.45), r + 0.8, r, SAND_D, segs=14, caps=False, smooth=False)
    p.ellipsoid((0, 0, 0.02), (r, r, 0.08), WET, segs=(14, 4), smooth=False)
    return p.finish()


def lander():
    p = pg.Prop("lander")
    p.box((0, 0, 1.6), (1.8, 1.8, 1.2), YELLOW)          # gold foil descent stage
    p.cone((0, 0, 2.2), (0, 0, 3.4), 1.1, 0.9, WHITE, segs=8, smooth=False)
    p.ellipsoid((0, -0.9, 2.8), (0.3, 0.05, 0.25), NET, segs=(6, 4))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.cone((sx * 0.8, sy * 0.8, 1.2), (sx * 1.6, sy * 1.6, 0.05), 0.06, 0.05, ROCK, segs=4)
            p.cone((sx * 1.6, sy * 1.6, 0), (sx * 1.6, sy * 1.6, 0.08), 0.3, 0.3, ROCK, segs=8)
    p.cone((1.2, 1.2, 0), (1.2, 1.2, 2.4), 0.03, 0.03, WHITE, segs=4)  # flag
    p.box((1.62, 1.2, 2.15), (0.8, 0.03, 0.5), RED)
    return p.finish()


def satellite_dish():
    p = pg.Prop("satellite_dish")
    p.cone((0, 0, 0), (0, 0, 2.0), 0.12, 0.1, ROCK, segs=6)
    rot = Matrix.Rotation(math.radians(-35), 3, "X")
    p.cone(Vector((0, -0.2, 2.3)), Vector((0, -0.2, 2.3)) + rot @ Vector((0, 0, 0.6)), 1.3, 0.2, WHITE, segs=12, smooth=False)
    p.ellipsoid((0, -0.6, 2.9), (0.1, 0.1, 0.1), RED, segs=(6, 4))
    return p.finish()


# ---------------------------------------------------------------- atlantis

def coral(seed):
    rng = random.Random(seed)
    p = pg.Prop("coral")
    cols = [RED, ORANGE, YELLOW, TEAL]
    for k in range(7):
        b = Vector((rng.uniform(-0.8, 0.8), rng.uniform(-0.8, 0.8), 0))
        col = cols[k % 4]
        pts = [b]
        for s in range(3):
            pts.append(pts[-1] + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0.4, 0.7))))
        for i in range(3):
            p.cone(pts[i], pts[i + 1], 0.12 - i * 0.03, 0.09 - i * 0.03, col, segs=5)
        p.ellipsoid(pts[-1], (0.1, 0.1, 0.1), col, segs=(5, 4))
    return p.finish()


def kelp(seed):
    rng = random.Random(seed)
    p = pg.Prop("kelp")
    for k in range(4):
        b = Vector((rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7), 0))
        prev = b
        for s in range(8):
            nxt = prev + Vector((math.sin(s * 0.9 + k) * 0.25, math.cos(s * 0.7 + k) * 0.2, 0.7))
            p.cone(prev, nxt, 0.07, 0.06, LEAF if s % 2 else LEAF_D, segs=4)
            p.ellipsoid((prev + nxt) / 2 + Vector((0.18, 0, 0)), (0.22, 0.05, 0.12), LEAF, segs=(5, 3), smooth=False)
            prev = nxt
    return p.finish()


def column_ruin(seed):
    rng = random.Random(seed)
    p = pg.Prop("column_ruin")
    h = rng.uniform(2.0, 5.0)
    p.box((0, 0, 0.2), (1.4, 1.4, 0.4), WHITE)
    p.cone((0, 0, 0.4), (0, 0, h), 0.5, 0.45, WHITE, segs=10)
    for k in range(10):  # flutes
        a = k / 10 * math.tau
        p.box((math.cos(a) * 0.48, math.sin(a) * 0.48, (0.4 + h) / 2), (0.06, 0.06, h - 0.4), TEAL,
              rot=Matrix.Rotation(a, 3, "Z"))
    if h > 3.5:
        p.box((0, 0, h + 0.15), (1.3, 1.3, 0.3), WHITE)
    else:  # broken: a fallen drum beside it
        p.cone((1.2, 0.6, 0.45), (2.3, 0.9, 0.45), 0.45, 0.45, WHITE, segs=10)
    return p.finish()


def clam():
    p = pg.Prop("clam")
    p.ellipsoid((0, 0, 0.2), (0.8, 0.6, 0.2), BLUE, segs=(10, 5), smooth=False)
    p.ellipsoid((0, 0.2, 0.55), (0.8, 0.25, 0.55), BLUE, rot=Matrix.Rotation(math.radians(-25), 3, "X"), segs=(10, 5), smooth=False)
    p.ellipsoid((0, -0.05, 0.42), (0.22, 0.22, 0.22), WHITE, segs=(10, 6))  # the pearl
    return p.finish()


# ---------------------------------------------------------------- cloud kingdom

def cloud_puff(seed):
    rng = random.Random(seed)
    p = pg.Prop("cloud_puff")
    for k in range(6):
        c = Vector((rng.uniform(-1.6, 1.6), rng.uniform(-1.0, 1.0), rng.uniform(0.3, 0.9)))
        r = rng.uniform(0.7, 1.2)
        p.ellipsoid(c, (r, r, r * 0.8), WHITE if k % 3 else SAND_D, segs=(10, 7))
    return p.finish()


def cloud_tower(seed):
    rng = random.Random(seed)
    p = pg.Prop("cloud_tower")
    h = rng.uniform(6, 9)
    p.cone((0, 0, 0), (0, 0, h), 1.3, 1.1, WHITE, segs=10)
    p.cone((0, 0, h), (0, 0, h + 3.0), 1.6, 0.05, BLUE, segs=10, smooth=False)
    for k in range(8):  # battlements
        a = k / 8 * math.tau
        p.box((math.cos(a) * 1.15, math.sin(a) * 1.15, h - 0.1), (0.4, 0.4, 0.5), WHITE, rot=Matrix.Rotation(a, 3, "Z"))
    p.box((0, -1.2, 1.0), (0.8, 0.1, 1.9), YELLOW)  # golden gate
    p.cone((0, 0, h + 3.0), (0, 0, h + 4.2), 0.03, 0.03, WOOD, segs=4)
    p.box((0.35, 0, h + 3.9), (0.6, 0.03, 0.35), RED)
    return p.finish()


def harp():
    p = pg.Prop("harp")
    pts = [Vector((0, 0, 0)), Vector((0.4, 0, 1.6)), Vector((0.2, 0, 2.6)), Vector((-0.8, 0, 2.3))]
    for i in range(3):
        p.cone(pts[i], pts[i + 1], 0.12, 0.1, YELLOW, segs=6)
    p.cone(pts[0], pts[3], 0.07, 0.07, YELLOW, segs=6)
    for k in range(6):
        x = -0.65 + k * 0.17
        p.cone((x, 0, 0.2 + k * 0.05), (x, 0, 2.2 + k * 0.05), 0.01, 0.01, WHITE, segs=3)
    return p.finish()


# ---------------------------------------------------------------- graveyard

def tombstone(seed):
    rng = random.Random(seed)
    p = pg.Prop("tombstone")
    for k in range(3):
        c = Vector((k * 1.4 - 1.4, rng.uniform(-0.3, 0.3), 0))
        tilt = Matrix.Rotation(rng.uniform(-0.2, 0.2), 3, "Y")
        if k == 1:  # a cross
            p.box(c + Vector((0, 0, 0.8)), (0.2, 0.15, 1.6), ROCK, rot=tilt)
            p.box(c + Vector((0, 0, 1.15)), (0.8, 0.15, 0.2), ROCK, rot=tilt)
        else:
            p.box(c + Vector((0, 0, 0.55)), (0.8, 0.2, 1.1), ROCK, rot=tilt)
            p.ellipsoid(c + Vector((0, 0, 1.1)), (0.4, 0.1, 0.25), ROCK, rot=tilt, segs=(8, 4))
        p.ellipsoid(c + Vector((0, -0.8, 0.02)), (0.45, 0.8, 0.1), SAND_D, segs=(8, 4), smooth=False)  # grave mound
    return p.finish()


def dead_tree(seed):
    rng = random.Random(seed)
    p = pg.Prop("dead_tree")
    h = rng.uniform(5, 7)
    p.cone((0, 0, 0), (0.4, 0.2, h), 0.35, 0.08, TRUNK, segs=6)
    for k in range(6):
        a = rng.uniform(0, math.tau)
        s = Vector((0.4 * k / 6, 0.2 * k / 6, h * rng.uniform(0.35, 0.9)))
        e = s + Vector((math.cos(a) * 1.8, math.sin(a) * 1.8, rng.uniform(0.3, 1.5)))
        p.cone(s, e, 0.12, 0.02, TRUNK, segs=4)
        p.cone(e, e + Vector((math.cos(a + 0.8) * 0.7, math.sin(a + 0.8) * 0.7, 0.4)), 0.05, 0.01, TRUNK, segs=3)
    return p.finish()


def pumpkin(seed):
    rng = random.Random(seed)
    p = pg.Prop("pumpkin")
    for k in range(3):
        c = Vector((k * 0.9 - 0.9, rng.uniform(-0.3, 0.3), 0))
        r = rng.uniform(0.35, 0.5)
        for s in range(6):  # ribbed body
            a = s / 6 * math.tau
            p.ellipsoid(c + Vector((math.cos(a) * r * 0.35, math.sin(a) * r * 0.35, r * 0.7)),
                        (r * 0.55, r * 0.55, r * 0.7), ORANGE, segs=(8, 6))
        p.cone(c + Vector((0, 0, r * 1.3)), c + Vector((0, 0, r * 1.6)), 0.05, 0.03, LEAF_D, segs=4)
        for sx in (-1, 1):  # glowing jack-o'-lantern eyes
            p.cone(c + Vector((sx * r * 0.3, -r * 0.8, r * 0.85)), c + Vector((sx * r * 0.3, -r * 0.9, r * 1.05)),
                   0.08, 0.01, YELLOW, segs=3, smooth=False)
        p.ellipsoid(c + Vector((0, -r * 0.88, r * 0.55)), (r * 0.35, 0.03, 0.06), YELLOW, segs=(6, 3))
    return p.finish()


def lamp_post():
    p = pg.Prop("lamp_post")
    p.cone((0, 0, 0), (0, 0, 2.8), 0.07, 0.05, NET, segs=6)
    p.box((0, 0, 3.0), (0.35, 0.35, 0.45), YELLOW)
    p.cone((0, 0, 3.2), (0, 0, 3.5), 0.3, 0.02, NET, segs=4, smooth=False)
    return p.finish()


def crypt():
    p = pg.Prop("crypt")
    p.box((0, 0, 1.3), (3.2, 3.8, 2.6), ROCK)
    p.cone((0, 0, 2.6), (0, 0, 3.8), 2.6, 0.05, NET, segs=4, smooth=False)
    p.box((0, -1.92, 1.0), (1.2, 0.06, 2.0), NET)
    for sx in (-1, 1):
        p.cone((sx * 1.4, -2.0, 0), (sx * 1.4, -2.0, 2.6), 0.2, 0.18, WHITE, segs=8)
    return p.finish()


# ---------------------------------------------------------------- neon rooftop

def ac_unit(seed):
    rng = random.Random(seed)
    p = pg.Prop("ac_unit")
    p.box((0, 0, 0.6), (1.6, 1.2, 1.2), ROCK)
    p.cone((0, 0, 1.2), (0, 0, 1.25), 0.45, 0.45, NET, segs=10)
    for k in range(4):
        p.box((0, -0.61, 0.25 + k * 0.22), (1.3, 0.03, 0.06), NET)
    if rng.random() < 0.5:
        p.cone((0.5, 0.4, 1.2), (0.5, 0.4, 2.2), 0.08, 0.08, ROCK, segs=6)
    return p.finish()


def water_tower():
    p = pg.Prop("water_tower")
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.cone((sx * 1.1, sy * 1.1, 0), (sx * 0.9, sy * 0.9, 3.5), 0.08, 0.08, NET, segs=4)
    p.cone((0, 0, 3.5), (0, 0, 6.5), 1.5, 1.5, WOOD, segs=12)
    p.cone((0, 0, 6.5), (0, 0, 7.5), 1.6, 0.1, NET, segs=12, smooth=False)
    for z in (4.2, 5.2, 6.2):
        p.cone((0, 0, z - 0.04), (0, 0, z + 0.04), 1.53, 1.53, NET, segs=12, caps=False)
    return p.finish()


def neon_sign(seed):
    rng = random.Random(seed)
    p = pg.Prop("neon_sign")
    for sx in (-1, 1):
        p.cone((sx * 1.8, 0, 0), (sx * 1.8, 0, 3.0), 0.06, 0.06, NET, segs=4)
    p.box((0, 0.05, 3.2), (4.2, 0.12, 1.6), NET)
    col = rng.choice((RED, TEAL, YELLOW, BLUE))
    col2 = TEAL if col != TEAL else RED
    # glowing tubes: a volleyball + a zigzag
    for k in range(10):
        a0, a1 = k / 10 * math.tau, (k + 1) / 10 * math.tau
        c = Vector((-1.2, -0.05, 3.2))
        p.cone(c + Vector((math.cos(a0) * 0.55, 0, math.sin(a0) * 0.55)), c + Vector((math.cos(a1) * 0.55, 0, math.sin(a1) * 0.55)),
               0.05, 0.05, col, segs=4)
    for k in range(5):
        a = Vector((-0.3 + k * 0.4, -0.05, 2.8 + (k % 2) * 0.8))
        b = Vector((-0.3 + (k + 1) * 0.4, -0.05, 2.8 + ((k + 1) % 2) * 0.8))
        p.cone(a, b, 0.05, 0.05, col2, segs=4)
    return p.finish()


def rooftop_edge():
    """Parapet wall around the roof, outside the play area."""
    p = pg.Prop("rooftop_edge")
    x0, x1, y0, y1 = -20.0, 20.0, -26.0, 26.0
    for (a, b) in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))):
        c = Vector(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, 0.6))
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        horiz = abs(b[1] - a[1]) < 0.1
        p.box(c, (L if horiz else 0.6, 0.6 if horiz else L, 1.2), ROCK)
        p.box(c + Vector((0, 0, 0.65)), (L if horiz else 0.7, 0.7 if horiz else L, 0.1), TEAL)  # neon trim (self-lit)
    return p.finish()


# ---------------------------------------------------------------- themes

def _pal(*cols):
    return list(cols)


MORE_THEMES = {
    "outback": {
        "folder": "Outback",
        "palette": _pal((0.80, 0.42, 0.24), (0.68, 0.34, 0.20), (0.45, 0.33, 0.22), (0.55, 0.62, 0.38),
                        (0.40, 0.48, 0.30), (0.82, 0.36, 0.20), (0.92, 0.90, 0.84), (0.66, 0.50, 0.32),
                        (0.62, 0.40, 0.28), (0.35, 0.55, 0.60), (0.88, 0.76, 0.40), (0.30, 0.50, 0.85),
                        (0.12, 0.12, 0.14), (0.62, 0.45, 0.40), (0.95, 0.55, 0.20), (0.55, 0.32, 0.20)),
        "terrain": dict(amp=0.6, sea=False), "backdrop": "monolith",
        "props": {"eucalyptus": lambda: eucalyptus(3), "eucalyptus_b": lambda: eucalyptus(8), "windmill": windmill,
                  "spinifex": lambda: _grass(5), "termite_mound": lambda: _termite(2), "boulder": lambda: boulder(4)},
    },
    "himalaya": {
        "folder": "Himalaya",
        "palette": _pal((0.76, 0.78, 0.84), (0.62, 0.62, 0.66), (0.40, 0.30, 0.22), (0.30, 0.48, 0.36),
                        (0.20, 0.36, 0.30), (0.85, 0.22, 0.22), (1.00, 1.00, 1.00), (0.60, 0.45, 0.30),
                        (0.52, 0.52, 0.56), (0.35, 0.55, 0.70), (0.98, 0.78, 0.22), (0.25, 0.45, 0.85),
                        (0.12, 0.12, 0.14), (0.55, 0.60, 0.70), (0.95, 0.55, 0.20), (0.70, 0.72, 0.78)),
        "terrain": dict(amp=1.6, sea=False, bumps=0.1), "backdrop": "himalaya",
        "props": {"prayer_flags": lambda: prayer_flags(1), "stupa": stupa, "boulder": lambda: boulder(9),
                  "pine_snow": lambda: _pine(3)},
    },
    "forest": {
        "folder": "Forest",
        "palette": _pal((0.30, 0.46, 0.22), (0.24, 0.38, 0.18), (0.36, 0.25, 0.16), (0.18, 0.40, 0.22),
                        (0.10, 0.28, 0.16), (0.82, 0.18, 0.16), (0.97, 0.95, 0.90), (0.58, 0.40, 0.24),
                        (0.45, 0.46, 0.44), (0.30, 0.45, 0.50), (0.98, 0.80, 0.35), (0.25, 0.45, 0.85),
                        (0.12, 0.12, 0.14), (0.25, 0.55, 0.45), (0.95, 0.55, 0.20), (0.30, 0.30, 0.20)),
        "terrain": dict(amp=0.9, sea=False), "backdrop": "firs",
        "props": {"fir": lambda: fir(2), "fir_b": lambda: fir(7), "cabin": cabin, "mushroom": lambda: mushroom(4),
                  "fern": lambda: _fern(3), "log": lambda: _log()},
    },
    "rockies": {
        "folder": "Rockies",
        "palette": _pal((0.46, 0.60, 0.30), (0.38, 0.50, 0.26), (0.40, 0.28, 0.18), (0.20, 0.42, 0.28),
                        (0.12, 0.32, 0.22), (0.72, 0.25, 0.20), (0.97, 0.97, 0.97), (0.62, 0.44, 0.28),
                        (0.55, 0.55, 0.58), (0.25, 0.50, 0.72), (0.95, 0.78, 0.30), (0.25, 0.45, 0.85),
                        (0.12, 0.12, 0.14), (0.35, 0.60, 0.65), (0.95, 0.55, 0.20), (0.40, 0.45, 0.35)),
        "terrain": dict(amp=1.1, sea=True), "backdrop": "rockies",
        "props": {"fir": lambda: fir(5), "fir_b": lambda: fir(11), "cabin": lambda: cabin(roof_slot=ROCK),
                  "boulder": lambda: boulder(2), "boulder_b": lambda: boulder(12)},
        "water": True,
    },
    "volcano": {
        "folder": "Volcano",
        "palette": _pal((0.30, 0.26, 0.26), (0.22, 0.19, 0.20), (0.20, 0.16, 0.14), (0.45, 0.42, 0.30),
                        (0.30, 0.28, 0.22), (0.95, 0.25, 0.10, GLOW), (0.90, 0.86, 0.80), (0.45, 0.30, 0.20),
                        (0.36, 0.32, 0.32), (1.00, 0.42, 0.08, GLOW), (1.00, 0.85, 0.30, GLOW), (0.30, 0.45, 0.80),
                        (0.10, 0.09, 0.10), (0.55, 0.30, 0.25), (1.00, 0.55, 0.12, GLOW), (0.45, 0.20, 0.12)),
        "terrain": dict(amp=1.2, sea=True, bumps=0.15), "backdrop": "volcano",
        "props": {"obsidian_spikes": lambda: obsidian_spikes(3), "lava_rock": lambda: lava_rock(5),
                  "lava_rock_b": lambda: lava_rock(9), "charred_tree": lambda: charred_tree(2)},
        "water": True,
    },
    "lunar": {
        "folder": "Lunar",
        "palette": _pal((0.62, 0.62, 0.64), (0.50, 0.50, 0.53), (0.40, 0.40, 0.42), (0.30, 0.62, 0.35),
                        (0.20, 0.45, 0.30), (0.85, 0.20, 0.20), (0.96, 0.96, 0.98), (0.60, 0.60, 0.62),
                        (0.45, 0.45, 0.48), (0.25, 0.45, 0.85), (0.95, 0.80, 0.30), (0.22, 0.45, 0.90),
                        (0.08, 0.08, 0.10), (0.40, 0.70, 0.85, GLOW), (0.95, 0.55, 0.20), (0.40, 0.40, 0.42)),
        "terrain": dict(amp=0.5, sea=False, bumps=0.2), "backdrop": "moon",
        "props": {"crater": lambda: crater(2), "crater_b": lambda: crater(7), "lander": lander,
                  "satellite_dish": satellite_dish, "boulder": lambda: boulder(6)},
    },
    "atlantis": {
        "folder": "Atlantis",
        "palette": _pal((0.82, 0.78, 0.62), (0.70, 0.68, 0.55), (0.40, 0.32, 0.25), (0.25, 0.60, 0.40),
                        (0.15, 0.42, 0.32), (0.95, 0.40, 0.50), (0.92, 0.94, 0.96), (0.60, 0.48, 0.35),
                        (0.45, 0.52, 0.55), (0.25, 0.55, 0.75), (0.98, 0.82, 0.35), (0.35, 0.40, 0.85),
                        (0.12, 0.14, 0.18), (0.35, 0.78, 0.75), (0.98, 0.58, 0.30), (0.60, 0.62, 0.55)),
        "terrain": dict(amp=0.8, sea=False), "backdrop": "reef",
        "props": {"coral": lambda: coral(2), "coral_b": lambda: coral(9), "kelp": lambda: kelp(3),
                  "column_ruin": lambda: column_ruin(4), "column_ruin_b": lambda: column_ruin(1), "clam": clam},
    },
    "sky": {
        "folder": "Sky",
        "palette": _pal((0.80, 0.80, 0.95), (0.70, 0.71, 0.90), (0.55, 0.42, 0.30), (0.45, 0.78, 0.40),
                        (0.30, 0.62, 0.32), (0.95, 0.35, 0.40), (1.00, 1.00, 1.00), (0.70, 0.55, 0.35),
                        (0.62, 0.58, 0.70), (0.45, 0.70, 0.95), (1.00, 0.85, 0.35), (0.40, 0.55, 0.95),
                        (0.20, 0.20, 0.30), (0.55, 0.80, 0.90), (1.00, 0.62, 0.30), (0.72, 0.74, 0.90)),
        "terrain": dict(amp=0.7, sea=False, bumps=0.15), "backdrop": "clouds",
        "props": {"cloud_puff": lambda: cloud_puff(2), "cloud_puff_b": lambda: cloud_puff(8),
                  "cloud_tower": lambda: cloud_tower(3), "harp": harp},
    },
    "graveyard": {
        "folder": "Graveyard",
        "palette": _pal((0.28, 0.30, 0.30), (0.22, 0.24, 0.25), (0.25, 0.20, 0.20), (0.30, 0.36, 0.28),
                        (0.20, 0.24, 0.22), (0.70, 0.20, 0.25), (0.85, 0.85, 0.88), (0.40, 0.32, 0.26),
                        (0.50, 0.50, 0.55), (0.25, 0.30, 0.40), (1.00, 0.85, 0.40, GLOW), (0.35, 0.35, 0.65),
                        (0.08, 0.08, 0.10), (0.45, 0.80, 0.60, GLOW), (0.95, 0.52, 0.15), (0.30, 0.28, 0.28)),
        "terrain": dict(amp=0.8, sea=False), "backdrop": "graveyard",
        "props": {"tombstone": lambda: tombstone(2), "tombstone_b": lambda: tombstone(7), "dead_tree": lambda: dead_tree(3),
                  "dead_tree_b": lambda: dead_tree(9), "pumpkin": lambda: pumpkin(4), "lamp_post": lamp_post, "crypt": crypt},
    },
    "neon": {
        "folder": "Neon",
        "palette": _pal((0.34, 0.34, 0.38), (0.28, 0.28, 0.32), (0.30, 0.26, 0.24), (0.30, 0.50, 0.35),
                        (0.20, 0.35, 0.25), (1.00, 0.25, 0.55, GLOW), (0.90, 0.90, 0.95), (0.50, 0.40, 0.32),
                        (0.42, 0.42, 0.48), (0.25, 0.35, 0.60), (1.00, 0.85, 0.35, GLOW), (0.35, 0.50, 1.00, GLOW),
                        (0.10, 0.10, 0.14), (0.20, 0.95, 0.90, GLOW), (1.00, 0.55, 0.20), (0.26, 0.26, 0.30)),
        "terrain": dict(amp=0.0, sea=False), "backdrop": "skyline",
        "props": {"ac_unit": lambda: ac_unit(1), "ac_unit_b": lambda: ac_unit(6), "water_tower": water_tower,
                  "neon_sign": lambda: neon_sign(2), "neon_sign_b": lambda: neon_sign(5), "rooftop_edge": rooftop_edge},
    },
}


# thin re-exports of arena_gen props (imported lazily to avoid a circular import)
def _grass(seed):
    import arena_gen
    return arena_gen.grass_tuft(seed)


def _termite(seed):
    import arena_gen
    return arena_gen.termite_mound(seed)


def _pine(seed):
    import arena_gen
    return arena_gen.pine_snow(seed)


def _fern(seed):
    import arena_gen
    return arena_gen.fern(seed)


def _log():
    import arena_gen
    return arena_gen.fallen_log()
