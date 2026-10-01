"""End-of-court vistas for the baseline camera.

The arenas were dressed for the old broadcast camera (Blender +X, looking toward -X), so their
distant `backdrop` ring only covers the -X hemisphere and nothing stands beyond the baselines.
The baseline camera looks DOWN the court (Blender ±Y), so each arena gets one more mesh,
`prop_vista`, with three layers:

  1. far filler — the theme's distant shapes completing the horizon on the +X half;
  2. a hero landmark at each end of the court (a different one at each end, so both teams'
     views have something to look at): Kilimanjaro, pyramids, a lighthouse, a launch pad...;
  3. mid-ground dressing around both ends, sat on the terrain's real height.

Usage (headless, after arena_gen has written the theme palettes):
  blender -b --factory-startup -P Tools/blender/arena_vistas.py -- export all
  blender -b --factory-startup -P Tools/blender/arena_vistas.py -- export beach savanna
  blender -b --factory-startup -P Tools/blender/arena_vistas.py -- preview out.png savanna
"""
import math
import os
import random
import sys

from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import props_gen as pg  # noqa: E402
from props_gen import (SAND, SAND_D, TRUNK, LEAF, LEAF_D, RED, WHITE, WOOD, ROCK, WATER,  # noqa: E402
                       YELLOW, BLUE, NET, TEAL, ORANGE, WET)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
ARENA_ROOT = os.path.join(ROOT, "Assets", "Art", "Arenas")
BEACH_DIR = os.path.join(ROOT, "Assets", "Art", "Props")


# ---------------------------------------------------------------- terrain height (matches arena_gen)

def land_fn(amp, sea, bumps=0.12):
    def h(x, y):
        dx = max(0.0, abs(x) - 16.0)
        dy = max(0.0, abs(y) - 22.0)
        outside = math.hypot(dx, dy)
        dune = (0.7 * math.sin(0.33 * x + 1.3) * math.cos(0.27 * y) + 0.45 * math.sin(0.55 * y + 0.2 * x)
                + 0.5 * noise.noise(Vector((x * bumps, y * bumps, 0.7))))
        v = pg.smoothstep(0.0, 6.0, outside) * (dune * amp + 0.4 + outside * 0.06 * amp)
        if sea:
            s = max(0.0, -x - 18.0)
            v = v * (1.0 - pg.smoothstep(0.0, 8.0, s)) - s * 0.09
        return v
    return h


# ---------------------------------------------------------------- placement helpers

def far_fill(n, rmin, rmax, seed):
    """Positions on the +X half of the horizon (the half the old backdrops never covered)."""
    rng = random.Random(seed)
    for k in range(n):
        a = math.radians(-80 + 160 * k / max(n - 1, 1)) + rng.uniform(-0.04, 0.04)
        r = rng.uniform(rmin, rmax)
        yield rng, math.cos(a) * r, math.sin(a) * r


def end_ring(n, rmin, rmax, seed, half_angle=55):
    """Positions in the two end sectors (around ±Y), mid-distance — beyond the play area."""
    rng = random.Random(seed)
    for k in range(n):
        end = 1 if k % 2 == 0 else -1
        a = math.radians(rng.uniform(-half_angle, half_angle))
        r = rng.uniform(rmin, rmax)
        x, y = math.sin(a) * r, end * math.cos(a) * r
        if abs(x) < 9 and abs(y) < 40:   # keep the line straight down the court clear of clutter
            x += 12 * (1 if x >= 0 else -1)
        yield rng, x, y


# ---------------------------------------------------------------- reusable shapes

def tree_round(p, b, h, trunk=TRUNK, leaf=LEAF, leaf2=LEAF_D, rng=None):
    rng = rng or random.Random(1)
    p.cone(b, b + Vector((0, 0, h * 0.55)), h * 0.06, h * 0.04, trunk, segs=6)
    for k in range(3):
        c = b + Vector((rng.uniform(-1, 1) * h * 0.12, rng.uniform(-1, 1) * h * 0.12, h * (0.6 + k * 0.12)))
        p.ellipsoid(c, (h * 0.28, h * 0.28, h * 0.2), leaf if k % 2 else leaf2, segs=(8, 5), smooth=False)


def tree_flat(p, b, h, rng):
    """Acacia/eucalyptus: a thin trunk and a wide flat crown."""
    p.cone(b, b + Vector((0, 0, h * 0.75)), h * 0.05, h * 0.03, TRUNK, segs=6)
    p.ellipsoid(b + Vector((0, 0, h * 0.82)), (h * 0.55, h * 0.45, h * 0.13), LEAF_D if rng.random() < 0.5 else LEAF,
                segs=(10, 5), smooth=False)


def conifer(p, b, h, snow=False):
    p.cone(b, b + Vector((0, 0, h * 0.2)), h * 0.05, h * 0.04, TRUNK, segs=5)
    for k in range(3):
        z0 = h * (0.15 + k * 0.25)
        p.cone(b + Vector((0, 0, z0)), b + Vector((0, 0, z0 + h * 0.4)), h * (0.3 - k * 0.07), 0.05, LEAF_D, segs=7, smooth=False)
        if snow:
            p.cone(b + Vector((0, 0, z0 + h * 0.22)), b + Vector((0, 0, z0 + h * 0.4)), h * (0.13 - k * 0.03), 0.04, WHITE, segs=7, smooth=False)


def palm_tree(p, b, h, rng):
    lean = Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), 1)).normalized()
    top = b + lean * h
    p.cone(b, top, h * 0.05, h * 0.035, TRUNK, segs=6)
    for k in range(6):
        a = k / 6 * math.tau
        d = Vector((math.cos(a), math.sin(a), -0.35)).normalized()
        p.ellipsoid(top + d * h * 0.22, (h * 0.26, h * 0.07, h * 0.03), LEAF,
                    rot=Vector((1, 0, 0)).rotation_difference(d).to_matrix(), segs=(7, 4), smooth=False)


