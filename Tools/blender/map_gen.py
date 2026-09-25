"""Toon 3D world map for the World Tour screen.

Continents come from the SAME ellipse-blob table the old pixel map used
(Assets/Editor/WorldMapArt.cs: Land / Ice / Clouds), and landmarks sit at each region's
RegionDef.mapSpot (Assets/Scripts/Campaign/RegionDef.cs) — both parsed straight from the C#, so
the UI pins (placed by mapSpot) always line up. Low-poly raised land, biome colours around each
stop, a little 3D landmark per region, shallows around the coasts, clouds over the finals.

Map space: u,v in [0,1] -> x = (u-0.5)*36, y = (v-0.5)*18 (Blender units, Z up).
Unity (Assets/Editor/Art3D/WorldTourArtBaker.cs) renders it top-down into the UI map image.

    blender -b --factory-startup -P Tools/blender/map_gen.py -- export
"""
import math
import os
import re
import sys

import bpy
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import props_gen as pg  # noqa: E402
from props_gen import (SAND, SAND_D, TRUNK, LEAF, LEAF_D, RED, WHITE, WOOD, ROCK, WATER,  # noqa: E402
                       YELLOW, BLUE, NET, TEAL, ORANGE, WET)
import arena_gen  # noqa: E402
import arena_themes_more as more  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Map")
W, H = 36.0, 18.0

PALETTE = [
    (0.93, 0.84, 0.60), (0.84, 0.72, 0.48), (0.45, 0.32, 0.20), (0.46, 0.70, 0.36),
    (0.26, 0.52, 0.28), (0.84, 0.42, 0.24), (0.97, 0.98, 1.00), (0.66, 0.50, 0.32),
    (0.58, 0.56, 0.58), (0.20, 0.50, 0.78), (0.93, 0.78, 0.40), (0.30, 0.55, 0.95),
    (0.12, 0.12, 0.14), (0.36, 0.72, 0.84), (0.95, 0.66, 0.35), (0.80, 0.70, 0.50),
]

BIOME = {"savanna": YELLOW, "amazon": LEAF_D, "outback": RED, "himalaya": ROCK,
         "forest": LEAF_D, "sahara": ORANGE, "rockies": ROCK, "arctic": WHITE}
MOUNTAINS = ("himalaya", "rockies")


# ---------------------------------------------------------------- data from the C#

def parse_blobs(name):
    src = open(os.path.join(ROOT, "Assets", "Editor", "WorldMapArt.cs"), encoding="utf-8").read()
    block = re.search(r"static readonly Vector4\[\] " + name + r"\s*=\s*\{(.*?)\};", src, re.S).group(1)
    return [tuple(float(x.rstrip("f")) for x in m)
            for m in re.findall(r"new Vector4\(([-\d.]+f?),\s*([-\d.]+f?),\s*([-\d.]+f?),\s*([-\d.]+f?)\)", block)]


def parse_spots():
    src = open(os.path.join(ROOT, "Assets", "Scripts", "Campaign", "RegionDef.cs"), encoding="utf-8").read()
    spots = {}
    for block in re.split(r"new RegionDef\s*\{", src)[1:]:
        rid = re.search(r'id = "(\w+)"', block).group(1)
        m = re.search(r"mapSpot = new Vector2\(([-\d.]+)f,\s*([-\d.]+)f\)", block)
        spots[rid] = (float(m.group(1)), float(m.group(2)))
    return spots


def field(blobs, u, v):
    best = -1e9
    for bx, by, bz, bw in blobs:
        du, dv = (u - bx) / bz, (v - by) / bw
        best = max(best, 1.0 - math.sqrt(du * du + dv * dv))
    return best


def to_xy(u, v):
    return (u - 0.5) * W, (v - 0.5) * H


# ---------------------------------------------------------------- terrain

def build_world(land, ice, spots):
    p = pg.Prop("world")
    # the grid runs past the map on every side as open sea, so the tilted camera never sees an edge
    U0, U1, V0, V1 = -0.35, 1.35, -0.45, 1.45
    nx, ny = int(288 * (U1 - U0)), int(144 * (V1 - V0))

    def sample(u, v):
        wob = noise.noise(Vector((u * 14.3, v * 14.3, 0.3))) * 0.125
        f = field(land, u, v) + wob
        iced = field(ice, u, v) + wob > 0  # (no Antarctic band: it read as a wall from the tilted view)
        return f, iced

    def near_region(u, v):
        best, bid = 1e9, None
        for rid, (su, sv) in spots.items():
            d = math.hypot((u - su) * 2.0, v - sv)
            if d < best:
                best, bid = d, rid
        return bid, best

    grid = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            u, v = U0 + (U1 - U0) * i / nx, V0 + (V1 - V0) * j / ny
            f, iced = sample(u, v)
            # continuous height through the coast (no staircase): sea floor -> beach -> hills
            h = max(-0.14, min(f, 0.6)) * 1.5
            if iced and f <= 0:
                h = 0.12
            if f > 0:
                rid, d = near_region(u, v)
                if rid in MOUNTAINS and d < 0.09:
                    h += (1.0 - d / 0.09) ** 1.5 * 1.8 * (0.7 + 0.3 * noise.noise(Vector((u * 40, v * 40, 1))))
                h += noise.noise(Vector((u * 25, v * 25, 2.0))) * 0.06 * min(1.0, f * 10)
            x, y = to_xy(u, v)
            grid[i, j] = p.bm.verts.new((x, y, h))

    for i in range(nx):
        for j in range(ny):
            a, b, c, d = grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]
            for tri in ((a, b, c), (a, c, d)) if (i + j) % 2 == 0 else ((a, b, d), (b, c, d)):
                f = p.bm.faces.new(tri)
                cc = f.calc_center_median()
                u, v = cc.x / W + 0.5, cc.y / H + 0.5
                fv, iced = sample(u, v)
                if iced:
                    slot = WHITE
                elif fv <= 0:
                    slot = TEAL if fv > -0.10 else WATER
                elif fv < 0.05:
                    slot = SAND
                else:
                    rid, dist = near_region(u, v)
                    if rid in BIOME and dist < 0.085:
                        slot = BIOME[rid]
                        if rid in MOUNTAINS and cc.z > 1.1:
                            slot = WHITE  # snow caps
                    else:
                        slot = LEAF_D if noise.noise(Vector((u * 9, v * 9, 5))) > 0.2 else LEAF
                p.paint([f], slot, smooth=False)
    return p.finish()


