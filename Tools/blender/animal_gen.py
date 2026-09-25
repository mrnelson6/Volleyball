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
        self.head_r = 0.28 if head_kind == "Beak" else 0.30
        self.neck_base = Vector((0, 0, 1.08))
        self.neck_top = Vector((0, 0, 1.08 + 0.06 + neck * 0.55))
        self.head_c = self.neck_top + Vector((0, 0, self.head_r * 0.85))
        self.shoulder = Vector((0.29, 0, 1.00))
        self.elbow = Vector((0.37, 0.0, 0.78))
        self.wrist = Vector((0.40, -0.02, 0.56))
        self.hip = Vector((self.leg_x, 0, self.hip_z))
        self.knee = Vector((self.leg_x, -0.01, 0.31))
        self.ankle = Vector((self.leg_x, 0.0, 0.08))
        self.toe = Vector((self.leg_x, -0.17, 0.05))
        tl = max(sp["tail"], 0.35)
        self.tail0 = Vector((0, 0.20, 0.60))
        self.tail1 = self.tail0 + Vector((0, 0.16, 0.10)) * tl
        self.tail2 = self.tail1 + Vector((0, 0.12, 0.20)) * tl

    def bones(self):
        """(name, head, tail, parent, roll-axis) — roll axis is where the bone's local Z points."""
        hc, nt = self.head_c, self.neck_top
        b = [
            ("Hips", Vector((0, 0, self.hip_z)), Vector((0, 0, self.hip_z + 0.14)), None, FWD),
            ("Spine", Vector((0, 0, self.hip_z + 0.14)), Vector((0, 0, 0.90)), "Hips", FWD),
            ("Chest", Vector((0, 0, 0.90)), self.neck_base, "Spine", FWD),
            ("Neck", self.neck_base, nt, "Chest", FWD),
            ("Head", nt, hc + Vector((0, 0, self.head_r)), "Neck", FWD),
            ("Tail1", self.tail0, self.tail1, "Hips", UP),
            ("Tail2", self.tail1, self.tail2, "Tail1", UP),
        ]
        for side in (1, -1):
            s = ".L" if side == 1 else ".R"
            f = (lambda p: Vector(p)) if side == 1 else mx
            b += [
                ("Ear" + s, hc + f((0.17, 0.0, self.head_r * 0.6)), hc + f((0.22, 0.0, self.head_r * 1.3)), "Head", FWD),
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


# ---------------------------------------------------------------- body parts

def build_body(sp, skel):
    mb = MeshBuilder()
    hc = skel.head_c
    hr = skel.head_r

    # --- legs, feet, shorts
    for side in (1, -1):
        s = ".L" if side == 1 else ".R"
        f = (lambda p: Vector(p)) if side == 1 else mx
        mb.capsule(f(skel.hip) + Vector((0, 0, -0.04)), f(skel.knee), 0.105, SLOT_FUR, "UpperLeg" + s)
        mb.capsule(f(skel.knee), f(skel.ankle) + Vector((0, 0, 0.02)), 0.09, SLOT_FUR, "LowerLeg" + s)
        fs = feats(sp).get("foot_scale", 1.0)
        mb.ellipsoid(f(skel.ankle) + Vector((0, -0.07 * fs, -0.025)), (0.095, 0.14 * fs, 0.06), SLOT_ACCENT, "Foot" + s)
    mb.ellipsoid((0, 0, 0.60), (0.29, 0.23, 0.15), SLOT_SHORTS, "Hips")

    # --- torso (jersey) + collar trim
    mb.ellipsoid((0, 0, 0.84), (0.31, 0.25, 0.30), SLOT_JERSEY, "Spine", segs=(16, 10))
    mb.cone((0, 0, 1.06), (0, 0, 1.10), 0.16, 0.13, SLOT_TRIM, "Chest", segs=14, caps=False)

    # --- arms: jersey sleeve cap, fur arm, paw
    for side in (1, -1):
        s = ".L" if side == 1 else ".R"
        f = (lambda p: Vector(p)) if side == 1 else mx
        mb.ellipsoid(f(skel.shoulder) + Vector((0, 0, -0.03)), (0.11, 0.11, 0.10), SLOT_JERSEY, "UpperArm" + s, segs=(10, 7))
        mb.capsule(f(skel.shoulder), f(skel.elbow), 0.075, SLOT_FUR, "UpperArm" + s)
        mb.capsule(f(skel.elbow), f(skel.wrist), 0.07, SLOT_FUR, "LowerArm" + s)
        mb.sphere(f(skel.wrist) + Vector((0, -0.01, -0.05)), 0.085, SLOT_FUR, "Hand" + s)

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
        mb.ellipsoid(e, (0.068, 0.035, 0.08), SLOT_EYE_WHITE, "Head", segs=(10, 7))
        mb.ellipsoid(e + Vector((side * 0.004, -0.028, 0.004)), (0.042, 0.02, 0.055), SLOT_EYE_DARK, "Head", segs=(10, 6))
        mb.sphere(e + Vector((side * 0.012, -0.045, 0.03)), 0.012, SLOT_EYE_WHITE, "Head", segs=(6, 4))

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
        limbs += [(f(skel.hip), f(skel.knee), 0.105, "UpperLeg" + s),
                  (f(skel.knee), f(skel.ankle), 0.09, "LowerLeg" + s),
                  (f(skel.elbow), f(skel.wrist), 0.07, "LowerArm" + s)]
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

    return {
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
    natural = skel.head_c.z + skel.head_r
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