def mountain(p, x, y, base_z, h, w, snow=0.0, rock=ROCK, segs=7):
    p.cone((x, y, base_z - 2), (x, y, base_z + h), w, 0.6, rock, segs=segs, smooth=False)
    if snow > 0:
        p.cone((x, y, base_z + h * snow), (x, y, base_z + h + 0.4), w * (1 - snow) * 1.06, 0.2, WHITE, segs=segs, smooth=False)


def building(p, x, y, base_z, w, d, h, body, window, rng, lit=0.55):
    p.box((x, y, base_z + h / 2 - 1), (w, d, h), body)
    to_court = Vector((-x, -y, 0)).normalized()
    face_rot = Matrix.Rotation(math.atan2(to_court.y, to_court.x), 3, "Z")
    for row in range(int(h / 5)):
        if rng.random() < lit:
            z = base_z + 3 + row * 5
            p.box(Vector((x, y, z)) + to_court * (max(w, d) * 0.51), (0.3, min(w, d) * 0.7, 1.4), window, rot=face_rot)


# ---------------------------------------------------------------- heroes (one per end)
# Each takes (p, y_end, ground) where y_end = ±distance down the court axis and ground(x, y) the land.

def hero_kilimanjaro(p, ye, g):
    x, yy = -15.0, ye * 1.7
    p.cone((x, yy, -3), (x, yy, 30), 46, 12, ROCK, segs=11, smooth=False)
    p.cone((x, yy, 21), (x, yy, 30.5), 19, 11.5, WHITE, segs=11, smooth=False)
    for k in range(5):  # foothill shoulders
        a = k / 5 * math.tau
        p.ellipsoid((x + math.cos(a) * 45, yy + math.sin(a) * 20, 2), (20, 16, 9), SAND_D, segs=(9, 5), smooth=False)


def hero_pride_rock(p, ye, g):
    x = 20.0
    z = g(x, ye)
    p.ellipsoid((x, ye, z), (26, 16, 12), ROCK, segs=(12, 7), smooth=False)
    # the jutting ledge
    p.box((x - 14, ye - math.copysign(6, ye), z + 13), (22, 7, 2.5), ROCK, rot=Matrix.Rotation(math.radians(8), 3, "Y"))
    rng = random.Random(4)
    for k in range(4):
        tree_flat(p, Vector((x + rng.uniform(-30, 30), ye + rng.uniform(-14, 14), g(x, ye))), rng.uniform(9, 13), rng)


def hero_waterfall(p, ye, g):
    x = 0.0
    z = g(x, ye)
    p.box((x, ye, z + 18), (70, 14, 40), ROCK)                       # cliff face
    p.box((x, ye - math.copysign(7.2, ye), z + 18), (9, 0.6, 38), WATER)  # the fall
    for k in range(6):                                                 # canopy along the top
        p.ellipsoid((x - 30 + k * 12, ye, z + 40), (9, 8, 6), LEAF_D if k % 2 else LEAF, segs=(9, 5), smooth=False)
    for k in range(4):                                                 # mist at the foot
        p.ellipsoid((x + (k - 1.5) * 4, ye - math.copysign(10, ye), z + 1), (5, 4, 3), WHITE, segs=(8, 5))


def hero_temple(p, ye, g):
    x = 18.0
    z = g(x, ye)
    for k in range(6):  # stepped pyramid
        s = 30 - k * 4.5
        p.box((x, ye, z + 2 + k * 3.4), (s, s, 3.4), ROCK)
    p.box((x, ye, z + 24), (6, 6, 5), ROCK)
    rng = random.Random(9)
    for k in range(10):  # overgrown
        p.ellipsoid((x + rng.uniform(-14, 14), ye + rng.uniform(-14, 14), z + rng.uniform(4, 20)),
                    (rng.uniform(2.5, 4.5),) * 2 + (rng.uniform(1.5, 2.5),), LEAF_D, segs=(7, 4), smooth=False)


def hero_pyramids(p, ye, g):
    for x, s in ((-28, 34), (8, 26), (34, 18)):
        yy = ye * 1.45
        p.cone((x, yy, -1), (x, yy, s * 0.95), s, 0.3, SAND_D, segs=4, smooth=False)
    # the sphinx, lying in front
    x, y0 = -2.0, ye - math.copysign(22, ye)
    z = g(x, y0)
    p.box((x, y0, z + 2.2), (6, 16, 4.4), SAND_D)
    p.box((x, y0 - math.copysign(8, ye), z + 6), (4.4, 4.4, 5), SAND_D)
    p.box((x, y0 - math.copysign(8.5, ye), z + 8.9), (5.4, 3.6, 1.2), BLUE)


def hero_caravan(p, ye, g):
    x0 = -10.0
    p.ellipsoid((x0, ye, g(x0, ye) - 4), (60, 22, 16), SAND, segs=(14, 7))  # a big dune
    for k in range(5):  # camel silhouettes walking the crest
        x = x0 - 20 + k * 9
        b = Vector((x, ye, g(x0, ye) + 11.5))
        p.ellipsoid(b + Vector((0, 0, 2.2)), (2.2, 1.0, 1.0), TRUNK, segs=(8, 5))
        p.ellipsoid(b + Vector((0, 0, 3.2)), (0.8, 0.7, 0.7), TRUNK, segs=(6, 4))
        p.cone(b + Vector((1.8, 0, 2.5)), b + Vector((2.9, 0, 4.4)), 0.35, 0.3, TRUNK, segs=5)
        p.ellipsoid(b + Vector((3.1, 0, 4.5)), (0.6, 0.35, 0.35), TRUNK, segs=(6, 4))
        for lx in (-1.4, 1.2):
            p.cone(b + Vector((lx, 0, 1.6)), b + Vector((lx, 0, 0)), 0.18, 0.15, TRUNK, segs=4)
    for k in range(3):
        palm_tree(p, Vector((x0 + 40 + k * 5, ye - math.copysign(6, ye) + k * 2, g(x0 + 40, ye))), 9 + k, random.Random(k))


