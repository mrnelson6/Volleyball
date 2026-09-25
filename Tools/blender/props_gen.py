"""Procedural beach props + terrain for the Animal Volleyball 3D look-dev / Sunset Beach.

Same palette-strip trick as animal_gen.py: every face's UVs point at one texel of a shared
16x1 palette (Assets/Art/Props/props_palette.png, written by this script), so ALL props share
one material and one texture, and the palette can be re-tuned without touching meshes.

Usage (headless):
  blender -b --factory-startup -P Tools/blender/props_gen.py -- export
  blender -b --factory-startup -P Tools/blender/props_gen.py -- export palm rock_a
Axis conventions while building: Blender Z-up. Unity maps Blender (x, y, z) -> (x, z, y)-ish via
the importer's axis conversion; the look-dev builder places props by name, so orientation of
individual props only matters for the terrain/court, which are built symmetric.
"""
import math
import os
import random
import struct
import sys
import zlib

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Props")

SLOTS = 16
PALETTE = [  # sRGB
    (0.96, 0.86, 0.62),  # 0 sand
    (0.90, 0.77, 0.52),  # 1 sand dark
    (0.55, 0.38, 0.22),  # 2 trunk
    (0.30, 0.68, 0.28),  # 3 leaf
    (0.18, 0.50, 0.22),  # 4 leaf dark
    (0.93, 0.28, 0.25),  # 5 red
    (0.97, 0.96, 0.92),  # 6 white
    (0.72, 0.52, 0.32),  # 7 wood
    (0.58, 0.56, 0.55),  # 8 rock
    (0.20, 0.62, 0.78),  # 9 water
    (0.99, 0.82, 0.22),  # 10 yellow
    (0.22, 0.50, 0.92),  # 11 blue
    (0.12, 0.12, 0.14),  # 12 net dark
    (0.25, 0.78, 0.72),  # 13 teal
    (1.00, 0.55, 0.15),  # 14 orange
    (0.80, 0.68, 0.48),  # 15 wet sand
]
SAND, SAND_D, TRUNK, LEAF, LEAF_D, RED, WHITE, WOOD, ROCK, WATER, YELLOW, BLUE, NET, TEAL, ORANGE, WET = range(16)


# ---------------------------------------------------------------- mesh building

class Prop:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def paint(self, faces, slot, smooth=True):
        u = ((slot + 0.5) / SLOTS, 0.5)
        for f in faces:
            f.smooth = smooth
            for loop in f.loops:
                loop[self.uv].uv = u

    @staticmethod
    def faces_of(verts):
        return list({f for v in verts for f in v.link_faces})

    def ellipsoid(self, c, r, slot, rot=None, segs=(12, 8), smooth=True):
        m = Matrix.Translation(Vector(c))
        if rot is not None:
            m = m @ rot.to_4x4()
        m = m @ Matrix.Diagonal((r[0], r[1], r[2], 1))
        v = bmesh.ops.create_uvsphere(self.bm, u_segments=segs[0], v_segments=segs[1], radius=1, matrix=m, calc_uvs=False)["verts"]
        f = self.faces_of(v)
        self.paint(f, slot, smooth)
        return f

    def box(self, c, size, slot, rot=None):
        m = Matrix.Translation(Vector(c))
        if rot is not None:
            m = m @ rot.to_4x4()
        m = m @ Matrix.Diagonal((size[0], size[1], size[2], 1))
        v = bmesh.ops.create_cube(self.bm, size=1, matrix=m, calc_uvs=False)["verts"]
        f = self.faces_of(v)
        self.paint(f, slot, smooth=False)
        return f

    def cone(self, base, tip, r1, r2, slot, segs=10, caps=True, smooth=True):
        base, tip = Vector(base), Vector(tip)
        d = tip - base
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        m = Matrix.Translation((base + tip) / 2) @ rot
        v = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=segs, radius1=r1, radius2=r2,
                                  depth=d.length, matrix=m, calc_uvs=False)["verts"]
        f = self.faces_of(v)
        self.paint(f, slot, smooth)
        return f

    def finish(self):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