# ---------------------------------------------------------------- landmarks

LANDMARK_SCALE = 1.8  # landmarks read as little dioramas from the tilted map camera


def place(obj, u, v, scale, z=0.0, yaw=0.0):
    scale *= LANDMARK_SCALE
    x, y = to_xy(u, v)
    obj.matrix_world = Matrix.Translation((x, y, z)) @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Scale(scale, 4)
    return obj


def ground_z(u, v, land):
    f = field(land, u, v)
    return max(-0.14, min(f, 0.6)) * 1.5


def landmarks(spots, land):
    objs = []
    s = spots
    gz = lambda rid: ground_z(*s[rid], land)  # noqa: E731

    objs.append(place(arena_gen.acacia(2), *s["savanna"], 0.12, gz("savanna")))
    objs.append(place(arena_gen.acacia(9), s["savanna"][0] + 0.012, s["savanna"][1] - 0.02, 0.09, gz("savanna")))
    objs.append(place(arena_gen.jungle_tree(3), *s["amazon"], 0.07, gz("amazon")))
    objs.append(place(arena_gen.jungle_tree(8), s["amazon"][0] - 0.012, s["amazon"][1] + 0.02, 0.06, gz("amazon")))

    rock = pg.Prop("uluru")
    rock.ellipsoid((0, 0, 0), (1.0, 0.55, 0.4), RED, segs=(12, 6), smooth=False)
    objs.append(place(rock.finish(), *s["outback"], 0.45, gz("outback")))

    objs.append(place(more.stupa(), *s["himalaya"], 0.14, gz("himalaya") + 0.9))
    for k, (du, dv) in enumerate(((0.0, 0.0), (0.012, 0.012), (-0.012, 0.008))):
        objs.append(place(more.fir(2 + k), s["forest"][0] + du, s["forest"][1] + dv, 0.08, gz("forest")))

    pyr = pg.Prop("pyramid")
    pyr.cone((0, 0, 0), (0, 0, 1.0), 1.1, 0.02, SAND_D, segs=4, smooth=False)
    objs.append(place(pyr.finish(), *s["sahara"], 0.38, gz("sahara"), yaw=0.3))
    objs.append(place(more.cabin(roof_slot=RED), *s["rockies"], 0.12, gz("rockies") + 0.6))
    objs.append(place(arena_gen.igloo(), *s["arctic"], 0.16, gz("arctic")))

    # the finals: a cloud castle floating over the ocean
    fu, fv = s["skyfinals"]
    for k, (du, dv, sc) in enumerate(((0, 0, 0.5), (0.02, -0.01, 0.4), (-0.018, 0.006, 0.38))):
        objs.append(place(more.cloud_puff(2 + k), fu + du, fv + dv, sc, 0.4))
    objs.append(place(more.cloud_tower(3), fu, fv, 0.1, 0.8))
    return objs


# ---------------------------------------------------------------- export

def export():
    os.makedirs(OUT_DIR, exist_ok=True)
    pg.write_palette_png(os.path.join(OUT_DIR, "palette.png"), PALETTE)
    pg.clear_scene()
    land, ice = parse_blobs("Land"), parse_blobs("Ice")
    spots = parse_spots()
    world = build_world(land, ice, spots)
    marks = landmarks(spots, land)

    # bake each landmark's placement into its mesh, then merge everything into one object
    bpy.ops.object.select_all(action="DESELECT")
    for o in marks:
        o.select_set(True)
    bpy.context.view_layer.objects.active = marks[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in [world] + marks:
        o.select_set(True)
    bpy.context.view_layer.objects.active = world
    bpy.ops.object.join()
    world.name = "world_map"
    pg.export_mesh(world, os.path.join(OUT_DIR, "prop_world_map.fbx"))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if argv[:1] != ["export"]:
        print(__doc__)
        return
    export()


if __name__ == "__main__":
    main()