def hero_glacier(p, ye, g):
    rng = random.Random(3)
    for k in range(9):
        x = -50 + k * 12.5
        h = rng.uniform(16, 28)
        p.box((x, ye, h / 2 - 2), (13, 10, h), WHITE if k % 3 else TEAL)
        p.cone((x + rng.uniform(-3, 3), ye, h - 2), (x, ye, h + rng.uniform(3, 7)), 5, 0.3, WHITE, segs=4, smooth=False)


def hero_iceberg(p, ye, g):
    x = -25.0
    p.cone((x, ye, -3), (x, ye, 30), 26, 3, WHITE, segs=6, smooth=False)
    p.cone((x + 14, ye + 6, -3), (x + 14, ye + 6, 18), 14, 2, TEAL, segs=5, smooth=False)
    # an ice arch beside it
    for sx in (-1, 1):
        p.cone((22 + sx * 8, ye, -2), (22 + sx * 6, ye, 14), 3.5, 2.5, WHITE, segs=6, smooth=False)
    p.box((22, ye, 15.5), (18, 5, 3.5), WHITE)


def hero_uluru(p, ye, g):
    x = 5.0
    p.ellipsoid((x, ye, g(x, ye) - 1), (48, 20, 17), RED, segs=(18, 9), smooth=False)
    for k in range(5):  # erosion grooves
        p.box((x - 30 + k * 14, ye - math.copysign(19, ye), g(x, ye) + 7), (1.2, 2, 12), WOOD)


def hero_windmill_station(p, ye, g):
    x = -18.0
    z = g(x, ye)
    p.cone((x, ye, z), (x, ye, z + 14), 2.4, 0.3, NET, segs=4)     # lattice tower (silhouette)
    hub = Vector((x, ye - math.copysign(0.8, ye), z + 14))
    for k in range(10):
        a = k / 10 * math.tau
        tip = hub + Vector((math.cos(a) * 4.5, 0, math.sin(a) * 4.5))
        p.cone(hub, tip, 0.25, 0.6, WHITE, segs=4)
    p.cone((x + 9, ye, z), (x + 9, ye, z + 6), 3.2, 3.2, ROCK, segs=10)  # water tank
    p.box((x + 22, ye + 4, z + 2.5), (10, 7, 5), WOOD)                  # homestead
    p.cone((x + 22, ye + 4, z + 5), (x + 22, ye + 4, z + 8), 7.5, 0.3, RED, segs=4, smooth=False)
    p.ellipsoid((x + 50, ye + 10, z - 1), (24, 14, 11), RED, segs=(12, 6), smooth=False)  # a mesa beyond


def hero_everest(p, ye, g):
    mountain(p, -10, ye * 1.9, 0, 75, 52, snow=0.45, segs=8)
    mountain(p, -48, ye * 1.6, 0, 50, 36, snow=0.5)
    mountain(p, 30, ye * 1.7, 0, 56, 38, snow=0.5)


def hero_monastery(p, ye, g):
    x = 14.0
    ye = ye * 1.25
    z = g(x, ye)
    p.cone((x, ye, z - 2), (x, ye, z + 22), 32, 12, ROCK, segs=8, smooth=False)  # the crag
    for k, (dx, w, h) in enumerate(((-6, 10, 7), (5, 8, 9), (0, 6, 5))):
        p.box((x + dx, ye, z + 22 + h / 2 + k * 3), (w, 7, h), WHITE)
        p.box((x + dx, ye, z + 22 + h + k * 3 + 0.6), (w + 1.5, 8.5, 1.2), RED)
    for k in range(8):  # prayer flag strings fanning down
        a = math.radians(-60 + k * 17)
        top = Vector((x, ye, z + 35))
        bot = top + Vector((math.sin(a) * 28, 0, -20))
        for s in range(6):
            q = top.lerp(bot, s / 6)
            p.box(q, (0.9, 0.2, 0.9), (RED, YELLOW, BLUE, LEAF, WHITE)[s % 5])


def hero_mountain_lake(p, ye, g):
    mountain(p, 0, ye * 1.95, 0, 60, 55, snow=0.6, segs=9)
    p.ellipsoid((0, ye * 1.02, g(0, ye) - 1.6), (38, 14, 2), WATER, segs=(14, 5))  # lake in front
    rng = random.Random(6)
    for k in range(14):
        x = -40 + k * 6
        conifer(p, Vector((x, ye * 0.95 + rng.uniform(-4, 4), g(x, ye))), rng.uniform(9, 15))


def hero_redwood(p, ye, g):
    x = -16.0
    z = g(x, ye)
    p.cone((x, ye, z - 1), (x, ye, z + 45), 4.5, 2.0, TRUNK, segs=9)
    for k in range(5):
        p.ellipsoid((x, ye, z + 26 + k * 5), (10 - k * 1.5, 10 - k * 1.5, 4), LEAF_D, segs=(10, 5), smooth=False)
    # a fire lookout tower
    x2 = 18.0
    z2 = g(x2, ye)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.cone((x2 + sx * 3, ye + sy * 3, z2), (x2 + sx * 1.6, ye + sy * 1.6, z2 + 16), 0.3, 0.3, WOOD, segs=4)
    p.box((x2, ye, z2 + 17.5), (5, 5, 3), WOOD)
    p.cone((x2, ye, z2 + 19), (x2, ye, z2 + 22), 4.2, 0.2, RED, segs=4, smooth=False)


