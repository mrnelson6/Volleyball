"""Procedural 3D animal generator for Animal Volleyball.

One shared bipedal skeleton + per-species parts (head template, ears, horns, neck, tail,
markings) driven by Tools/blender/species.json — the 3D twin of Assets/Editor/CharacterArt.cs.
Every animal gets the SAME bone names and hierarchy, and the same keyframed clip set.

Colour is not baked in. Every part's UVs point at one texel of a 16x1 palette strip (see
SLOT_* below), so a single material + a runtime palette recolours fur, accent, jersey, etc.
(Unity side: AnimalPalette). That is what lets jerseys change per team at runtime.

Usage (headless):
  blender -b --factory-startup -P Tools/blender/animal_gen.py -- export fox bear penguin giraffe
  blender -b --factory-startup -P Tools/blender/animal_gen.py -- export all
  blender -b --factory-startup -P Tools/blender/animal_gen.py -- export --detail 0.45 --suffix _lp fox
  blender -b --factory-startup -P Tools/blender/animal_gen.py -- preview out.png fox:Idle:0 bear:Spike:6

Axis conventions while building: Blender Z-up, animals face -Y, +X is the animal's left.
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SPECIES_JSON = os.path.join(HERE, "species.json")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Characters")

FPS = 30
BASE_HEIGHT = 1.8  # world height of a height-1.0 animal (matches the old sprite size)

# Mesh detail: 1 = default. <1 gives chunky faceted low-poly (and flat face normals), >1 smooth toy.
DETAIL = 1.0
SUFFIX = ""


def seg(n, lo=3):
    return max(lo, int(round(n * DETAIL)))

# ---------------------------------------------------------------- palette slots
# Must match Assets/Scripts/Art3D/AnimalPalette.cs
SLOTS = 16
SLOT_FUR, SLOT_ACCENT, SLOT_JERSEY, SLOT_MARK, SLOT_NOSE, SLOT_EYE_WHITE, SLOT_EYE_DARK, \
    SLOT_SHORTS, SLOT_HORN, SLOT_TRIM = range(10)
# fixed "wardrobe" colours for per-character looks (glasses, hats, jewellery, tattoos, scars)
SLOT_DARK, SLOT_GOLD, SLOT_RED, SLOT_TEAL, SLOT_PINK, SLOT_TAN = range(10, 16)
WARDROBE = {
    SLOT_DARK: [0.13, 0.12, 0.15], SLOT_GOLD: [0.98, 0.76, 0.18], SLOT_RED: [0.86, 0.18, 0.20],
    SLOT_TEAL: [0.10, 0.68, 0.66], SLOT_PINK: [0.98, 0.56, 0.70], SLOT_TAN: [0.93, 0.78, 0.58],
}

JERSEY_PREVIEW = [0.20, 0.50, 0.95]


def uv_for(slot):
    return ((slot + 0.5) / SLOTS, 0.5)


# ---------------------------------------------------------------- mesh builder

class MeshBuilder:
    """Accumulates primitives into one bmesh, each rigidly bound to one bone and one palette slot."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.deform = self.bm.verts.layers.deform.verify()
        self.groups = []

    def _group(self, bone):
        if bone not in self.groups:
            self.groups.append(bone)
        return self.groups.index(bone)

    def _finish(self, verts, slot, bone):
        gi = self._group(bone)
        faces = {f for v in verts for f in v.link_faces}
        u = uv_for(slot)
        for f in faces:
            f.smooth = DETAIL >= 0.75  # low-poly keeps hard facets in the mesh itself
            for loop in f.loops:
                loop[self.uv].uv = u
        for v in verts:
            v[self.deform][gi] = 1.0

    def ellipsoid(self, center, radii, slot, bone, rot=None, segs=(14, 9)):
        m = Matrix.Translation(Vector(center))
        if rot is not None:
            m = m @ rot.to_4x4()
        m = m @ Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0))
        r = bmesh.ops.create_uvsphere(self.bm, u_segments=seg(segs[0], 5), v_segments=seg(segs[1], 3),
                                      radius=1.0, matrix=m, calc_uvs=False)
        self._finish(r["verts"], slot, bone)

    def sphere(self, center, radius, slot, bone, segs=(12, 8)):
        self.ellipsoid(center, (radius, radius, radius), slot, bone, segs=segs)

    def capsule(self, a, b, radius, slot, bone, segs=(12, 8), squash=1.0):
        """A stretched ellipsoid spanning a->b (chunky toy limbs)."""
        a, b = Vector(a), Vector(b)
        d = b - a
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix()
        self.ellipsoid((a + b) / 2, (radius, radius * squash, d.length / 2 + radius * 0.6),
                       slot, bone, rot=rot, segs=segs)

    def cone(self, base, tip, r1, r2, slot, bone, segs=10, caps=True):
        base, tip = Vector(base), Vector(tip)
        d = tip - base
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        m = Matrix.Translation((base + tip) / 2) @ rot
        r = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=seg(segs, 4),
                                  radius1=r1, radius2=r2, depth=d.length, matrix=m, calc_uvs=False)
        self._finish(r["verts"], slot, bone)

    def dome(self, center, radii, slot, bone, cut=0.0, rot=None, segs=(16, 10)):
        """Ellipsoid with everything below local z = cut removed (hat crowns, caps)."""
        m = Matrix.Translation(Vector(center))
        if rot is not None:
            m = m @ rot.to_4x4()
        m = m @ Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0))
        r = bmesh.ops.create_uvsphere(self.bm, u_segments=seg(segs[0], 5), v_segments=seg(segs[1], 3),
                                      radius=1.0, matrix=m, calc_uvs=False)
        inv = m.inverted()
        drop = [v for v in r["verts"] if (inv @ v.co).z < cut - 1e-4]
        bmesh.ops.delete(self.bm, geom=drop, context="VERTS")
        self._finish([v for v in r["verts"] if v.is_valid], slot, bone)

    def tube(self, center, rx, ry, h, slot, bone, rot=None, caps=True, segs=16, taper=1.0):
        """Elliptical cylinder of height h along local Z (hat bands, headbands, lenses)."""
        m = Matrix.Translation(Vector(center))
        if rot is not None:
            m = m @ rot.to_4x4()
        m = m @ Matrix.Diagonal((rx, ry, h, 1.0))
        r = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=seg(segs, 6),
                                  radius1=1.0, radius2=taper, depth=1.0, matrix=m, calc_uvs=False)
        self._finish(r["verts"], slot, bone)

    def hoop(self, center, radius, thick, axis, slot, bone, n=12, arc=1.0):
        """A torus-ish ring (earrings, glasses rims, nose rings): a closed chain of short tubes."""
        c, ax = Vector(center), Vector(axis).normalized()
        u = ax.orthogonal().normalized()
        v = ax.cross(u)
        steps = max(6, int(n * arc))
        pts = [c + (u * math.cos(k / n * math.tau) + v * math.sin(k / n * math.tau)) * radius
               for k in range(steps + 1)]
        for a, b in zip(pts, pts[1:]):
            d = b - a
            self.cone(a - d * 0.15, b + d * 0.15, thick, thick, slot, bone, segs=6)

    def patch(self, pos, normal, su, sv, slot, bone, angle=0.0, thick=0.012):
        """Flat decal-like ellipse lying on a surface (scars, plasters, tattoos, blush)."""
        n = Vector(normal).normalized()
        rot = Matrix.Rotation(angle, 3, n) @ Vector((0, 0, 1)).rotation_difference(n).to_matrix()
        self.ellipsoid(pos, (su, sv, thick), slot, bone, rot=rot, segs=(10, 6))

    def reshape(self, bones, center, scale):
        """Non-uniformly scale every vertex bound to `bones` about `center` (head shapes)."""
        idx = {self.groups.index(b) for b in bones if b in self.groups}
        c = Vector(center)
        for vert in self.bm.verts:
            if any(g in idx for g in vert[self.deform].keys()):
                d = vert.co - c
                vert.co = c + Vector((d.x * scale[0], d.y * scale[1], d.z * scale[2]))

    def ring(self, a, b, t, radius, width, slot, bone):
        """A band around the a->b limb at parameter t (stripes)."""
        a, b = Vector(a), Vector(b)
        c = a.lerp(b, t)
        d = (b - a).normalized()
        self.cone(c - d * width / 2, c + d * width / 2, radius, radius, slot, bone, segs=12, caps=False)

    def spot(self, a, b, t, angle, radius, size, slot, bone):
        """A flattened patch on the surface of the a->b limb (spots, patches)."""
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        u = d.orthogonal().normalized()
        v = d.cross(u)
        n = u * math.cos(angle) + v * math.sin(angle)
        c = a.lerp(b, t) + n * radius * 0.92
        rot = Vector((0, 0, 1)).rotation_difference(n).to_matrix()
        self.ellipsoid(c, (size, size * 0.8, size * 0.3), slot, bone, rot=rot, segs=(8, 5))

    def to_object(self, name, arm_obj):
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for g in self.groups:
            obj.vertex_groups.new(name=g)
        obj.parent = arm_obj
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = arm_obj
        return obj


# ---------------------------------------------------------------- skeleton

def mirror(name):
    if name.endswith(".L"):
        return name[:-2] + ".R"
    if name.endswith(".R"):
        return name[:-2] + ".L"
    return name


def mx(p):
    return Vector((-p[0], p[1], p[2]))


FWD = Vector((0, -1, 0))
UP = Vector((0, 0, 1))