# ---------------------------------------------------------------- props

def palm(seed=0):
    rng = random.Random(seed)
    p = Prop("palm")
    lean = Vector((rng.uniform(-0.25, 0.25), rng.uniform(0.15, 0.35), 0))
    pts = []
    h = 6.5
    for i in range(9):
        t = i / 8
        pts.append(Vector((0, 0, t * h)) + lean * (t * t) * h * 0.35)
    for i in range(8):
        r = 0.26 - 0.1 * i / 8
        p.cone(pts[i], pts[i + 1] + (pts[i + 1] - pts[i]) * 0.08, r, r * 0.8, TRUNK if i % 2 == 0 else WOOD, segs=8)
    top = pts[-1]
    for k in range(8):
        a = k / 8 * math.tau + rng.uniform(-0.2, 0.2)
        droop = math.radians(rng.uniform(25, 45))
        rot = Matrix.Rotation(a, 3, "Z") @ Matrix.Rotation(droop, 3, "Y")
        out = rot @ Vector((1, 0, 0))
        c = top + out * 1.25 + Vector((0, 0, -0.25))
        p.ellipsoid(c, (1.45, 0.34, 0.035), LEAF if k % 2 == 0 else LEAF_D, rot=rot, segs=(10, 6))
    for k in range(3):
        a = k / 3 * math.tau
        p.ellipsoid(top + Vector((math.cos(a) * 0.2, math.sin(a) * 0.2, -0.28)), (0.15, 0.15, 0.15), TRUNK, segs=(8, 6))
    return p.finish()


def umbrella():
    p = Prop("umbrella")
    p.cone((0, 0, 0), (0, 0, 2.3), 0.035, 0.035, WHITE, segs=8)
    faces = p.cone((0, 0, 1.85), (0, 0, 2.35), 1.45, 0.03, RED, segs=12, caps=True, smooth=False)
    # alternate canopy panels red/white by angle around the pole
    for f in faces:
        c = f.calc_center_median()
        a = math.atan2(c.y, c.x)
        sector = int(((a + math.pi) / math.tau) * 12) % 12
        p.paint([f], RED if sector % 2 == 0 else WHITE, smooth=False)
    p.ellipsoid((0, 0, 2.4), (0.07, 0.07, 0.07), WHITE, segs=(8, 6))
    return p.finish()


def towel():
    p = Prop("towel")
    colors = [TEAL, WHITE, TEAL, YELLOW, TEAL]
    for i, c in enumerate(colors):
        p.box((0, (i - 2) * 0.2, 0.012), (0.95, 0.2, 0.024), c)
    return p.finish()


def lifeguard_tower():
    p = Prop("lifeguard_tower")
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((sx * 0.7, sy * 0.7, 1.1), (0.12, 0.12, 2.2), WOOD)
    p.box((0, 0, 2.25), (1.9, 1.9, 0.12), WOOD)
    p.box((0, 0.35, 2.95), (1.6, 1.0, 1.3), WHITE)
    p.box((0, -0.19, 3.1), (1.2, 0.05, 0.6), BLUE)  # window
    p.cone((0, 0.35, 3.6), (0, 0.35, 4.3), 1.35, 0.02, RED, segs=4, smooth=False)
    for i in range(6):  # ladder
        p.box((0, -1.25, 0.2 + i * 0.36), (0.7, 0.08, 0.06), WOOD)
    for sx in (-1, 1):
        p.box((sx * 0.35, -1.15, 1.1), (0.07, 0.07, 2.3), WOOD, rot=Matrix.Rotation(math.radians(-12), 3, "X"))
    return p.finish()


def rock(seed, name):
    rng = random.Random(seed)
    p = Prop(name)
    v = bmesh.ops.create_icosphere(p.bm, subdivisions=1, radius=1.0, calc_uvs=False)["verts"]
    for vert in v:
        vert.co *= rng.uniform(0.75, 1.15)
        vert.co.z *= 0.6
    bmesh.ops.translate(p.bm, verts=v, vec=(0, 0, 0.25))
    p.paint(p.faces_of(v), ROCK, smooth=False)
    return p.finish()