def hero_volcano(p, ye, g):
    # compact enough to sit close (its skirt stays clear of the court) and short enough that the
    # glowing crater stays in frame from the baseline camera
    x = 8.0
    ye = ye * 1.45
    H, R, C = 26.0, 34.0, 7.0
    p.cone((x, ye, -2), (x, ye, H), R, C, ROCK, segs=12, smooth=False)
    p.cone((x, ye, H - 2), (x, ye, H + 1.2), C - 0.4, C - 2.0, ORANGE, segs=12, smooth=False)  # glowing crater
    for k in range(5):  # lava streams down the court-facing flank
        a = math.radians(-44 + 22 * k)
        d = Vector((math.sin(a), -math.copysign(math.cos(a), ye), 0))
        top = Vector((x, ye, H - 0.5)) + d * (C - 0.5)
        bot = Vector((x, ye, 0.5)) + d * (R - 2)
        p.cone(top, bot, 0.8, 1.8, ORANGE, segs=5, smooth=False)
    for k in range(4):  # a little smoke
        p.ellipsoid((x + k * 2.5, ye + k * 1.5, H + 3 + k * 3), (3 + k, 3 + k, 2.2 + k * 0.5), NET, segs=(9, 6))


def hero_lava_lake(p, ye, g):
    x = -14.0
    p.ellipsoid((x, ye, -0.6), (40, 18, 1.2), ORANGE, segs=(16, 6))
    rng = random.Random(8)
    for k in range(9):
        a = k / 9 * math.tau
        b = Vector((x + math.cos(a) * 34, ye + math.sin(a) * 15, -1))
        h = rng.uniform(10, 24)
        p.cone(b, b + Vector((rng.uniform(-2, 2), rng.uniform(-2, 2), h)), rng.uniform(2.5, 4), 0.2, NET, segs=5, smooth=False)


def hero_launch_pad(p, ye, g):
    x = 12.0
    z = g(x, ye)
    p.cone((x, ye, z), (x, ye, z + 34), 3.2, 3.2, WHITE, segs=12)           # the rocket
    p.cone((x, ye, z + 34), (x, ye, z + 42), 3.2, 0.2, RED, segs=12, smooth=False)
    for k in range(4):                                                         # fins
        a = k / 4 * math.tau
        p.box((x + math.cos(a) * 3.6, ye + math.sin(a) * 3.6, z + 3), (2.5, 0.4, 6), RED,
              rot=Matrix.Rotation(a, 3, "Z"))
    p.box((x + 8, ye, z + 18), (2.5, 2.5, 36), NET)                           # gantry
    for k in range(6):
        p.box((x + 5.5, ye, z + 4 + k * 6), (5, 1, 0.6), NET)
    for dx in (-24, -34):                                                      # radio dishes
        b = Vector((x + dx, ye + 6, z))
        p.cone(b, b + Vector((0, 0, 6)), 0.6, 0.6, ROCK, segs=6)
        p.ellipsoid(b + Vector((0, -math.copysign(0.5, ye), 8)), (5, 1.2, 5), WHITE, segs=(10, 5))


def hero_moon_base(p, ye, g):
    p.ellipsoid((0, ye * 1.9, -6), (80, 26, 14), SAND_D, segs=(16, 6), smooth=False)  # crater rim ridge, behind
    for k, (dx, r) in enumerate(((-26, 8), (-10, 11), (8, 7), (22, 9))):
        z = g(dx, ye)
        p.ellipsoid((dx, ye, z), (r, r, r * 0.8), WHITE, segs=(12, 7))               # habitat domes
        p.cone((dx, ye, z), (dx + 10 if k < 3 else dx, ye, z + 1), 1.2, 1.2, ROCK, segs=6)  # tunnels
    p.cone((34, ye, g(34, ye)), (34, ye, g(34, ye) + 16), 0.4, 0.2, ROCK, segs=4)        # antenna
    p.ellipsoid((34, ye, g(34, ye) + 16.5), (0.8, 0.8, 0.8), RED, segs=(6, 4))


def hero_palace(p, ye, g):
    x = 0.0
    z = g(x, ye)
    p.box((x, ye, z + 5), (40, 18, 10), WHITE)
    p.ellipsoid((x, ye, z + 10), (12, 12, 12), TEAL, segs=(14, 8))           # the dome
    p.cone((x, ye, z + 21), (x, ye, z + 27), 1.2, 0.1, YELLOW, segs=6)      # golden spire
    for k in range(8):                                                        # colonnade
        p.cone((x - 17.5 + k * 5, ye - math.copysign(10, ye), z), (x - 17.5 + k * 5, ye - math.copysign(10, ye), z + 9),
               1.0, 1.0, WHITE, segs=8)
    p.box((x, ye - math.copysign(10, ye), z + 9.7), (40, 3, 1.4), WHITE)
    rng = random.Random(2)
    for k in range(6):
        p.ellipsoid((x + rng.uniform(-22, 22), ye + rng.uniform(-8, 8), z + rng.uniform(0, 3)), (3, 3, 4), RED, segs=(7, 5))


def hero_statue_head(p, ye, g):
    x = -12.0
    z = g(x, ye)
    p.ellipsoid((x, ye, z + 9), (11, 10, 13), ROCK, segs=(14, 9), smooth=False)       # the head, half buried
    for sx in (-1, 1):
        p.ellipsoid((x + sx * 4, ye - math.copysign(8.5, ye), z + 12), (1.6, 1, 1.2), NET, segs=(8, 5))  # eyes
    for k in range(5):                                                                 # crown spikes
        a = math.radians(-60 + k * 30)
        b = Vector((x + math.sin(a) * 9, ye, z + 18 + math.cos(a) * 3))
        p.cone(b, b + Vector((math.sin(a) * 2, 0, 6)), 1.4, 0.1, YELLOW, segs=5, smooth=False)
    p.ellipsoid((x + 9, ye, z + 22), (1.8, 1.8, 1.8), TEAL, segs=(6, 4))
    # a trident beside it
    tx = x + 24
    p.cone((tx, ye, z - 1), (tx, ye, z + 24), 0.6, 0.6, YELLOW, segs=6)
    for dx in (-2.5, 0, 2.5):
        p.cone((tx + dx, ye, z + 22), (tx + dx, ye, z + 28), 0.4, 0.05, YELLOW, segs=5)
    p.box((tx, ye, z + 22), (6, 0.8, 0.8), YELLOW)