class Skeleton:
    """Joint positions for one animal (unscaled, before fitting to target height)."""

    def __init__(self, sp):
        self.sp = sp
        neck = sp["neck"]
        head_kind = sp["head"]
        self.hip_z = 0.56
        self.leg_x = 0.14
        lk = look(sp)
        self.head_r = (0.28 if head_kind == "Beak" else 0.30) * lk.get("head", 1.0)
        self.head_shape = Vector(lk.get("head_shape", (1.0, 1.0, 1.0)))
        self.neck_base = Vector((0, 0, 1.08))
        self.neck_top = Vector((0, 0, 1.08 + 0.06 + neck * 0.55))
        self.head_c = self.neck_top + Vector((0, 0, self.head_r * 0.85))
        # arm length stretches the elbow/wrist down from the shoulder; shoulders set the width
        al = lk.get("arm_len", 1.0)
        self.shoulder = Vector((0.29 * lk.get("shoulders", 1.0), 0, 1.00))
        self.elbow = self.shoulder + Vector((0.08, 0.0, -0.22)) * al
        self.wrist = self.elbow + Vector((0.03, -0.02, -0.22)) * al
        self.hip = Vector((self.leg_x, 0, self.hip_z))
        self.knee = Vector((self.leg_x, -0.01, 0.31))
        self.ankle = Vector((self.leg_x, 0.0, 0.08))
        self.toe = Vector((self.leg_x, -0.17, 0.05))
        tl = max(sp["tail"], 0.35)
        self.tail0 = Vector((0, 0.20, 0.60))
        self.tail1 = self.tail0 + Vector((0, 0.16, 0.10)) * tl
        self.tail2 = self.tail1 + Vector((0, 0.12, 0.20)) * tl

    def on_head(self, offset):
        """Head-centre offset -> world point after the per-character head reshape."""
        o = Vector(offset)
        return self.head_c + Vector((o.x * self.head_shape.x, o.y * self.head_shape.y, o.z * self.head_shape.z))

    def bones(self):
        """(name, head, tail, parent, roll-axis) — roll axis is where the bone's local Z points."""
        hc, nt = self.head_c, self.neck_top
        b = [
            ("Hips", Vector((0, 0, self.hip_z)), Vector((0, 0, self.hip_z + 0.14)), None, FWD),
            ("Spine", Vector((0, 0, self.hip_z + 0.14)), Vector((0, 0, 0.90)), "Hips", FWD),
            ("Chest", Vector((0, 0, 0.90)), self.neck_base, "Spine", FWD),
            ("Neck", self.neck_base, nt, "Chest", FWD),
            ("Head", nt, hc + Vector((0, 0, self.head_r * self.head_shape.z)), "Neck", FWD),
            ("Tail1", self.tail0, self.tail1, "Hips", UP),
            ("Tail2", self.tail1, self.tail2, "Tail1", UP),
        ]
        for side in (1, -1):
            s = ".L" if side == 1 else ".R"
            f = (lambda p: Vector(p)) if side == 1 else mx
            b += [
                ("Ear" + s, self.on_head(f((0.17, 0.0, self.head_r * 0.6))), self.on_head(f((0.22, 0.0, self.head_r * 1.3))), "Head", FWD),
                ("UpperArm" + s, f(self.shoulder), f(self.elbow), "Chest", FWD),
                ("LowerArm" + s, f(self.elbow), f(self.wrist), "UpperArm" + s, FWD),
                ("Hand" + s, f(self.wrist), f(self.wrist) + Vector((0, -0.02, -0.10)), "LowerArm" + s, FWD),
                ("UpperLeg" + s, f(self.hip), f(self.knee), "Hips", FWD),
                ("LowerLeg" + s, f(self.knee), f(self.ankle), "UpperLeg" + s, FWD),
                ("Foot" + s, f(self.ankle), f(self.toe), "LowerLeg" + s, UP),
            ]
        return b


def build_armature(name, skel):
    arm = bpy.data.armatures.new(name + "_Armature")
    obj = bpy.data.objects.new("Rig", arm)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    made = {}
    for bname, head, tail, parent, roll_axis in skel.bones():
        eb = arm.edit_bones.new(bname)
        eb.head = head
        eb.tail = tail
        eb.align_roll(roll_axis)
        eb.use_deform = True
        if parent:
            eb.parent = made[parent]
        made[bname] = eb
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj



# ---------------------------------------------------------------- species identity features
# The shared parts (head template, ears, horns, markings) make every animal buildable, but on
# their own a lion is a bear with a tail. These per-species extras carry the silhouette cues
# that make each animal read at a glance. Art-only data, so it lives here rather than in
# CharacterDef. Colours come from the existing palette slots (mane/mohawk/rings use MARK).
FEATURES = {
    "lion": {"mane": True, "whiskers": True},
    "jaguar": {"whiskers": True},
    "cougar": {"whiskers": True},
    "snowleopard": {"whiskers": True},
    "rhino": {"nose_horn": True},
    "buffalo": {"curved_horns": True, "horn_boss": True},
    "yak": {"curved_horns": True, "fringe": True},
    "markhor": {"spiral_horns": True, "beard": True},
    "oryx": {"long_horns": True},
    "zebra": {"mohawk": True},
    "warthog": {"mohawk": True},
    "boar": {"mohawk": True},
    "camel": {"hump": True},
    "moose": {"dewlap": True},
    "fennec": {"ear_scale": 1.45},
    "jerboa": {"ear_scale": 1.35, "foot_scale": 1.4},
    "hare": {"foot_scale": 1.35, "whiskers": True},
    "kangaroo": {"foot_scale": 1.5},
    "raccoon": {"ringed_tail": True, "mask_band": True},
    "redpanda": {"ringed_tail": True},
    "badger": {"head_stripe": True},
    "toucan": {"beak_scale": 1.6, "beak_fat": 1.5},
    "snowyowl": {"beak_scale": 0.45, "facial_disc": True},
    "emu": {"beak_scale": 0.75},
    "penguin": {"white_belly": True},
    "walrus": {"big_tusks": True, "whisker_pad": True},
    "sloth": {"claws": True},
    "wombat": {"nose_scale": 1.8},
    "capybara": {"nose_scale": 1.4},
}

# species whose horn/tusk silhouette is built by a feature instead of the generic style
CUSTOM_HORNS = ("nose_horn", "curved_horns", "spiral_horns", "long_horns", "big_tusks")


def feats(sp):
    return FEATURES.get(sp["id"], {})


def chain(mb, pts, r0, r1, slot, bone, segs=8):
    """Tapered tube through a list of points (curved horns)."""
    n = len(pts) - 1
    for i in range(n):
        ra = r0 + (r1 - r0) * i / n
        rb = r0 + (r1 - r0) * (i + 1) / n
        mb.cone(pts[i], pts[i + 1] + (pts[i + 1] - pts[i]) * 0.1, ra, rb, slot, bone, segs=segs)


def add_features(mb, sp, skel):
    fx = feats(sp)
    hc, hr = skel.head_c, skel.head_r
    head = sp["head"]
    snout_y = {"LongMuzzle": -hr * 1.1, "Muzzle": -hr * 0.9}.get(head, -hr * 0.85)

    if fx.get("mane"):
        # fluffy halo behind and around the face, plus a chin tuft
        mb.ellipsoid(hc + Vector((0, hr * 0.28, -hr * 0.05)), (hr * 1.30, hr * 0.62, hr * 1.25), SLOT_MARK, "Head", segs=(16, 10))
        for k in range(12):
            a = k / 12 * math.tau
            p = hc + Vector((math.cos(a) * hr * 1.18, hr * 0.05, math.sin(a) * hr * 1.12))
            mb.sphere(p, 0.12, SLOT_MARK, "Head", segs=(8, 6))
        mb.ellipsoid(hc + Vector((0, -hr * 0.35, -hr * 0.95)), (0.16, 0.10, 0.12), SLOT_MARK, "Head", segs=(10, 6))

    if fx.get("whiskers"):
        for side in (1, -1):
            root = hc + Vector((side * hr * 0.28, snout_y - 0.02, -hr * 0.30))
            for dz in (0.03, -0.01, -0.05):
                tip = root + Vector((side * 0.22, 0.03, dz * 2.0))
                mb.cone(root, tip, 0.008, 0.003, SLOT_EYE_DARK, "Head", segs=4)

    if fx.get("whisker_pad"):
        for side in (1, -1):
            mb.ellipsoid(hc + Vector((side * hr * 0.28, -hr * 0.95, -hr * 0.35)), (0.13, 0.10, 0.11), SLOT_ACCENT, "Head")

    if fx.get("nose_horn"):
        base = hc + Vector((0, -hr * 1.05, -hr * 0.05))
        mb.cone(base, base + Vector((0, -0.10, 0.26)), 0.075, 0.012, SLOT_HORN, "Head", segs=10)
        base2 = hc + Vector((0, -hr * 0.62, hr * 0.25))
        mb.cone(base2, base2 + Vector((0, -0.04, 0.12)), 0.05, 0.01, SLOT_HORN, "Head", segs=8)

    for side in (1, -1):
        if fx.get("curved_horns"):
            b = hc + Vector((side * hr * 0.72, 0.02, hr * 0.55))
            pts = [b, b + Vector((side * 0.14, 0.0, -0.02)), b + Vector((side * 0.26, -0.02, 0.06)),
                   b + Vector((side * 0.30, -0.05, 0.20))]
            chain(mb, pts, 0.065, 0.012, SLOT_HORN, "Head")
        if fx.get("spiral_horns"):
            b = hc + Vector((side * hr * 0.30, 0.03, hr * 0.85))
            pts = []
            for k in range(10):
                t = k / 9
                a = t * math.tau * 1.6
                pts.append(b + Vector((side * (0.03 + 0.07 * t + 0.035 * math.cos(a)),
                                       0.10 * t + 0.035 * math.sin(a), 0.42 * t)))
            chain(mb, pts, 0.05, 0.01, SLOT_HORN, "Head", segs=7)
        if fx.get("long_horns"):
            b = hc + Vector((side * hr * 0.25, 0.04, hr * 0.85))
            mb.cone(b, b + Vector((side * 0.05, 0.28, 0.58)), 0.035, 0.008, SLOT_HORN, "Head", segs=8)
        if fx.get("big_tusks"):
            b = hc + Vector((side * hr * 0.28, -hr * 1.05, -hr * 0.55))
            mb.cone(b, b + Vector((side * 0.02, -0.03, -0.30)), 0.035, 0.01, SLOT_TRIM, "Head", segs=8)
    if fx.get("horn_boss"):
        mb.ellipsoid(hc + Vector((0, 0.0, hr * 0.78)), (hr * 0.85, hr * 0.45, hr * 0.28), SLOT_HORN, "Head", segs=(12, 7))

    if fx.get("fringe"):  # shaggy yak fringe over the brow and a skirt below the jaw
        for k in range(9):
            x = (k / 8 - 0.5) * hr * 1.6
            mb.ellipsoid(hc + Vector((x, -hr * 0.55, hr * 0.62)), (0.07, 0.06, 0.12), SLOT_FUR, "Head", segs=(7, 5))
        mb.ellipsoid(hc + Vector((0, -hr * 0.1, -hr * 0.95)), (hr * 0.95, hr * 0.8, hr * 0.35), SLOT_FUR, "Head")

    if fx.get("beard"):
        b = hc + Vector((0, -hr * 1.0, -hr * 0.75))
        mb.cone(b, b + Vector((0, -0.02, -0.26)), 0.07, 0.02, SLOT_MARK, "Head", segs=8)

    if fx.get("mohawk"):
        for k in range(7):
            t = k / 6
            ang = math.radians(-35 + 125 * t)  # from forehead over the crown to the nape
            p = hc + Vector((0, math.sin(ang) * hr * 0.97, math.cos(ang) * hr * 0.97))
            n = (p - hc).normalized()
            mb.cone(p - n * 0.02, p + n * 0.13, 0.05, 0.01, SLOT_MARK, "Head", segs=5)

    if fx.get("white_belly"):
        # the classic tuxedo front: a white belly pushing out through the jersey, down to the shorts
        mb.ellipsoid((0, -0.10, 0.80), (0.21, 0.19, 0.30), SLOT_TRIM, "Spine", segs=(14, 9))
        mb.ellipsoid((0, -0.06, 0.62), (0.20, 0.19, 0.12), SLOT_TRIM, "Hips", segs=(12, 7))

    if fx.get("hump"):
        mb.ellipsoid((0, 0.24, 1.02), (0.20, 0.17, 0.20), SLOT_FUR, "Chest")

    if fx.get("dewlap"):
        mb.ellipsoid(hc + Vector((0, -hr * 0.55, -hr * 1.1)), (0.07, 0.06, 0.14), SLOT_FUR, "Head", segs=(8, 6))

    if fx.get("head_stripe"):  # white blaze from the crown down between the eyes to the snout
        for ang in (0, 22, 44, 66):
            a = math.radians(ang)
            p = hc + Vector((0, -math.sin(a) * hr * 0.97, math.cos(a) * hr * 0.97))
            n = (p - hc).normalized()
            rot = Vector((0, 0, 1)).rotation_difference(n).to_matrix()
            mb.ellipsoid(p, (0.055, 0.09, 0.02), SLOT_TRIM, "Head", rot=rot, segs=(8, 5))

    if fx.get("mask_band"):  # bandit mask across both eyes (drawn under the eyes)
        mb.ellipsoid(hc + Vector((0, -hr * 0.78, hr * 0.14)), (hr * 0.78, hr * 0.17, hr * 0.26), SLOT_MARK, "Head", segs=(14, 8))

    if fx.get("facial_disc"):
        mb.ellipsoid(hc + Vector((0, -hr * 0.5, 0)), (hr * 0.85, hr * 0.5, hr * 0.8), SLOT_TRIM, "Head", segs=(14, 9))

    if fx.get("ringed_tail") and sp["tail"] > 0.05:
        for a, b, bone in ((skel.tail0, skel.tail1, "Tail1"), (skel.tail1, skel.tail2, "Tail2")):
            for tt in (0.35, 0.8):
                mb.ring(a, b, tt, 0.125, 0.06, SLOT_MARK, bone)

    if fx.get("claws"):
        for side in (1, -1):
            s = ".L" if side == 1 else ".R"
            w = Vector(skel.wrist) if side == 1 else mx(skel.wrist)
            for k in (-1, 0, 1):
                b = w + Vector((k * 0.035, -0.04, -0.10))
                mb.cone(b, b + Vector((0, -0.05, -0.10)), 0.018, 0.004, SLOT_HORN, "Hand" + s, segs=5)