def beach_ball():
    p = Prop("beach_ball")
    faces = p.ellipsoid((0, 0, 0), (1, 1, 1), WHITE, segs=(12, 8))
    cols = [RED, WHITE, BLUE, YELLOW, WHITE, TEAL]
    for f in faces:
        c = f.calc_center_median()
        if abs(c.z) > 0.9:
            p.paint([f], WHITE)
            continue
        a = math.atan2(c.y, c.x)
        sector = int(((a + math.pi) / math.tau) * 6) % 6
        p.paint([f], cols[sector])
    return p.finish()


def surfboard():
    p = Prop("surfboard")
    p.ellipsoid((0, 0, 1.0), (0.28, 0.05, 1.0), ORANGE, segs=(14, 8))
    p.ellipsoid((0, -0.02, 1.0), (0.05, 0.04, 0.95), WHITE, segs=(8, 6))
    return p.finish()


def cooler():
    p = Prop("cooler")
    p.box((0, 0, 0.22), (0.7, 0.45, 0.42), BLUE)
    p.box((0, 0, 0.47), (0.74, 0.49, 0.1), WHITE)
    return p.finish()


def tiki_torch():
    p = Prop("tiki_torch")
    p.cone((0, 0, 0), (0, 0, 1.9), 0.05, 0.04, WOOD, segs=6)
    p.cone((0, 0, 1.75), (0, 0, 2.1), 0.12, 0.14, TRUNK, segs=8)
    p.cone((0, 0, 2.05), (0, 0, 2.5), 0.12, 0.0, ORANGE, segs=7)
    p.cone((0, 0, 2.05), (0, 0, 2.35), 0.07, 0.0, YELLOW, segs=6)
    return p.finish()


def court():
    """Lines + posts + a strung net. Unity-space: court centred on origin, X across, net along X.

    Blender Z-up with the Unity importer's axis conversion: Blender X -> Unity X,
    Blender Y -> Unity Z (sign handled by bakeAxisConversion). Court is symmetric in Y so
    the sign doesn't matter; the net runs along Blender X at Y=0.
    """
    p = Prop("court")
    hw, hd, nh = 4.0, 8.0, 2.2
    lw = 0.1
    for sx in (-1, 1):
        p.box((sx * hw, 0, 0.012), (lw, hd * 2 + lw, 0.024), WHITE)
    for y in (-hd, 0, hd):
        p.box((0, y, 0.012), (hw * 2, lw, 0.024), WHITE)
    post_x = hw + 0.5
    for sx in (-1, 1):
        p.cone((sx * post_x, 0, 0), (sx * post_x, 0, nh + 0.15), 0.07, 0.06, WHITE, segs=10)
        p.ellipsoid((sx * post_x, 0, nh + 0.17), (0.08, 0.08, 0.05), RED, segs=(8, 5))
    top, bottom = nh, nh - 1.0
    p.box((0, 0, top - 0.04), (post_x * 2, 0.04, 0.08), WHITE)  # top tape
    p.box((0, 0, bottom), (post_x * 2, 0.03, 0.04), WHITE)
    n = int(post_x * 2 / 0.22)
    for i in range(n + 1):
        x = -post_x + i * (post_x * 2 / n)
        p.box((x, 0, (top + bottom) / 2), (0.018, 0.018, top - bottom), NET)
    for j in range(1, 5):
        z = bottom + j * (top - bottom) / 5
        p.box((0, 0, z), (post_x * 2, 0.018, 0.018), NET)
    for sx in (-1, 1):  # antennae
        p.box((sx * hw, 0, top + 0.4), (0.025, 0.025, 1.2), RED)
    return p.finish()