def hero_sky_castle(p, ye, g):
    x, z = 0.0, 18.0
    p.cone((x, ye, z - 18), (x, ye, z), 0.5, 26, ROCK, segs=8, smooth=False)        # island underside
    p.cone((x, ye, z), (x, ye, z + 1.5), 26, 25, LEAF, segs=8, smooth=False)
    for dx, h in ((-10, 16), (0, 26), (10, 16), (-4, 12), (6, 12)):
        p.cone((x + dx, ye, z + 1), (x + dx, ye, z + h), 2.5, 2.5, WHITE, segs=8)
        p.cone((x + dx, ye, z + h), (x + dx, ye, z + h + 6), 3.3, 0.1, BLUE, segs=8, smooth=False)
    p.box((x, ye, z + 5), (22, 8, 8), WHITE)


def hero_balloons(p, ye, g):
    rng = random.Random(11)
    for k in range(5):
        c = Vector((-40 + k * 20, ye + rng.uniform(-12, 12), rng.uniform(14, 34)))
        col = (RED, YELLOW, BLUE, ORANGE, TEAL)[k]
        p.ellipsoid(c, (5, 5, 6), col, segs=(12, 8))
        p.box(c + Vector((0, 0, -9)), (1.6, 1.6, 1.4), WOOD)
        for sx in (-0.7, 0.7):
            p.cone(c + Vector((sx * 3, 0, -4)), c + Vector((sx * 0.8, 0, -8.3)), 0.05, 0.05, NET, segs=3)
    for k in range(3):  # small floating islands below them
        x = -30 + k * 30
        z = rng.uniform(4, 10)
        p.cone((x, ye * 1.1, z - 7), (x, ye * 1.1, z), 0.3, 8, ROCK, segs=7, smooth=False)
        p.cone((x, ye * 1.1, z), (x, ye * 1.1, z + 0.8), 8, 7.6, LEAF, segs=7, smooth=False)


def hero_mansion(p, ye, g):
    x = 6.0
    p.ellipsoid((x, ye, -6), (48, 26, 16), LEAF_D, segs=(14, 7), smooth=False)    # the hill
    z = 8.0
    p.box((x, ye, z + 7), (20, 12, 14), NET)
    p.cone((x, ye, z + 14), (x, ye, z + 22), 14, 0.3, ROCK, segs=4, smooth=False)
    for dx in (-12, 12):                                                         # towers
        p.cone((x + dx, ye, z), (x + dx, ye, z + 20), 3, 3, NET, segs=8)
        p.cone((x + dx, ye, z + 20), (x + dx, ye, z + 28), 4, 0.1, ROCK, segs=8, smooth=False)
    to_court = -math.copysign(1, ye)
    for row in range(2):                                                         # lit windows (self-lit)
        for col in range(4):
            p.box((x - 6 + col * 4, ye + to_court * 6.1, z + 4 + row * 5), (1.6, 0.3, 2.2), YELLOW)
    rng = random.Random(5)
    for k in range(4):  # dead trees on the hillside
        b = Vector((x + rng.uniform(-36, 36), ye + rng.uniform(-10, 10), rng.uniform(2, 6)))
        p.cone(b, b + Vector((0, 0, 9)), 0.6, 0.15, NET, segs=5)
        for j in range(3):
            a = rng.uniform(0, math.tau)
            s = b + Vector((0, 0, 4 + j * 1.6))
            p.cone(s, s + Vector((math.cos(a) * 3.5, math.sin(a) * 3.5, 2.5)), 0.3, 0.05, NET, segs=4)


def hero_chapel(p, ye, g):
    x = -14.0
    z = g(x, ye)
    p.box((x, ye, z + 5), (12, 18, 10), ROCK)
    p.cone((x, ye, z + 10), (x, ye, z + 15), 9, 0.3, NET, segs=4, smooth=False)
    tz = ye - math.copysign(9, ye)
    p.box((x, tz, z + 11), (5, 5, 22), ROCK)                                   # bell tower
    p.cone((x, tz, z + 22), (x, tz, z + 32), 4, 0.1, NET, segs=4, smooth=False)
    p.box((x, tz - math.copysign(2.6, ye), z + 17), (1.6, 0.3, 2.4), YELLOW)
    # a big gnarled dead tree
    b = Vector((20, ye, g(20, ye)))
    p.cone(b, b + Vector((0, 0, 20)), 2.2, 0.5, NET, segs=6)
    rng = random.Random(7)
    for k in range(7):
        a = rng.uniform(0, math.tau)
        s = b + Vector((0, 0, 9 + k * 1.6))
        p.cone(s, s + Vector((math.cos(a) * 9, math.sin(a) * 9, 5)), 0.7, 0.1, NET, segs=4)


def hero_tower(p, ye, g):
    x = -8.0
    p.cone((x, ye, -2), (x, ye, 70), 3.2, 1.6, ROCK, segs=8)                  # the needle
    p.cone((x, ye, 52), (x, ye, 58), 10, 10, NET, segs=12)                     # observation deck
    p.cone((x, ye, 57.5), (x, ye, 58.5), 10.5, 10.5, TEAL, segs=12)            # lit ring (self-lit)
    p.cone((x, ye, 70), (x, ye, 82), 0.5, 0.1, ROCK, segs=5)
    p.ellipsoid((x, ye, 82.5), (0.9, 0.9, 0.9), RED, segs=(6, 4))
    rng = random.Random(31)
    for k in range(18):
        building(p, -60 + k * 7 + rng.uniform(-1, 1), ye * 1.15 + rng.uniform(-8, 8), -1,
                 rng.uniform(5, 8), rng.uniform(5, 8), rng.uniform(16, 45), NET, YELLOW, rng)