# ---------------------------------------------------------------- per-character looks
# FEATURES make a lion read as a lion; LOOKS make Leo read as Leo. Proportions (head size and
# shape, arm/leg thickness, belly, eyes) plus a personality kit: hats, hair, glasses, brows,
# scars, jewellery, tattoos. Wardrobe colours are the fixed SLOT_DARK..SLOT_TAN palette slots;
# SLOT_JERSEY gives team-coloured gear (caps).
#   head / eyes / arms / legs / shoulders / arm_len : scale factors (1 = shared body)
#   head_shape (x, y, z)  : squash/stretch of the whole head (width, depth, height)
#   belly                 : torso width/depth factor (or (x, y))
LOOKS = {
    "fox": {"head_shape": (0.95, 1.0, 1.05), "arms": 0.9, "hair": "quiff", "hair_col": SLOT_ACCENT,
            "brows": "smug", "wristbands": SLOT_TEAL},
    "bear": {"head_shape": (1.12, 1.0, 0.94), "arms": 1.4, "shoulders": 1.1, "belly": 1.15, "eyes": 0.85,
             "hat": "cap_back", "hat_col": SLOT_JERSEY, "scar": 1},
    "meerkat": {"head": 1.15, "eyes": 1.2, "arms": 0.8, "legs": 0.85, "hair": "tuft", "hair_col": SLOT_MARK,
                "neck": "binoculars"},
    "zebra": {"head_shape": (0.92, 1.0, 1.06), "legs": 0.9, "glasses": "visor", "glasses_col": SLOT_DARK,
              "wristbands": SLOT_RED},
    "warthog": {"head_shape": (1.18, 1.0, 0.9), "arms": 1.2, "belly": 1.1, "eyes": 0.9, "glasses": "sun",
                "nose_ring": True},
    "giraffe": {"eyes": 1.1, "lashes": True, "flower": 1, "blush": True},
    "lion": {"shoulders": 1.1, "arms": 1.15, "hat": "crown", "neck": "medallion"},
    "rhino": {"head_shape": (1.15, 1.05, 0.95), "arms": 1.35, "shoulders": 1.12, "belly": 1.12, "eyes": 0.8,
              "hat": "cap", "hat_col": SLOT_JERSEY, "brows": "angry"},
    "capybara": {"head_shape": (1.05, 1.12, 0.9), "belly": 1.12, "arms": 0.9, "lids": True, "yuzu": True},
    "toucan": {"head": 1.05, "eyes": 1.1, "hair": "tuft", "hair_col": SLOT_ACCENT, "neck": "beads",
               "neck_col": SLOT_RED},
    "sloth": {"head": 1.05, "arm_len": 1.2, "arms": 0.85, "lids": True, "lid_col": SLOT_MARK, "flower": -1},
    "jaguar": {"head_shape": (1.05, 1.0, 0.95), "arms": 1.1, "brows": "angry", "tattoo": "bands", "earring": 1},
    "wombat": {"head_shape": (1.18, 1.0, 0.86), "belly": 1.2, "arms": 1.25, "legs": 1.2, "arm_len": 0.85,
               "hat": "headband", "hat_col": SLOT_RED, "kneepads": SLOT_DARK},
    "dingo": {"head_shape": (0.95, 1.05, 1.0), "neck": "bandana", "neck_col": SLOT_RED, "freckles": True},
    "emu": {"eyes": 1.3, "legs": 0.8, "arms": 0.8, "hair": "spiky", "hair_col": SLOT_DARK, "brows": "worried"},
    "kangaroo": {"gloves": True, "arms": 1.15, "shoulders": 1.05, "hat": "headband", "hat_col": SLOT_TRIM},
    "redpanda": {"head": 1.12, "eyes": 1.1, "blush": True, "neck": "scarf", "neck_col": SLOT_TEAL},
    "yak": {"arms": 1.3, "belly": 1.15, "shoulders": 1.08, "neck": "bell", "earring": 1},
    "markhor": {"head_shape": (0.92, 1.05, 1.05), "hat": "headband", "hat_col": SLOT_TEAL, "earring": -1},
    "snowleopard": {"arms": 0.95, "scar": -1, "earring": 1, "lashes": True},
    "hare": {"buck_teeth": True, "freckles": True, "hat": "headband", "hat_col": SLOT_PINK, "legs": 0.9},
    "badger": {"head_shape": (1.1, 1.0, 0.92), "arms": 1.2, "belly": 1.1, "hat": "hard_hat",
               "kneepads": SLOT_DARK},
    "boar": {"head_shape": (1.12, 1.0, 0.94), "arms": 1.2, "brows": "angry", "bandaid": True,
             "wristbands": SLOT_DARK},
    "stag": {"shoulders": 1.05, "glasses": "monocle", "neck": "bowtie", "neck_col": SLOT_DARK},
    "jerboa": {"head": 1.25, "eyes": 1.3, "arms": 0.75, "legs": 0.85, "bow": True},
    "fennec": {"head": 1.1, "glasses": "round_sun", "glasses_col": SLOT_GOLD, "blush": True},
    "oryx": {"head_shape": (0.92, 1.05, 1.05), "shoulders": 1.08, "neck": "scarf", "neck_col": SLOT_TAN},
    "camel": {"head_shape": (0.95, 1.1, 1.0), "lashes": True, "neck": "beads", "neck_col": SLOT_RED},
    "raccoon": {"arms": 0.95, "hat": "beanie", "hat_col": SLOT_DARK, "brows": "smug"},
    "moose": {"head_shape": (1.0, 1.12, 1.02), "eyes": 0.85, "brows": "worried", "neck": "scarf",
              "neck_col": SLOT_RED},
    "buffalo": {"head_shape": (1.1, 1.0, 0.95), "arms": 1.4, "shoulders": 1.15, "belly": 1.1, "brows": "angry",
                "nose_ring": True},
    "cougar": {"hair": "ponytail", "hair_col": SLOT_MARK, "wristbands": SLOT_PINK, "lashes": True},
    "penguin": {"head": 1.1, "eyes": 1.1, "neck": "bowtie", "neck_col": SLOT_RED},
    "snowyowl": {"eyes": 1.15, "glasses": "round", "glasses_col": SLOT_DARK},
    "walrus": {"head_shape": (1.1, 1.0, 0.95), "belly": 1.3, "arms": 1.2, "tattoo": "heart", "brows": "bushy",
               "brow_col": SLOT_TRIM},
    "polarbear": {"head": 0.9, "head_shape": (0.95, 1.1, 0.95), "arms": 1.35, "shoulders": 1.1, "belly": 1.15,
                  "eyes": 0.85, "hat": "beanie_pom", "hat_col": SLOT_TEAL},
}

# hats that would fight the species' own headgear are skipped (antlers)
HAT_BLOCKERS = ("Antlers",)


def look(sp):
    return LOOKS.get(sp["id"], {})