def terrain():
    """Low-poly sand: flat court pad, dunes around it, sloping to the sea on Blender -X."""
    p = Prop("terrain")
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
                if cc.z < -0.35:
                    slot = WET
                else:
                    nval = noise.noise(Vector((cc.x * 0.21, cc.y * 0.21, 3.1)))
                    slot = SAND_D if nval > 0.25 else SAND
                p.paint([f], slot, smooth=False)
    return p.finish()


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def height(x, y):
    # distance outside the flat pad. The pad matches the game's scoring ground sheet (CourtKit:
    # ~x +-16, z +-22) so an out-of-bounds ball never visibly sinks into a dune before it lands.
    dx = max(0.0, abs(x) - 16.0)
    dy = max(0.0, abs(y) - 22.0)
    outside = math.hypot(dx, dy)
    dune = (0.7 * math.sin(0.33 * x + 1.3) * math.cos(0.27 * y) + 0.45 * math.sin(0.55 * y + 0.2 * x)
            + 0.5 * noise.noise(Vector((x * 0.12, y * 0.12, 0.7))))
    h = smoothstep(0.0, 6.0, outside) * (dune + 0.4 + outside * 0.06)
    sea = max(0.0, -x - 18.0)  # beach slopes toward the sea on the far side of the court
    h = h * (1.0 - smoothstep(0.0, 8.0, sea)) - sea * 0.09
    return h


def ocean():
    p = Prop("ocean")
    x0, x1, y0, y1 = -160.0, -21.0, -160.0, 160.0
    nx, ny = 20, 30
    grid = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            x = x0 + (x1 - x0) * i / nx
            y = y0 + (y1 - y0) * j / ny
            z = -0.55 + 0.18 * noise.noise(Vector((x * 0.08, y * 0.08, 1.9)))
            grid[i, j] = p.bm.verts.new((x, y, z))
    for i in range(nx):
        for j in range(ny):
            f = p.bm.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
            p.paint([f], WATER if (i * 7 + j * 3) % 5 else TEAL, smooth=False)
    return p.finish()


def pedestal():
    """Character-select showcase stand: a low sand drum with a darker rim, a couple of shells."""
    p = Prop("pedestal")
    p.cone((0, 0, -0.35), (0, 0, 0.0), 1.35, 1.25, SAND_D, segs=20, smooth=False)
    p.cone((0, 0, -0.01), (0, 0, 0.02), 1.22, 1.22, SAND, segs=20, smooth=False)
    p.ellipsoid((0.85, 0.55, 0.02), (0.12, 0.09, 0.04), WHITE, segs=(8, 4))
    p.ellipsoid((-0.7, 0.75, 0.02), (0.10, 0.08, 0.035), ORANGE, segs=(8, 4))
    return p.finish()


PROPS = {
    "palm": lambda: palm(1), "palm_b": lambda: palm(7), "umbrella": umbrella, "towel": towel,
    "lifeguard_tower": lifeguard_tower, "rock_a": lambda: rock(3, "rock_a"), "rock_b": lambda: rock(11, "rock_b"),
    "beach_ball": beach_ball, "surfboard": surfboard, "cooler": cooler, "tiki_torch": tiki_torch,
    "court": court, "terrain": terrain, "ocean": ocean, "pedestal": pedestal,
}


# ---------------------------------------------------------------- output

def write_palette_png(path, palette=None):
    raw = b""
    raw += b"\x00" + b"".join(bytes(int(round(c * 255)) for c in col) + b"\xff" for col in (palette or PALETTE))
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", SLOTS, 1, 8, 6, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)


def export_mesh(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", bake_anim=False)
    print(f"[props_gen] wrote {path} ({len(obj.data.polygons)} faces)")


def export(names):
    os.makedirs(OUT_DIR, exist_ok=True)
    write_palette_png(os.path.join(OUT_DIR, "props_palette.png"))
    for name in names:
        clear_scene()
        obj = PROPS[name]()
        obj.name = name
        export_mesh(obj, os.path.join(OUT_DIR, f"prop_{name}.fbx"))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv or argv[0] != "export":
        print(__doc__)
        return
    export(argv[1:] or list(PROPS))


if __name__ == "__main__":  # importable by arena_gen.py for its mesh helpers
    main()