def hero_billboard_wall(p, ye, g):
    rng = random.Random(41)
    for k in range(14):
        building(p, -55 + k * 8 + rng.uniform(-1, 1), ye + rng.uniform(-6, 6), -1,
                 rng.uniform(6, 9), rng.uniform(6, 9), rng.uniform(20, 55), NET if k % 3 else ROCK, YELLOW, rng)
    to_court = -math.copysign(1, ye)
    for k, (x, z, w, h, col) in enumerate(((-20, 30, 22, 10, TEAL), (16, 22, 16, 8, RED), (0, 44, 12, 6, YELLOW))):
        p.box((x, ye + to_court * 6, z), (w, 0.8, h), NET)
        p.box((x, ye + to_court * 6.5, z), (w - 1.5, 0.3, h - 1.5), col)       # glowing screens


def hero_lighthouse(p, ye, g):
    x = -30.0
    p.ellipsoid((x, ye, -4), (30, 20, 12), ROCK, segs=(12, 7), smooth=False)   # headland
    p.ellipsoid((x + 6, ye, 6), (18, 14, 4), LEAF, segs=(10, 5), smooth=False)
    b = Vector((x, ye, 8))
    for k in range(5):  # red and white bands
        p.cone(b + Vector((0, 0, k * 3.2)), b + Vector((0, 0, (k + 1) * 3.2)), 2.8 - k * 0.25, 2.55 - k * 0.25,
               RED if k % 2 == 0 else WHITE, segs=12)
    p.cone(b + Vector((0, 0, 16)), b + Vector((0, 0, 18.5)), 1.8, 1.8, YELLOW, segs=10)   # the lamp
    p.cone(b + Vector((0, 0, 18.5)), b + Vector((0, 0, 21)), 2.2, 0.1, RED, segs=10, smooth=False)


def hero_pier(p, ye, g):
    x0 = -20.0
    for k in range(9):  # a pier striding out to sea
        x = x0 - k * 6
        p.box((x, ye, 1.2), (6.2, 5, 0.6), WOOD)
        for sy in (-2, 2):
            p.cone((x, ye + sy, -2), (x, ye + sy, 1), 0.35, 0.35, TRUNK, segs=5)
    # the Ferris wheel at the end of it
    c = Vector((x0 - 50, ye, 16))
    for k in range(12):
        a0, a1 = k / 12 * math.tau, (k + 1) / 12 * math.tau
        p.cone(c + Vector((math.cos(a0) * 13, 0, math.sin(a0) * 13)), c + Vector((math.cos(a1) * 13, 0, math.sin(a1) * 13)),
               0.4, 0.4, WHITE, segs=5)
        p.cone(c, c + Vector((math.cos(a0) * 13, 0, math.sin(a0) * 13)), 0.15, 0.15, WHITE, segs=4)
        p.box(c + Vector((math.cos(a0) * 13, 0, math.sin(a0) * 13 - 1.5)), (2, 2, 2), (RED, YELLOW, BLUE, TEAL)[k % 4])
    for sx in (-1, 1):
        p.cone(c + Vector((sx * 6, 0, -17)), c, 0.5, 0.4, WHITE, segs=5)


# ---------------------------------------------------------------- fitting + ground apron

CAM_BACK, CAM_UP, SKY_DEG = 16.0, 5.6, 9.5   # MatchCameraRig's baseline pose: what fits in frame


def fit_to_frame(p, new, ye):
    """Shrink (uniformly, about its footprint) the landmark's vertices `new` so its top
    stays inside the baseline camera's frame — a signature snowcap or spire must be SEEN."""
    p.bm.verts.ensure_lookup_table()
    if not new:
        return
    top = max(v.co.z for v in new)
    dist = abs(ye) + CAM_BACK
    limit = (CAM_UP + dist * math.tan(math.radians(SKY_DEG))) * 0.92
    if top <= limit:
        return
    k = limit / top
    # shrink about the landmark's own centre (not the court end) so nothing slides toward the court
    cx = sum(v.co.x for v in new) / len(new)
    cy = sum(v.co.y for v in new) / len(new)
    pivot = Vector((cx, cy, 0.0))
    for v in new:
        v.co = pivot + (v.co - pivot) * k


KEEP_OUT = 30.0  # no landmark geometry closer to the court than this along its length


def keep_clear(p, new, ye):
    """A hard rule for landmarks: nothing within KEEP_OUT of the court down its length (where the
    baseline cameras sit, 16m behind each baseline). A cone's skirt reaching in there rises right
    in front of a camera and reads as a giant rod or slab. Any such vertex is pinned to the line on
    the landmark's OWN side (pinning by the vertex's sign flung strays to the far baseline, stretching
    faces across the whole court)."""
    p.bm.verts.ensure_lookup_table()
    for v in new:
        if abs(v.co.x) < 60.0 and abs(v.co.y) < KEEP_OUT:
            v.co.y = math.copysign(KEEP_OUT, ye)


def apron(p, land, sea):
    """The arena terrain only reaches |x|<34, |y|<40; carry the ground on to the horizon so the
    far end never shows the edge of the world. Its grid lines up exactly with the terrain's edges
    and never overlaps it (a coarse sheet poking up through finer dunes shows as slabs), and it
    stays near the edge height with a gentle swell instead of climbing away."""
    def axis(edge, far, inner_cells, outer_cells):
        lo = [-far + (far - edge) * k / outer_cells for k in range(outer_cells)]
        mid = [-edge + 2 * edge * k / inner_cells for k in range(inner_cells)]
        hi = [edge + (far - edge) * k / outer_cells for k in range(outer_cells + 1)]
        return lo + mid + hi
    xs = axis(34.0, 220.0, 4, 10)
    ys = axis(40.0, 220.0, 4, 10)

    def h(x, y):
        bx, by = max(-34.0, min(34.0, x)), max(-40.0, min(40.0, y))
        edge = land(bx, by)                       # the terrain's own height at its border
        swell = 1.2 * noise.noise(Vector((x * 0.02, y * 0.02, 4.2)))
        far = min(1.0, math.hypot(x - bx, y - by) / 40.0)
        return edge + swell * far - 0.12

    verts = {}
    def v(i, j):
        if (i, j) not in verts:
            verts[i, j] = p.bm.verts.new((xs[i], ys[j], h(xs[i], ys[j])))
        return verts[i, j]
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            x0, x1, y0, y1 = xs[i], xs[i + 1], ys[j], ys[j + 1]
            if x1 > -34.001 and x0 < 34.001 and y1 > -40.001 and y0 < 40.001:
                continue  # the real terrain is there
            if sea and x1 < -24:
                continue  # the ocean mesh covers it
            f = p.bm.faces.new((v(i, j), v(i + 1, j), v(i + 1, j + 1), v(i, j + 1)))
            p.paint([f], SAND_D if (i * 3 + j) % 4 == 0 else SAND, smooth=False)