def add_look(mb, sp, skel):
    lk = look(sp)
    if not lk:
        return
    hc, hr = skel.head_c, skel.head_r
    es = lk.get("eyes", 1.0)
    head = sp["head"]
    A, B, C = hr * 1.05, hr * 0.95, hr  # head ellipsoid radii (before the reshape)
    face_y = -hr * 0.88

    def surf(x, z, lift=0.01):
        """Front-of-head surface point at (x, z) head-local, and its normal."""
        y = -B * math.sqrt(max(0.02, 1 - (x / A) ** 2 - (z / C) ** 2))
        n = Vector((x / A ** 2, y / B ** 2, z / C ** 2)).normalized()
        return hc + Vector((x, y, z)) + n * lift, n

    def on_dir(d, lift=0.0):
        """Point on the head surface along direction d (hats, hair)."""
        n = Vector(d).normalized()
        r = 1.0 / math.sqrt((n.x / A) ** 2 + (n.y / B) ** 2 + (n.z / C) ** 2)
        g = Vector((n.x * r / A ** 2, n.y * r / B ** 2, n.z * r / C ** 2)).normalized()
        return hc + n * r + g * lift, g

    def band(z0, z1, slot, grow=1.07):
        """A strip hugging the head between heights z0..z1 (headbands, hat cuffs)."""
        s0 = math.sqrt(max(0.05, 1 - (z0 / C) ** 2))
        s1 = math.sqrt(max(0.05, 1 - (z1 / C) ** 2))
        mb.tube(hc + Vector((0, 0, (z0 + z1) / 2)), A * s0 * grow, B * s0 * grow, z1 - z0, slot, "Head",
                caps=False, segs=20, taper=s1 / s0)

    def patch_along(pos, n, along, su, sv, slot, bone="Head", thick=0.012):
        n = Vector(n).normalized()
        x = Vector(along) - n * Vector(along).dot(n)
        x.normalize()
        y = n.cross(x)
        rot = Matrix((x, y, n)).transposed()
        mb.ellipsoid(pos, (su, sv, thick), slot, bone, rot=rot, segs=(10, 6))

    def eye(side):
        return hc + Vector((side * hr * 0.38, face_y, hr * 0.18))

    # snout / nose anchors per head template (matches build_body)
    ns = feats(sp).get("nose_scale", 1.0)
    snout_c, snout_r, nose = {
        "Muzzle": (Vector((0, -hr * 0.78, -hr * 0.28)), Vector((0.14, 0.12, 0.10)), Vector((0, -hr * 1.18, -hr * 0.16))),
        "LongMuzzle": (Vector((0, -hr * 0.85, -hr * 0.38)), Vector((0.15, 0.21, 0.12)), Vector((0, -hr * 1.48, -hr * 0.30))),
        "Round": (Vector((0, -hr * 0.86, -hr * 0.22)), Vector((0.11, 0.06, 0.08)), Vector((0, -hr * (1.0 + 0.1 * (ns - 1)), -hr * 0.10))),
    }.get(head, (Vector((0, -hr * 0.72, -hr * 0.15)), Vector((0.1, 0.1, 0.1)), Vector((0, -hr * 1.2, -hr * 0.2))))
    snout_c, nose = hc + snout_c, hc + nose

    # --------------------------------------------------------------- eyes: lids, lashes, brows
    for side in (1, -1):
        e = eye(side)
        if lk.get("lids"):  # sleepy half-closed lids
            mb.ellipsoid(e + Vector((0, -0.012 * es, 0.045 * es)), (0.076 * es, 0.048 * es, 0.062 * es),
                         lk.get("lid_col", SLOT_FUR), "Head", segs=(10, 7))
        if lk.get("lashes"):
            for a in (25, 50, 75):
                r = math.radians(a)
                base = e + Vector((side * math.cos(r) * 0.066 * es, -0.022 * es, math.sin(r) * 0.078 * es))
                tip = base + Vector((side * math.cos(r) * 0.05, -0.012, math.sin(r) * 0.045))
                mb.cone(base, tip, 0.011, 0.003, SLOT_DARK, "Head", segs=4)

    brows = lk.get("brows")
    if brows:
        top = hr * 0.18 + 0.08 * es + 0.035
        thick = 0.03 if brows == "bushy" else 0.018
        for side in (1, -1):
            xi, xo = side * (hr * 0.38 - 0.055), side * (hr * 0.38 + 0.065)
            zi, zo = {"angry": (top - 0.03, top + 0.012), "worried": (top + 0.02, top - 0.02),
                      "bushy": (top, top - 0.005)}.get(brows, (top, top))
            if brows == "smug":
                zi, zo = (top + 0.03, top + 0.025) if side == 1 else (top - 0.02, top + 0.005)
            a, _ = surf(xi, zi, 0.018)
            b, _ = surf(xo, zo, 0.018)
            mb.capsule(a, b, thick, lk.get("brow_col", SLOT_DARK), "Head", segs=(8, 6))

    # --------------------------------------------------------------- face marks
    if lk.get("scar"):
        side = lk["scar"]
        pts = [surf(side * hr * (0.58 - 0.34 * t), hr * (0.78 - 1.05 * t), 0.006) for t in [k / 6 for k in range(7)]]
        for (p0, _), (p1, _) in zip(pts, pts[1:]):
            mb.cone(p0, p1, 0.017, 0.017, SLOT_PINK, "Head", segs=5)
        for k in (1, 5):  # stitches across it (skip the eye in the middle)
            p, n = pts[k]
            d = (pts[k + 1][0] - pts[k - 1][0]).normalized()
            perp = n.cross(d).normalized()
            mb.cone(p - perp * 0.028 + n * 0.004, p + perp * 0.028 + n * 0.004, 0.006, 0.006, SLOT_DARK, "Head", segs=4)

    if lk.get("blush"):
        for side in (1, -1):
            p, n = surf(side * hr * 0.68, -hr * 0.08, 0.004)
            mb.patch(p, n, 0.055, 0.035, SLOT_PINK, "Head", thick=0.01)

    if lk.get("freckles"):
        for side in (1, -1):
            for x, z in ((0.52, -0.02), (0.64, -0.10), (0.56, -0.20), (0.70, 0.02)):
                p, n = surf(side * hr * x, hr * z, 0.004)
                mb.sphere(p, 0.011, SLOT_DARK, "Head", segs=(6, 4))

    if lk.get("bandaid"):  # crossed plasters on the forehead
        p, n = surf(-hr * 0.30, hr * 0.62, 0.008)
        for ang in (40, -40):
            r = math.radians(ang)
            patch_along(p, n, Vector((math.cos(r), 0, math.sin(r))), 0.075, 0.024, SLOT_TAN)

    if lk.get("buck_teeth"):
        for side in (1, -1):
            mb.ellipsoid(snout_c + Vector((side * 0.021, -snout_r.y * 0.82, -snout_r.z * 0.9)),
                         (0.019, 0.011, 0.03), SLOT_TRIM, "Head", segs=(8, 5))

    if lk.get("nose_ring"):
        mb.hoop(nose + Vector((0, -0.01, -0.045)), 0.036, 0.009, (1, 0, 0), SLOT_GOLD, "Head")

    # --------------------------------------------------------------- glasses
    g = lk.get("glasses")
    gcol = lk.get("glasses_col", SLOT_DARK)
    if g in ("sun", "round", "round_sun"):
        for side in (1, -1):
            e = eye(side) + Vector((0, -0.045 * es, 0.0))
            if g == "sun":
                mb.ellipsoid(e + Vector((0, 0.004, 0)), (0.086 * es, 0.02, 0.068 * es), SLOT_DARK, "Head", segs=(12, 7))
            else:
                mb.hoop(e, 0.085 * es, 0.011, (0, 1, 0), gcol, "Head", n=16)
                if g == "round_sun":
                    mb.ellipsoid(e + Vector((0, 0.006, 0)), (0.082 * es, 0.012, 0.082 * es), SLOT_DARK, "Head", segs=(12, 7))
            # temple arm back to the side of the head
            back, _ = on_dir((side * 1.0, 0.05, 0.25), 0.01)
            mb.capsule(e + Vector((side * 0.08 * es, 0.01, 0.02)), back, 0.011, gcol, "Head", segs=(6, 4))
        l, r = eye(1) + Vector((-0.07 * es, -0.05 * es, 0.03)), eye(-1) + Vector((0.07 * es, -0.05 * es, 0.03))
        mb.capsule(l, r, 0.012, gcol, "Head", segs=(6, 4))
    elif g == "visor":  # wraparound sports shades
        # a thin curved band across both eyes, wrapped to the head
        mb.ellipsoid(hc + Vector((0, face_y + hr * 0.18, hr * 0.2)), (hr * 1.0, hr * 0.46, 0.06 * es + 0.01),
                     gcol, "Head", segs=(20, 7))
        mb.ellipsoid(hc + Vector((0, face_y + hr * 0.18, hr * 0.2 + 0.05 * es + 0.012)), (hr * 1.0, hr * 0.47, 0.012),
                     SLOT_TEAL, "Head", segs=(20, 5))
        for side in (1, -1):
            back, _ = on_dir((side * 1.0, 0.1, 0.25), 0.012)
            mb.capsule(hc + Vector((side * hr * 0.85, face_y * 0.7, hr * 0.2)), back, 0.013, SLOT_DARK, "Head", segs=(6, 4))
    elif g == "monocle":
        e = eye(-1) + Vector((0, -0.05 * es, 0))
        mb.hoop(e, 0.088 * es, 0.011, (0, 1, 0), SLOT_GOLD, "Head", n=16)
        prev = e + Vector((-0.06, 0, -0.065))
        for k in range(1, 5):  # little chain dangling to the cheek
            p = prev + Vector((-0.012, 0.018, -0.03))
            mb.sphere(p, 0.009, SLOT_GOLD, "Head", segs=(5, 4))
            prev = p

    # --------------------------------------------------------------- mustache
    if lk.get("mustache"):
        for side in (1, -1):
            a = nose + Vector((0, 0.02, -0.05))
            pts = [a, a + Vector((side * 0.07, 0.01, -0.02)), a + Vector((side * 0.13, 0.03, 0.0)),
                   a + Vector((side * 0.155, 0.04, 0.035))]
            chain(mb, pts, 0.03, 0.01, lk["mustache"], "Head")

    # --------------------------------------------------------------- hats
    hat, hcol = lk.get("hat"), lk.get("hat_col", SLOT_RED)
    if hat and sp["horns"] in HAT_BLOCKERS:
        hat = None
    if hat in ("cap", "cap_back"):
        mb.dome(hc + Vector((0, 0.01, hr * 0.42)), (hr * 1.12, hr * 1.05, hr * 0.8), hcol, "Head")
        mb.sphere(hc + Vector((0, 0.01, hr * 1.22)), 0.024, hcol, "Head", segs=(6, 4))
        fwd = -1 if hat == "cap" else 1
        mb.ellipsoid(hc + Vector((0, fwd * hr * 1.02, hr * 0.46)), (hr * 0.72, hr * 0.58, 0.018), hcol, "Head",
                     rot=Matrix.Rotation(math.radians(-fwd * 12), 3, "X"), segs=(14, 6))
    elif hat in ("beanie", "beanie_pom"):
        mb.dome(hc + Vector((0, 0.01, hr * 0.36)), (hr * 1.12, hr * 1.06, hr * 0.94), hcol, "Head")
        band(hr * 0.30, hr * 0.56, hcol, grow=1.12)
        if hat == "beanie_pom":
            mb.sphere(hc + Vector((0, 0.01, hr * 1.36)), 0.075, SLOT_TRIM, "Head", segs=(10, 7))
    elif hat == "headband":
        band(hr * 0.42, hr * 0.64, hcol)
        # tie tails at the back
        k, _ = on_dir((0.15, 1.0, 0.55), 0.01)
        mb.capsule(k, k + Vector((0.05, 0.1, -0.14)), 0.025, hcol, "Head", segs=(6, 4))
        mb.capsule(k, k + Vector((-0.03, 0.12, -0.10)), 0.025, hcol, "Head", segs=(6, 4))
    elif hat == "crown":
        c = hc + Vector((0, -hr * 0.28, hr * 1.0))
        tilt = Matrix.Rotation(math.radians(14), 3, "X")
        up = tilt @ Vector((0, 0, 1))
        mb.tube(c, hr * 0.55, hr * 0.55, hr * 0.3, SLOT_GOLD, "Head", rot=tilt, caps=False, segs=18)
        mb.sphere(c, hr * 0.47, SLOT_RED, "Head", segs=(10, 7))
        for k in range(6):
            a = k / 6 * math.tau
            rim = c + tilt @ Vector((math.cos(a) * hr * 0.55, math.sin(a) * hr * 0.55, hr * 0.15))
            mb.cone(rim - up * 0.01, rim + up * 0.09, 0.035, 0.006, SLOT_GOLD, "Head", segs=5)
            mb.sphere(rim + up * 0.1, 0.014, SLOT_GOLD, "Head", segs=(5, 4))
        mb.sphere(c + tilt @ Vector((0, -hr * 0.57, 0)), 0.024, SLOT_TEAL, "Head", segs=(6, 5))
    elif hat == "hard_hat":
        mb.dome(hc + Vector((0, 0, hr * 0.45)), (hr * 1.12, hr * 1.08, hr * 0.88), SLOT_GOLD, "Head")
        mb.tube(hc + Vector((0, -hr * 0.06, hr * 0.47)), hr * 1.3, hr * 1.28, 0.03, SLOT_GOLD, "Head", segs=20)
        mb.ellipsoid(hc + Vector((0, 0, hr * 1.3)), (0.05, hr * 0.95, 0.04), SLOT_GOLD, "Head", segs=(8, 6))
        lamp = hc + Vector((0, -hr * 1.05, hr * 0.78))
        mb.tube(lamp, 0.055, 0.055, 0.06, SLOT_DARK, "Head", rot=Matrix.Rotation(math.radians(90), 3, "X"), segs=10)
        mb.ellipsoid(lamp + Vector((0, -0.032, 0)), (0.045, 0.012, 0.045), SLOT_TRIM, "Head", segs=(8, 5))

    if lk.get("yuzu"):  # capybara + citrus, the internet's favourite combo
        top = hc + Vector((0, hr * 0.05, hr * 1.0 + 0.07))
        mb.sphere(top, 0.085, SLOT_GOLD, "Head", segs=(12, 8))
        mb.ellipsoid(top + Vector((0.03, 0.0, 0.085)), (0.035, 0.018, 0.012), SLOT_TEAL, "Head",
                     rot=Matrix.Rotation(math.radians(-20), 3, "Y"), segs=(6, 4))

    if lk.get("bow"):
        k, n = on_dir((0.35, -0.25, 0.9), 0.02)
        mb.sphere(k, 0.026, SLOT_PINK, "Head", segs=(8, 5))
        for side in (1, -1):
            mb.cone(k, k + Vector((side * 0.095, 0.0, 0.025)), 0.012, 0.055, SLOT_PINK, "Head", segs=4)

    if lk.get("flower"):
        side = lk["flower"]
        if sp["ears"] == "None":
            c, n = on_dir((side * 0.85, -0.15, 0.45), 0.02)
        else:
            c, n = on_dir((side * 0.75, -0.35, 0.55), 0.03)
        u = n.orthogonal().normalized()
        v = n.cross(u)
        for k in range(5):
            a = k / 5 * math.tau
            mb.sphere(c + (u * math.cos(a) + v * math.sin(a)) * 0.042, 0.034, SLOT_PINK, "Head", segs=(8, 5))
        mb.sphere(c + n * 0.012, 0.028, SLOT_GOLD, "Head", segs=(8, 5))

    # --------------------------------------------------------------- hair
    hair, hcol2 = lk.get("hair"), lk.get("hair_col", SLOT_MARK)
    if hair == "quiff":
        pts = [hc + Vector((0, hr * 0.35, hr * 0.85)), hc + Vector((0, -hr * 0.15, hr * 1.12)),
               hc + Vector((0, -hr * 0.62, hr * 1.14)), hc + Vector((0, -hr * 0.86, hr * 0.92))]
        chain(mb, pts, 0.13, 0.05, hcol2, "Head", segs=10)
    elif hair == "spiky":
        for dx, dy in ((0, 0), (0.4, 0.15), (-0.4, 0.15), (0.2, -0.35), (-0.2, -0.35), (0.22, 0.45), (-0.22, 0.45),
                       (0, 0.6)):
            base, n = on_dir((dx, dy, 1.0))
            tip = base + (n + Vector((dx, dy, 0)) * 0.9).normalized() * 0.17
            mb.cone(base - n * 0.03, tip, 0.05, 0.008, hcol2, "Head", segs=5)
    elif hair == "tuft":
        base, n = on_dir((0, -0.1, 1.0))
        for tip in ((0, -0.06, 0.2), (0.06, 0.02, 0.16), (-0.06, 0.02, 0.15)):
            mid = base + Vector(tip) * 0.55 + Vector((0, -0.02, 0))
            chain(mb, [base - n * 0.02, mid, base + Vector(tip)], 0.03, 0.006, hcol2, "Head", segs=5)
    elif hair == "ponytail":
        base, _ = on_dir((0, 0.85, 0.6), 0.01)
        pts = [base, base + Vector((0, 0.12, 0.02)), base + Vector((0, 0.2, -0.1)), base + Vector((0, 0.2, -0.3))]
        chain(mb, pts, 0.08, 0.025, hcol2, "Head", segs=8)
        mb.sphere(base + Vector((0, 0.05, 0.01)), 0.05, SLOT_PINK, "Head", segs=(8, 5))

    # --------------------------------------------------------------- ears
    er = lk.get("earring")
    if er and sp["ears"] != "None":
        for side in ((1, -1) if er == 2 else (er,)):
            s = ".L" if side == 1 else ".R"
            if sp["ears"] == "Pointed":
                p = hc + Vector((side * (hr * 0.55 + 0.07), -0.02, hr * 0.62))
            elif sp["ears"] == "Round":
                p = hc + Vector((side * (hr * 0.66 + 0.075), 0.0, hr * 0.74 - 0.045))
            elif sp["ears"] == "Droopy":
                p = hc + Vector((side * (hr * 1.02 - 0.03), -0.02, hr * 0.05 - 0.13))  # droopy tip swings inward
            else:
                p = hc + Vector((side * (hr * 0.45), 0.0, hr * 0.95))
            mb.hoop(p + Vector((0, 0, -0.03)), 0.03, 0.007, (0, 1, 0), SLOT_GOLD, "Ear" + s, n=10)

    # --------------------------------------------------------------- neck & chest
    lk_by = lk.get("belly", 1.0)
    bx, by = lk_by if isinstance(lk_by, tuple) else (lk_by, lk_by)

    def torso_pt(a, z, lift=0.0):
        """Torso-surface point at angle a (0 = +X, -90 deg = front) and height z."""
        s = math.sqrt(max(0.05, 1 - ((z - 0.84) / 0.30) ** 2))
        x, y = 0.31 * bx * s * math.cos(a), 0.25 * by * s * math.sin(a)
        n = Vector((x / (0.31 * bx) ** 2, y / (0.25 * by) ** 2, (z - 0.84) / 0.09)).normalized()
        return Vector((x, y - 0.02 * (by - 1), z)) + n * lift, n

    FRONT = -math.pi / 2
    neck, ncol = lk.get("neck"), lk.get("neck_col", SLOT_GOLD)
    if neck in ("beads", "medallion", "bell", "binoculars"):
        strap = {"beads": ncol, "medallion": SLOT_GOLD, "bell": SLOT_RED, "binoculars": SLOT_DARK}[neck]
        n_beads = 18
        for k in range(n_beads):
            a = k / n_beads * math.tau
            front = max(0.0, -math.sin(a))
            p, _ = torso_pt(a, 1.07 - 0.10 * front ** 2, 0.015)
            mb.sphere(p, 0.026 if neck == "beads" else 0.016, strap, "Chest", segs=(6, 4))
        p, n = torso_pt(FRONT, 0.94, 0.02)
        if neck == "medallion":
            mb.patch(p, n, 0.06, 0.06, SLOT_GOLD, "Chest", thick=0.018)
            mb.patch(p + n * 0.015, n, 0.03, 0.03, SLOT_RED, "Chest", thick=0.008)
        elif neck == "bell":
            mb.cone(p + Vector((0, -0.02, 0.02)), p + Vector((0, -0.05, -0.08)), 0.03, 0.065, SLOT_GOLD, "Chest", segs=10)
            mb.sphere(p + Vector((0, -0.05, -0.09)), 0.02, SLOT_DARK, "Chest", segs=(6, 4))
        elif neck == "binoculars":
            for side in (1, -1):
                q = p + Vector((side * 0.045, -0.04, 0))
                mb.tube(q, 0.035, 0.035, 0.1, SLOT_DARK, "Chest", rot=Matrix.Rotation(math.radians(90), 3, "X"), segs=10)
                mb.ellipsoid(q + Vector((0, -0.052, 0)), (0.028, 0.006, 0.028), SLOT_TEAL, "Chest", segs=(8, 5))
    elif neck == "scarf":
        mb.ellipsoid((0, -0.01, 1.09), (0.21 * max(1, bx * 0.95), 0.2, 0.075), ncol, "Chest", segs=(16, 8))
        a, b = Vector((0.1, -0.17 * by, 1.07)), Vector((0.14, -0.23 * by, 0.84))
        mb.capsule(a, b, 0.05, ncol, "Chest", squash=0.45)
        mb.ring(a, b, 0.72, 0.052, 0.03, SLOT_TRIM, "Chest")
    elif neck == "bandana":
        mb.tube((0, -0.005, 1.085), 0.18, 0.175, 0.05, ncol, "Chest", caps=False, segs=16, taper=0.92)
        p, n = torso_pt(FRONT, 0.99, 0.012)
        patch_along(p, n, Vector((1, 0, 0)), 0.11, 0.08, ncol, "Chest", thick=0.018)
        mb.sphere(p + Vector((0, -0.01, 0.07)), 0.03, ncol, "Chest", segs=(8, 5))
    elif neck == "bowtie":
        k, _ = torso_pt(FRONT, 1.05, 0.03)
        mb.sphere(k, 0.028, ncol, "Chest", segs=(8, 5))
        for side in (1, -1):
            mb.cone(k, k + Vector((side * 0.1, 0.0, 0.0)), 0.014, 0.058, ncol, "Chest", segs=4)

    # --------------------------------------------------------------- arms & legs
    ar = lk.get("arms", 1.0)
    for side in (1, -1):
        s = ".L" if side == 1 else ".R"
        f = (lambda p: Vector(p)) if side == 1 else mx
        if lk.get("wristbands") is not None and not lk.get("gloves"):
            mb.ring(f(skel.elbow), f(skel.wrist), 0.8, 0.07 * ar * 1.22, 0.06, lk["wristbands"], "LowerArm" + s)
        if lk.get("kneepads") is not None:
            mb.ellipsoid(f(skel.knee) + Vector((0, -0.075 * lk.get("legs", 1.0), -0.03)), (0.085, 0.045, 0.075),
                         lk["kneepads"], "LowerLeg" + s, segs=(10, 6))
    tat = lk.get("tattoo")
    if tat == "bands":
        for t in (0.42, 0.56):
            mb.ring(skel.shoulder, skel.elbow, t, 0.075 * ar * 1.08, 0.016, SLOT_DARK, "UpperArm.L")
        mb.ring(skel.elbow, skel.wrist, 0.35, 0.07 * ar * 1.08, 0.016, SLOT_DARK, "LowerArm.L")
    elif tat == "heart":
        c = skel.shoulder.lerp(skel.elbow, 0.55)
        n = Vector((0.85, -0.5, 0.1)).normalized()
        r = 0.075 * ar * 0.95
        base = c + n * r
        up = Vector((0, 0, 1))
        side_v = n.cross(up).normalized()
        for k in (1, -1):
            mb.patch(base + side_v * k * 0.018 + up * 0.012, n, 0.024, 0.024, SLOT_RED, "UpperArm.L", thick=0.01)
        patch_along(base - up * 0.014, n, up, 0.03, 0.02, SLOT_RED, "UpperArm.L", thick=0.01)


# ---------------------------------------------------------------- body parts

def build_body(sp, skel):
    mb = MeshBuilder()
    hc = skel.head_c
    hr = skel.head_r
    lk = look(sp)
    ar, lr, es_ = lk.get("arms", 1.0), lk.get("legs", 1.0), lk.get("eyes", 1.0)
    bx, by = lk.get("belly", (1.0, 1.0)) if isinstance(lk.get("belly"), tuple) else (lk.get("belly", 1.0),) * 2

    # --- legs, feet, shorts
    for side in (1, -1):
        s = ".L" if side == 1 else ".R"
        f = (lambda p: Vector(p)) if side == 1 else mx
        mb.capsule(f(skel.hip) + Vector((0, 0, -0.04)), f(skel.knee), 0.105 * lr, SLOT_FUR, "UpperLeg" + s)
        mb.capsule(f(skel.knee), f(skel.ankle) + Vector((0, 0, 0.02)), 0.09 * lr, SLOT_FUR, "LowerLeg" + s)
        fs = feats(sp).get("foot_scale", 1.0)
        mb.ellipsoid(f(skel.ankle) + Vector((0, -0.07 * fs, -0.025)), (0.095, 0.14 * fs, 0.06), SLOT_ACCENT, "Foot" + s)
    mb.ellipsoid((0, 0, 0.60), (0.29 * bx, 0.23 * by, 0.15), SLOT_SHORTS, "Hips")

    # --- torso (jersey) + collar trim
    mb.ellipsoid((0, -0.02 * (by - 1), 0.84), (0.31 * bx, 0.25 * by, 0.30), SLOT_JERSEY, "Spine", segs=(16, 10))
    mb.cone((0, 0, 1.06), (0, 0, 1.10), 0.16, 0.13, SLOT_TRIM, "Chest", segs=14, caps=False)

    # --- arms: jersey sleeve cap, fur arm, paw
    for side in (1, -1):
        s = ".L" if side == 1 else ".R"
        f = (lambda p: Vector(p)) if side == 1 else mx
        sc = max(1.0, ar * 0.9)
        mb.ellipsoid(f(skel.shoulder) + Vector((0, 0, -0.03)), (0.11 * sc, 0.11 * sc, 0.10 * sc), SLOT_JERSEY, "UpperArm" + s, segs=(10, 7))
        mb.capsule(f(skel.shoulder), f(skel.elbow), 0.075 * ar, SLOT_FUR, "UpperArm" + s)
        mb.capsule(f(skel.elbow), f(skel.wrist), 0.07 * ar, SLOT_FUR, "LowerArm" + s)
        if lk.get("gloves"):  # boxing gloves replace the paws
            mb.ellipsoid(f(skel.wrist) + Vector((0, -0.03, -0.08)), (0.13, 0.14, 0.14), SLOT_RED, "Hand" + s)
            mb.ring(f(skel.elbow), f(skel.wrist), 0.92, 0.09, 0.06, SLOT_TRIM, "LowerArm" + s)
        else:
            mb.sphere(f(skel.wrist) + Vector((0, -0.01, -0.05)), 0.085 * max(ar, 0.85), SLOT_FUR, "Hand" + s)

    # --- neck
    if sp["neck"] > 0.05:
        mb.capsule(skel.neck_base, hc - Vector((0, 0, hr * 0.6)), 0.10 + 0.02 * sp["neck"], SLOT_FUR, "Neck")
    else:
        mb.cone(skel.neck_base - Vector((0, 0, 0.03)), skel.neck_top + Vector((0, 0, 0.05)), 0.12, 0.13, SLOT_FUR, "Neck", segs=12)

    # --- head
    mb.ellipsoid(hc, (hr * 1.05, hr * 0.95, hr), SLOT_FUR, "Head", segs=(18, 12))
    face_y = -hr * 0.88
    head = sp["head"]
    if head == "Muzzle":
        mb.ellipsoid(hc + Vector((0, -hr * 0.78, -hr * 0.28)), (0.14, 0.12, 0.10), SLOT_ACCENT, "Head")
        mb.ellipsoid(hc + Vector((0, -hr * 1.18, -hr * 0.16)), (0.05, 0.04, 0.035), SLOT_NOSE, "Head", segs=(10, 6))
    elif head == "LongMuzzle":
        mb.ellipsoid(hc + Vector((0, -hr * 0.85, -hr * 0.38)), (0.15, 0.21, 0.12), SLOT_ACCENT, "Head")
        mb.ellipsoid(hc + Vector((0, -hr * 1.48, -hr * 0.30)), (0.07, 0.035, 0.04), SLOT_NOSE, "Head", segs=(10, 6))
    elif head == "Beak":
        bs, bf = feats(sp).get("beak_scale", 1.0), feats(sp).get("beak_fat", 1.0)
        base = hc + Vector((0, -hr * 0.72, -hr * 0.15))
        tip = base + Vector((0, -hr * 0.83 * bs, -hr * 0.15 * bs))
        mb.cone(base, tip, 0.10 * bf, 0.012 * bf, SLOT_ACCENT, "Head", segs=10)
    else:  # Round
        mb.ellipsoid(hc + Vector((0, -hr * 0.86, -hr * 0.22)), (0.11, 0.06, 0.08), SLOT_ACCENT, "Head")
        ns = feats(sp).get("nose_scale", 1.0)
        mb.ellipsoid(hc + Vector((0, -hr * 1.0, -hr * 0.10)), (0.045 * ns, 0.03 * ns, 0.03 * ns), SLOT_NOSE, "Head", segs=(10, 6))

    # birds with a dark head get a pale face disc so the eyes read (penguin)
    if head == "Beak" and sum(sp["fur"]) < 0.9:
        mb.ellipsoid(hc + Vector((0, -hr * 0.42, -hr * 0.05)), (hr * 0.78, hr * 0.62, hr * 0.78), SLOT_TRIM, "Head", segs=(16, 10))

    # mask patches sit under the eyes
    if sp["markings"] == "MaskPatch":
        for side in (1, -1):
            mb.ellipsoid(hc + Vector((side * hr * 0.40, face_y * 0.92, hr * 0.12)), (0.10, 0.04, 0.085),
                         SLOT_MARK, "Head", segs=(10, 6))

    # big cute eyes
    for side in (1, -1):
        e = hc + Vector((side * hr * 0.38, face_y, hr * 0.18))
        mb.ellipsoid(e, (0.068 * es_, 0.035 * es_, 0.08 * es_), SLOT_EYE_WHITE, "Head", segs=(10, 7))
        mb.ellipsoid(e + Vector((side * 0.004, -0.028 * es_, 0.004)),
                     (0.042 * es_, 0.02 * es_, 0.055 * es_), SLOT_EYE_DARK, "Head", segs=(10, 6))
        mb.sphere(e + Vector((side * 0.012, -0.045 * es_, 0.03 * es_)), 0.012 * es_, SLOT_EYE_WHITE, "Head", segs=(6, 4))

    # --- ears
    ears = sp["ears"]
    es = feats(sp).get("ear_scale", 1.0)
    for side in (1, -1):
        s = ".L" if side == 1 else ".R"
        bone = "Ear" + s
        if ears == "Pointed":
            base = hc + Vector((side * hr * 0.55, 0.0, hr * 0.62))
            tip = base + Vector((side * 0.07, 0.01, 0.26))
            mb.cone(base, tip, 0.10, 0.01, SLOT_FUR, bone, segs=8)
            mb.cone(base + Vector((0, -0.035, 0.01)), tip + Vector((0, -0.02, -0.06)), 0.06, 0.008, SLOT_ACCENT, bone, segs=8)
        elif ears == "Round":
            c = hc + Vector((side * hr * 0.66, 0.02, hr * 0.74))
            mb.ellipsoid(c, (0.085, 0.04, 0.085), SLOT_FUR, bone, segs=(10, 7))
            mb.ellipsoid(c + Vector((0, -0.028, 0)), (0.05, 0.02, 0.05), SLOT_ACCENT, bone, segs=(8, 6))
        elif ears == "Tall":
            # ear_scale > 1: bigger ears that splay outward (fennec, jerboa)
            c = hc + Vector((side * hr * (0.35 + 0.5 * (es - 1)), 0.03, hr * (1.45 + 0.5 * (es - 1))))
            tilt = Matrix.Rotation(side * math.radians(55 * (es - 1)), 3, "Y")
            mb.ellipsoid(c, (0.065 * es * es, 0.035, 0.24 * es), SLOT_FUR, bone, rot=tilt, segs=(10, 7))
            mb.ellipsoid(c + Vector((0, -0.025, 0)), (0.035 * es * es, 0.02, 0.18 * es), SLOT_ACCENT, bone, rot=tilt, segs=(8, 6))
        elif ears == "Droopy":
            c = hc + Vector((side * hr * 1.02, 0.0, hr * 0.05))
            rot = Matrix.Rotation(side * math.radians(25), 3, "Y")
            mb.ellipsoid(c, (0.05, 0.08, 0.16), SLOT_FUR, bone, rot=rot, segs=(10, 7))

    # --- horns
    horns = sp["horns"]
    if any(feats(sp).get(k) for k in CUSTOM_HORNS):
        horns = "None"  # silhouette built in add_features
    for side in (1, -1):
        if horns == "Horns":
            base = hc + Vector((side * hr * 0.30, 0.02, hr * 0.85))
            tip = base + Vector((side * 0.03, 0.02, 0.18))
            mb.cone(base, tip, 0.035, 0.025, SLOT_HORN, "Head", segs=8)
            mb.sphere(tip, 0.04, SLOT_MARK, "Head", segs=(8, 6))
        elif horns == "Antlers":
            base = hc + Vector((side * hr * 0.45, 0.02, hr * 0.80))
            mid = base + Vector((side * 0.20, 0.04, 0.16))
            tip = mid + Vector((side * 0.16, 0.02, 0.10))
            mb.cone(base, mid, 0.035, 0.03, SLOT_HORN, "Head", segs=7)
            mb.cone(mid, tip, 0.03, 0.012, SLOT_HORN, "Head", segs=7)
            mb.ellipsoid(mid + Vector((side * 0.10, 0.0, 0.10)), (0.13, 0.035, 0.09), SLOT_HORN, "Head",
                         rot=Matrix.Rotation(side * math.radians(-30), 3, "Y"), segs=(10, 6))
            mb.cone(mid, mid + Vector((0, -0.08, 0.16)), 0.025, 0.01, SLOT_HORN, "Head", segs=6)
        elif horns == "Tusks":
            base = hc + Vector((side * hr * 0.35, -hr * 1.0, -hr * 0.45))
            mb.cone(base, base + Vector((side * 0.05, -0.05, 0.14)), 0.03, 0.008, SLOT_HORN, "Head", segs=7)

    # --- tail
    t = sp["tail"]
    if t > 0.05:
        bushy = 0.075 + (0.045 if t >= 1.2 else 0.0)
        mb.capsule(skel.tail0, skel.tail1, bushy * 0.8, SLOT_FUR, "Tail1")
        mb.capsule(skel.tail1, skel.tail2, bushy, SLOT_FUR if t < 1.2 else SLOT_FUR, "Tail2")
        if t >= 1.2:  # bushy tails get a pale tip
            mb.sphere(skel.tail2 + (skel.tail2 - skel.tail1).normalized() * 0.03, bushy * 0.85, SLOT_ACCENT, "Tail2")

    # --- body markings on limbs / neck / tail
    mk = sp["markings"]
    limbs = []
    for side in (1, -1):
        s = ".L" if side == 1 else ".R"
        f = (lambda p: Vector(p)) if side == 1 else mx
        limbs += [(f(skel.hip), f(skel.knee), 0.105 * lr, "UpperLeg" + s),
                  (f(skel.knee), f(skel.ankle), 0.09 * lr, "LowerLeg" + s),
                  (f(skel.elbow), f(skel.wrist), 0.07 * ar, "LowerArm" + s)]
    if sp["neck"] > 0.05:
        limbs.append((skel.neck_base, skel.head_c - Vector((0, 0, hr * 0.6)), 0.10 + 0.02 * sp["neck"], "Neck"))
    if mk == "Stripes":
        for a, b, r, bone in limbs:
            for tt in (0.3, 0.6):
                mb.ring(a, b, tt, r * 1.07, 0.035, SLOT_MARK, bone)
        for tt in (0.25, 0.55, 0.85):  # forehead stripes as thin bands over the crown
            mb.ellipsoid(hc + Vector((0, (tt - 0.55) * hr * 1.4, hr * 0.93)), (hr * 0.5, 0.025, 0.02), SLOT_MARK, "Head",
                         rot=Matrix.Rotation(math.radians(-10 + tt * 20), 3, "X"), segs=(8, 5))
    elif mk == "Spots":
        for i, (a, b, r, bone) in enumerate(limbs):
            for j, tt in enumerate((0.2, 0.5, 0.8)):
                mb.spot(a, b, tt, (i * 2.1 + j * 2.4), r, 0.045, SLOT_MARK, bone)
                mb.spot(a, b, tt + 0.12, (i * 2.1 + j * 2.4 + 3.1), r, 0.035, SLOT_MARK, bone)
        for ang, el in ((0.6, 0.5), (2.2, 0.3), (-0.5, 0.2), (3.6, 0.55)):
            n = Vector((math.cos(ang) * math.cos(el), math.sin(ang) * math.cos(el) + 0.3, math.sin(el))).normalized()
            if n.y < -0.4:
                continue  # keep the face clean
            rot = Vector((0, 0, 1)).rotation_difference(n).to_matrix()
            mb.ellipsoid(hc + n * hr * 0.97, (0.05, 0.04, 0.012), SLOT_MARK, "Head", rot=rot, segs=(8, 5))

    add_features(mb, sp, skel)
    add_look(mb, sp, skel)
    mb.reshape(("Head", "Ear.L", "Ear.R"), hc, skel.head_shape)
    return mb