# ---------------------------------------------------------------- per-theme recipes

def vista_for(theme, land):
    """Build the vista mesh for a theme key."""
    p = pg.Prop("vista")
    rng0 = random.Random(hash(theme) & 0xffff)

    def far(elem, n=12, rmin=95, rmax=145, seed=1):
        for rng, x, y in far_fill(n, rmin, rmax, seed):
            elem(rng, x, y)

    def mid(elem, n=14, rmin=34, rmax=62, seed=2):
        for rng, x, y in end_ring(n, rmin, rmax, seed):
            elem(rng, Vector((x, y, land(x, y) - 0.3)))

    D = 42.0  # just beyond the arena's play area: close heroes read big from the baseline camera
    R = {
        "beach": (hero_lighthouse, hero_pier),
        "savanna": (hero_kilimanjaro, hero_pride_rock),
        "amazon": (hero_waterfall, hero_temple),
        "sahara": (hero_pyramids, hero_caravan),
        "arctic": (hero_glacier, hero_iceberg),
        "outback": (hero_uluru, hero_windmill_station),
        "himalaya": (hero_everest, hero_monastery),
        "forest": (hero_redwood, hero_mountain_lake),
        "rockies": (hero_mountain_lake, hero_redwood),
        "volcano": (hero_volcano, hero_lava_lake),
        "lunar": (hero_launch_pad, hero_moon_base),
        "atlantis": (hero_palace, hero_statue_head),
        "sky": (hero_sky_castle, hero_balloons),
        "graveyard": (hero_mansion, hero_chapel),
        "neon": (hero_tower, hero_billboard_wall),
    }
    near = D
    a, b = R[theme]
    for hero, ye in ((a, near), (b, -near)):
        p.bm.verts.ensure_lookup_table()
        # track the landmark's verts by identity: bmesh reuses slots freed by deletions, so
        # "everything past index N" can include older geometry (it once dragged the far
        # mansion's verts to the near baseline, smearing faces across the court)
        before = set(p.bm.verts)
        hero(p, ye, land)
        new = [v for v in p.bm.verts if v not in before]
        fit_to_frame(p, new, ye)
        keep_clear(p, new, ye)
    sea = theme == "beach" or _themes().get(theme, {}).get("water", False)
    apron(p, land, sea)

    # far filler + mid-ground, in the theme's own vocabulary
    if theme == "beach":
        far(lambda r, x, y: p.ellipsoid((x, y, -2), (r.uniform(12, 22), r.uniform(8, 14), r.uniform(3, 6)), SAND_D, segs=(9, 5), smooth=False))
        mid(lambda r, b: palm_tree(p, b, r.uniform(7, 11), r), n=16)
    elif theme == "savanna":
        far(lambda r, x, y: p.cone((x, y, -1), (x, y, r.uniform(5, 10)), r.uniform(10, 20), r.uniform(7, 14), TEAL if r.random() < 0.5 else ROCK, segs=9, smooth=False))
        mid(lambda r, b: tree_flat(p, b, r.uniform(7, 12), r), n=14)
    elif theme == "amazon":
        far(lambda r, x, y: tree_round(p, Vector((x, y, -1)), r.uniform(20, 30), rng=r), n=18, rmin=80, rmax=120)
        mid(lambda r, b: tree_round(p, b, r.uniform(10, 16), rng=r), n=18)
    elif theme == "sahara":
        far(lambda r, x, y: p.ellipsoid((x, y, -2), (r.uniform(16, 28), r.uniform(10, 18), r.uniform(4, 9)), SAND, segs=(10, 6)))
        mid(lambda r, b: (palm_tree(p, b, r.uniform(8, 11), r) if r.random() < 0.4 else
                          p.ellipsoid(b, (r.uniform(6, 10), r.uniform(4, 7), r.uniform(1.5, 3)), SAND_D, segs=(8, 5))), n=12)
    elif theme == "arctic":
        far(lambda r, x, y: mountain(p, x, y, 0, r.uniform(20, 34), r.uniform(14, 24), snow=0.6))
        mid(lambda r, b: conifer(p, b, r.uniform(8, 13), snow=True), n=16)
    elif theme == "outback":
        far(lambda r, x, y: p.ellipsoid((x, y, -2), (r.uniform(12, 22), r.uniform(8, 14), r.uniform(4, 9)), RED if r.random() < 0.4 else SAND_D, segs=(9, 5), smooth=False))
        mid(lambda r, b: tree_flat(p, b, r.uniform(8, 12), r), n=12)
    elif theme == "himalaya":
        far(lambda r, x, y: mountain(p, x, y, 0, r.uniform(30, 55), r.uniform(20, 32), snow=0.5), n=14)
        mid(lambda r, b: conifer(p, b, r.uniform(9, 14), snow=True), n=12)
    elif theme in ("forest", "rockies"):
        far(lambda r, x, y: (mountain(p, x, y, 0, r.uniform(25, 40), r.uniform(18, 28), snow=0.7) if theme == "rockies" and r.random() < 0.6
                             else conifer(p, Vector((x, y, -1)), r.uniform(14, 22))), n=20, rmin=75, rmax=130)
        mid(lambda r, b: conifer(p, b, r.uniform(10, 16)), n=20)
    elif theme == "volcano":
        far(lambda r, x, y: p.cone((x, y, -1), (x, y, r.uniform(10, 22)), r.uniform(10, 18), 1.0, SAND_D, segs=7, smooth=False))
        mid(lambda r, b: p.cone(b, b + Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(3, 6))), r.uniform(1.2, 2.0), 0.1, NET, segs=5, smooth=False), n=8)
    elif theme == "lunar":
        far(lambda r, x, y: p.ellipsoid((x, y, -3), (r.uniform(15, 30), r.uniform(12, 22), r.uniform(4, 9)), SAND_D, segs=(10, 5), smooth=False))
        mid(lambda r, b: p.ellipsoid(b, (r.uniform(2, 5),) * 2 + (r.uniform(1.5, 3),), ROCK, segs=(7, 5), smooth=False), n=14)
    elif theme == "atlantis":
        far(lambda r, x, y: p.ellipsoid((x, y, -2), (r.uniform(10, 18), r.uniform(8, 14), r.uniform(6, 12)), TEAL, segs=(9, 6), smooth=False), n=12, rmin=70, rmax=100)
        mid(lambda r, b: p.cone(b, b + Vector((0, 0, r.uniform(6, 12))), 0.5, 0.2, LEAF, segs=5) if r.random() < 0.5
            else p.ellipsoid(b + Vector((0, 0, 1.5)), (2.5, 2.5, 3), RED if r.random() < 0.5 else TEAL, segs=(7, 5)), n=18)
    elif theme == "sky":
        def island(r, x, y):
            rr = r.uniform(8, 16)
            z = r.uniform(8, 30)
            p.cone((x, y, z - rr * 1.3), (x, y, z), 0.5, rr, ROCK, segs=7, smooth=False)
            p.cone((x, y, z), (x, y, z + 1.2), rr, rr * 0.95, LEAF, segs=7, smooth=False)
        far(island, n=10)
        mid(lambda r, b: p.ellipsoid(b + Vector((0, 0, r.uniform(-2, 3))), (r.uniform(4, 8), r.uniform(3, 6), r.uniform(2, 4)), WHITE, segs=(9, 6)), n=14)
    elif theme == "graveyard":
        far(lambda r, x, y: p.ellipsoid((x, y, -3), (r.uniform(18, 30), r.uniform(14, 24), r.uniform(6, 12)), LEAF_D, segs=(10, 5), smooth=False))
        def stone(r, b):
            p.box(b + Vector((0, 0, 1)), (1.4, 0.4, 2.2), ROCK, rot=Matrix.Rotation(r.uniform(-0.2, 0.2), 3, "Y"))
        mid(stone, n=22)
    elif theme == "neon":
        far(lambda r, x, y: building(p, x, y, -1, r.uniform(7, 13), r.uniform(7, 13), r.uniform(18, 60), NET, YELLOW if r.random() < 0.7 else TEAL, r), n=24, rmin=70, rmax=125)
        mid(lambda r, b: p.box(b + Vector((0, 0, 1)), (r.uniform(2, 4), r.uniform(2, 4), 2), ROCK), n=10)
    return p.finish()