# ---------------------------------------------------------------- animation

def sym(**bones):
    """Expand 'Arm=(x,y,z)' style dict to .L/.R with mirrored Y/Z rotations."""
    out = {}
    for k, v in bones.items():
        if k.endswith("_"):  # symmetric pair
            base = k[:-1]
            out[base + ".L"] = v
            out[base + ".R"] = (v[0], -v[1], -v[2])
        else:
            out[k.replace("_L", ".L").replace("_R", ".R")] = v
    return out


def mirror_pose(p):
    rot, loc = p
    out = {}
    for k, v in rot.items():
        out[mirror(k)] = (v[0], -v[1], -v[2]) if (k.endswith(".L") or k.endswith(".R")) else (v[0], -v[1], -v[2])
    return out, dict(loc)


def pose(loc=None, **bones):
    return sym(**bones), (loc or {})


# Each action: (frames, loop, [(frame, (rot_dict, loc_dict)), ...]).
# Rotations are XYZ euler degrees in each bone's local space. Every bone's local Z points
# "forward" (-Y) at rest, so +X always swings a bone's tip forward, and for left-side
# hanging limbs +Z swings the tip outward (away from the body). Careful with raised arms:
# Z is applied after X, so once an arm is lifted past ~120 deg (X) the spread flips —
# NEGATIVE Z spreads a raised left arm outward (set, block, cheer).
def actions():
    idle_a = pose(Spine=(3, 0, 0), UpperArm_=(4, 0, 8), LowerArm_=(18, 0, 0), Tail1=(15, 0, 12), Tail2=(10, 0, 8))
    idle_b = pose({"Hips": (0, -0.015, 0)}, Spine=(-1, 0, 0), Chest=(2, 0, 0), UpperArm_=(0, 0, 11), LowerArm_=(24, 0, 0),
                  Head=(-4, 0, 0), Ear_=(0, 0, 6), Tail1=(22, 0, -12), Tail2=(14, 0, -8))

    run_a = pose(Spine=(14, 0, 0), Head=(-10, 0, 0),
                 UpperLeg_L=(48, 0, 0), LowerLeg_L=(-25, 0, 0), UpperLeg_R=(-35, 0, 0), LowerLeg_R=(-75, 0, 0),
                 UpperArm_L=(-45, 0, 10), UpperArm_R=(45, 0, -10), LowerArm_=(70, 0, 0),
                 Tail1=(-10, 0, 10), Ear_=(-15, 0, 0))
    run_b = pose({"Hips": (0, 0.05, 0)}, Spine=(12, 0, 0), Head=(-8, 0, 0),
                 UpperLeg_L=(0, 0, 0), LowerLeg_L=(-10, 0, 0), UpperLeg_R=(15, 0, 0), LowerLeg_R=(-105, 0, 0),
                 UpperArm_=(0, 0, 10), LowerArm_=(75, 0, 0), Tail1=(-20, 0, 0), Ear_=(-25, 0, 0))

    jump_crouch = pose({"Hips": (0, -0.10, 0)}, Spine=(22, 0, 0), UpperLeg_=(55, 0, 0), LowerLeg_=(-85, 0, 0), Foot_=(20, 0, 0),
                       UpperArm_=(-40, 0, 12), LowerArm_=(20, 0, 0))
    jump_air = pose(Spine=(-6, 0, 0), UpperLeg_=(55, 0, 4), LowerLeg_=(-95, 0, 0), Foot_=(-10, 0, 0),
                    UpperArm_=(150, 0, -40), LowerArm_=(15, 0, 0), Tail1=(-30, 0, 0), Ear_=(-30, 0, 0), Head=(-12, 0, 0))

    spike_wind = pose(Spine=(-18, -12, 0), Chest=(-8, -18, 0), Head=(-18, 0, 0),
                      UpperArm_R=(160, 0, -55), LowerArm_R=(95, 0, 0), UpperArm_L=(125, 0, 40), LowerArm_L=(10, 0, 0),
                      UpperLeg_L=(50, 0, 0), LowerLeg_L=(-95, 0, 0), UpperLeg_R=(25, 0, 0), LowerLeg_R=(-70, 0, 0),
                      Tail1=(-35, 0, 0), Ear_=(-20, 0, 0))
    spike_hit = pose(Spine=(28, 10, 0), Chest=(12, 18, 0), Head=(8, 0, 0),
                     UpperArm_R=(70, 0, -8), LowerArm_R=(0, 0, 0), UpperArm_L=(10, 0, 18), LowerArm_L=(40, 0, 0),
                     UpperLeg_L=(40, 0, 0), LowerLeg_L=(-60, 0, 0), UpperLeg_R=(10, 0, 0), LowerLeg_R=(-40, 0, 0),
                     Tail1=(20, 0, 0), Ear_=(25, 0, 0))
    spike_follow = pose(Spine=(18, 4, 0), Chest=(6, 8, 0),
                        UpperArm_R=(15, 0, -12), LowerArm_R=(25, 0, 0), UpperArm_L=(0, 0, 14), LowerArm_L=(30, 0, 0),
                        UpperLeg_=(30, 0, 0), LowerLeg_=(-50, 0, 0))

    bump_ready = pose({"Hips": (0, -0.09, 0)}, Spine=(18, 0, 0), Head=(-14, 0, 0),
                      UpperLeg_=(40, 0, 6), LowerLeg_=(-62, 0, 0), Foot_=(12, 0, 0),
                      UpperArm_=(58, 0, -16), LowerArm_=(5, 0, 0), Hand_=(0, 0, -10))
    bump_hit = pose({"Hips": (0, -0.03, 0)}, Spine=(8, 0, 0), Head=(-10, 0, 0),
                    UpperLeg_=(18, 0, 4), LowerLeg_=(-28, 0, 0),
                    UpperArm_=(80, 0, -16), LowerArm_=(0, 0, 0), Hand_=(0, 0, -10))

    set_ready = pose({"Hips": (0, -0.05, 0)}, Spine=(-4, 0, 0), Head=(-22, 0, 0),
                     UpperLeg_=(22, 0, 0), LowerLeg_=(-38, 0, 0),
                     UpperArm_=(150, 0, -40), LowerArm_=(70, 0, 20), Hand_=(-30, 0, 0))
    set_push = pose(Spine=(-8, 0, 0), Head=(-25, 0, 0), UpperLeg_=(4, 0, 0), LowerLeg_=(-8, 0, 0), Foot_=(-15, 0, 0),
                    UpperArm_=(168, 0, -30), LowerArm_=(15, 0, 10), Hand_=(-20, 0, 0))

    block = pose(Spine=(-4, 0, 0), Head=(-10, 0, 0), UpperLeg_=(14, 0, 3), LowerLeg_=(-35, 0, 0), Foot_=(-20, 0, 0),
                 UpperArm_=(162, 0, -48), LowerArm_=(-5, 0, 0), Hand_=(-10, 0, 0), Ear_=(-15, 0, 0))

    dive_load = pose({"Hips": (0, -0.12, 0)}, Hips=(20, 0, 0), Spine=(25, 0, 0), UpperLeg_=(55, 0, 0), LowerLeg_=(-85, 0, 0),
                     UpperArm_=(70, 0, 10), LowerArm_=(10, 0, 0))
    dive_fly = pose({"Hips": (0, -0.25, 0)}, Hips=(78, 0, 0), Spine=(5, 0, 0), Head=(-55, 0, 0), Neck=(-15, 0, 0),
                    UpperLeg_=(-10, 0, 6), LowerLeg_=(-20, 0, 0), Foot_=(-30, 0, 0),
                    UpperArm_=(165, 0, -15), LowerArm_=(0, 0, 0), Tail1=(40, 0, 0), Ear_=(40, 0, 0))
    dive_land = pose({"Hips": (0, -0.40, 0)}, Hips=(88, 0, 0), Spine=(0, 0, 0), Head=(-60, 0, 0), Neck=(-20, 0, 0),
                     UpperLeg_=(-5, 0, 8), LowerLeg_=(-35, 0, 0), Foot_=(-30, 0, 0),
                     UpperArm_=(160, 0, -25), LowerArm_=(10, 0, 0), Tail1=(25, 0, 0))

    cheer_a = pose(UpperArm_L=(170, 0, -50), UpperArm_R=(140, 0, 30), LowerArm_=(10, 0, 0), Head=(-12, 0, 5),
                   Tail1=(20, 0, 25), Ear_=(0, 0, 10))
    cheer_b = pose({"Hips": (0, 0.08, 0)}, UpperArm_L=(140, 0, -30), UpperArm_R=(170, 0, 50), LowerArm_=(10, 0, 0),
                   Head=(-12, 0, -5), UpperLeg_=(12, 0, 0), LowerLeg_=(-30, 0, 0), Tail1=(20, 0, -25), Ear_=(0, 0, -10))

    # bowled over by a diving player: hit, fall on your back, dizzy sprawl, sit up, stand
    knock_hit = pose({"Hips": (0, -0.05, 0)}, Spine=(-25, 0, 0), Head=(-20, 0, 0),
                     UpperArm_=(140, 0, -45), LowerArm_=(20, 0, 0), Tail1=(30, 0, 0), Ear_=(-30, 0, 0))
    knock_fall = pose({"Hips": (0, -0.25, 0)}, Hips=(-55, 0, 0), Spine=(-10, 0, 0), Head=(-10, 0, 0),
                      UpperArm_=(125, 0, -70), LowerArm_=(15, 0, 0), UpperLeg_=(45, 0, 10), LowerLeg_=(-20, 0, 0),
                      Ear_=(-35, 0, 0))
    knock_flat = pose({"Hips": (0, -0.42, 0)}, Hips=(-82, 0, 0), Head=(15, 0, 0),
                      UpperArm_=(100, 0, -80), LowerArm_=(30, 0, 0), UpperLeg_=(35, 0, 15), LowerLeg_=(-25, 0, 0),
                      Foot_=(-20, 0, 0), Tail1=(-20, 0, 0), Ear_=(20, 0, 0))
    knock_dizzy = pose({"Hips": (0, -0.42, 0)}, Hips=(-82, 0, 0), Head=(25, 0, 14),
                       UpperArm_=(95, 0, -85), LowerArm_=(40, 0, 0), UpperLeg_=(30, 0, 18), LowerLeg_=(-35, 0, 0),
                       Foot_=(-20, 0, 0), Tail1=(-10, 0, 10), Ear_=(30, 0, -10))
    knock_sit = pose({"Hips": (0, -0.32, 0)}, Hips=(-35, 0, 0), Spine=(30, 0, 0), Head=(5, 0, -8),
                     UpperArm_=(20, 0, 30), LowerArm_=(30, 0, 0), UpperLeg_=(85, 0, 10), LowerLeg_=(-70, 0, 0))

    return {
        "Knockdown": (34, False, [(0, knock_hit), (4, knock_fall), (8, knock_flat), (18, knock_dizzy),
                                  (26, knock_sit), (34, idle_a)]),
        "Idle": (40, True, [(0, idle_a), (20, idle_b), (40, idle_a)]),
        "Run": (16, True, [(0, run_a), (4, run_b), (8, mirror_pose(run_a)), (12, mirror_pose(run_b)), (16, run_a)]),
        "Jump": (12, False, [(0, jump_crouch), (6, jump_air), (12, jump_air)]),
        "Spike": (18, False, [(0, spike_wind), (6, spike_wind), (9, spike_hit), (18, spike_follow)]),
        "Bump": (14, False, [(0, bump_ready), (6, bump_hit), (14, bump_ready)]),
        "Set": (14, False, [(0, set_ready), (6, set_push), (14, set_push)]),
        "Block": (10, False, [(0, jump_air), (5, block), (10, block)]),
        "Dive": (20, False, [(0, dive_load), (6, dive_fly), (14, dive_land), (20, dive_land)]),
        "Cheer": (24, True, [(0, cheer_a), (12, cheer_b), (24, cheer_a)]),
    }


def reset_pose(arm_obj):
    for pb in arm_obj.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)


def apply_pose(arm_obj, p, scale):
    rot, loc = p
    reset_pose(arm_obj)
    for bname, (x, y, z) in rot.items():
        pb = arm_obj.pose.bones.get(bname)
        if pb:
            pb.rotation_euler = (math.radians(x), math.radians(y), math.radians(z))
    for bname, v in loc.items():
        pb = arm_obj.pose.bones.get(bname)
        if pb:
            pb.location = Vector(v) * scale


def bake_actions(arm_obj, scale):
    arm_obj.animation_data_create()
    made = []
    for name, (length, loop, keys) in actions().items():
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        arm_obj.animation_data.action = act
        for frame, p in keys:
            apply_pose(arm_obj, p, scale)
            for pb in arm_obj.pose.bones:
                pb.keyframe_insert("rotation_euler", frame=frame)
                pb.keyframe_insert("location", frame=frame)
        act.frame_range = (0, length)
        made.append(act)
    arm_obj.animation_data.action = None
    reset_pose(arm_obj)
    return made


# ---------------------------------------------------------------- assembly

def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions, bpy.data.materials,
                 bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for d in list(coll):
            coll.remove(d)


def load_species():
    with open(SPECIES_JSON, encoding="utf-8") as f:
        return {s["id"]: s for s in json.load(f)["species"]}