# ---------------------------------------------------------------- export / preview

def _themes():
    import arena_gen
    return arena_gen._all_themes()


def land_for(key):
    if key == "beach":
        return pg.height
    t = _themes()[key]["terrain"]
    return land_fn(t.get("amp", 1.0), t.get("sea", False), t.get("bumps", 0.12))


def out_dir(key):
    return BEACH_DIR if key == "beach" else os.path.join(ARENA_ROOT, _themes()[key]["folder"])


def export(keys):
    for k in keys:
        pg.clear_scene()
        obj = vista_for(k, land_for(k))
        obj.name = "vista"
        pg.export_mesh(obj, os.path.join(out_dir(k), "prop_vista.fbx"))


def preview(path, key):
    """Workbench render from roughly the baseline camera (Unity z=-15 ≈ Blender y=-15), plus a
    reverse shot from the other end — both views a player can have."""
    import bpy
    pg.clear_scene()
    obj = vista_for(key, land_for(key))
    import arena_gen  # noqa: F401
    if key != "beach":
        t = _themes()[key]
        terr = arena_gen.terrain(**t["terrain"])
    else:
        terr = pg.terrain()
    pal = pg.PALETTE if key == "beach" else _themes()[key]["palette"]
    img = bpy.data.images.new("pal", width=16, height=1)
    px = []
    for c in pal:
        px += [c[0], c[1], c[2], 1.0]
    img.pixels = px
    mat = bpy.data.materials.new("m")
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], nt.nodes.get("Principled BSDF").inputs["Base Color"])
    for o in (obj, terr):
        o.data.materials.append(mat)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.color_type = "TEXTURE"
    sc.display.shading.light = "STUDIO"
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    cam_data = bpy.data.cameras.new("c")
    cam_data.lens_unit = "FOV"
    cam_data.angle = math.radians(84)
    cam_data.clip_end = 600
    cam = bpy.data.objects.new("c", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    base, ext = os.path.splitext(path)
    # the in-game camera (MatchCameraRig): 5.6m up, 16m behind a baseline, aimed just past the net
    for tag, y, look in (("a", -16.0, 1.0), ("b", 16.0, -1.0)):
        cam.location = (0.0, y, 5.6)
        d = Vector((0.0, look * 16.5, 0.8 - 5.6))
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = f"{base}_{tag}{ext}"
        bpy.ops.render.render(write_still=True)
        print("[vista] preview ->", sc.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print(__doc__)
        return
    all_keys = ["beach"] + list(_themes())
    if argv[0] == "export":
        keys = all_keys if argv[1:] in ([], ["all"]) else argv[1:]
        export(keys)
    elif argv[0] == "preview":
        preview(os.path.abspath(argv[1]), argv[2])
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