def build_animal(sp, offset=(0, 0, 0)):
    """Build the rigged animal at `offset`, fitted to its height stat. Returns (armature, mesh, scale)."""
    skel = Skeleton(sp)
    mb = build_body(sp, skel)
    arm = build_armature(sp["id"], skel)
    mesh = mb.to_object(sp["id"], arm)
    mesh.name = sp["id"] + "_Body"

    # fit to target height: top of the head (ears/horns excluded) -> BASE_HEIGHT * height
    natural = skel.head_c.z + skel.head_r * skel.head_shape.z
    scale = BASE_HEIGHT * sp["height"] / natural
    arm.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
    for o in (arm, mesh):
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    arm.location = offset
    return arm, mesh, scale


def export_species(sp):
    clear_scene()
    arm, mesh, scale = build_animal(sp)
    bake_actions(arm, 1.0)  # rig already has the scale applied
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, f"animal_{sp['id']}{SUFFIX}.fbx")
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=False, mesh_smooth_type="FACE",
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=1.0, bake_anim_step=1.0)
    print(f"[animal_gen] wrote {path} ({len(mesh.data.vertices)} verts)")


# ---------------------------------------------------------------- preview render

def srgb_to_linear(c):
    return [((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in c]


def palette_colors(sp, jersey=JERSEY_PREVIEW):
    """Mirror of AnimalPalette.Build on the Unity side."""
    pal = [[1, 0, 1]] * SLOTS
    pal[SLOT_FUR] = sp["fur"]
    pal[SLOT_ACCENT] = sp["accent"]
    pal[SLOT_JERSEY] = jersey
    pal[SLOT_MARK] = sp["marking"]
    pal[SLOT_NOSE] = sp["nose"]
    pal[SLOT_EYE_WHITE] = [0.97, 0.97, 0.97]
    pal[SLOT_EYE_DARK] = [0.06, 0.05, 0.06]
    pal[SLOT_SHORTS] = [c * 0.35 + 0.05 for c in jersey]
    pal[SLOT_HORN] = [0.62, 0.50, 0.36] if sp["horns"] == "Antlers" else [0.92, 0.87, 0.74]
    pal[SLOT_TRIM] = [0.97, 0.97, 0.95]
    for k, c in WARDROBE.items():
        pal[k] = c
    return pal


def palette_material(sp):
    img = bpy.data.images.new(sp["id"] + "_pal", width=SLOTS, height=1)
    px = []
    for c in palette_colors(sp):
        px += list(c) + [1.0]  # image is sRGB-tagged; keep authored values
    img.pixels = px
    mat = bpy.data.materials.new(sp["id"] + "_mat")
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def preview(out_path, specs):
    clear_scene()
    species = load_species()
    yaw = 0.0
    if specs and specs[0].startswith("@"):
        yaw = math.radians(float(specs[0][1:]))
        specs = specs[1:]
    spin = Matrix.Rotation(yaw, 3, "Z")
    n = len(specs)
    spacing = 1.6
    for i, spec in enumerate(specs):
        sid, action, frame = (spec.split(":") + ["Idle", "0"])[:3]
        sp = species[sid]
        x = (i - (n - 1) / 2) * spacing
        arm, mesh, scale = build_animal(sp, offset=spin @ Vector((x, 0, 0)))
        mesh.data.materials.append(palette_material(sp))
        length, loop, keys = actions()[action]
        frame = int(frame)
        best = min(keys, key=lambda k: abs(k[0] - frame))
        apply_pose(arm, best[1], 1.0)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.show_object_outline = True
    scene.render.resolution_x = 360 * n
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = int(os.environ.get("VB_PREVIEW_PCT", "100"))
    band = os.environ.get("VB_PREVIEW_BAND")  # "ymin,ymax" (0..1, from the bottom): crop to a strip
    if band:
        lo, hi = (float(v) for v in band.split(","))
        scene.render.use_border = scene.render.use_crop_to_border = True
        scene.render.border_min_x, scene.render.border_max_x = 0.0, 1.0
        scene.render.border_min_y, scene.render.border_max_y = lo, hi
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new("World")

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = max(spacing * n, 3.4)
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = spin @ Vector((3.5, -9.0, 2.6))
    direction = Vector((0, 0, 1.25)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)
    print(f"[animal_gen] preview -> {out_path}")


# ---------------------------------------------------------------- main

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print(__doc__)
        return
    global DETAIL, SUFFIX
    mode, rest = argv[0], argv[1:]
    while rest and rest[0].startswith("--"):
        if rest[0] == "--detail":
            DETAIL = float(rest[1])
        elif rest[0] == "--suffix":
            SUFFIX = rest[1]
        rest = rest[2:]
    if mode == "export":
        species = load_species()
        ids = list(species) if rest == ["all"] else rest
        for sid in ids:
            export_species(species[sid])
    elif mode == "preview":
        preview(os.path.abspath(rest[0]), rest[1:])
    else:
        print(__doc__)


main()
