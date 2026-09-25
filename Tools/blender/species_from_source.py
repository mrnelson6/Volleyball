"""Fallback species.json exporter that parses CharacterDef.cs / SpeciesArt.cs directly.

The canonical exporter is the Unity menu item Volleyball/3D/Export Species Specs for Blender
(Assets/Editor/Art3D/SpeciesSpecExporter.cs). This script produces the same JSON without
launching Unity, for quick iteration on the Blender generator.

    python Tools/blender/species_from_source.py
"""
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Scripts", "Player", "CharacterDef.cs")
OUT = os.path.join(os.path.dirname(__file__), "species.json")

DEFAULTS = dict(head="Muzzle", ears="Pointed", horns="None", neck=0.0, tail=1.0,
                markings="None", marking=[0.15, 0.12, 0.10], nose=[0.10, 0.08, 0.08])


def color(expr):
    nums = re.findall(r"[-\d.]+", expr)
    return [round(float(n.rstrip("f")), 3) for n in nums[:3]]


def main():
    text = open(SRC, encoding="utf-8").read()
    blocks = re.split(r"new CharacterDef\s*\{", text)[1:]
    out = []
    for b in blocks:
        m = re.search(r'id\s*=\s*"([^"]+)"', b)
        if not m:
            continue
        s = dict(DEFAULTS, id=m.group(1))
        s["height"] = float(re.search(r"height\s*=\s*([\d.]+)f", b).group(1))
        s["fur"] = color(re.search(r"\bfur\s*=\s*new Color\(([^)]*)\)", b).group(1))
        s["accent"] = color(re.search(r"furAccent\s*=\s*new Color\(([^)]*)\)", b).group(1))
        art = re.search(r"art\s*=\s*new SpeciesArt\s*\{(.*?)\}", b, re.S)
        if art:
            a = art.group(1)
            for key, enum in (("head", "HeadShape"), ("ears", "EarStyle"),
                              ("horns", "HornStyle"), ("markings", "MarkingStyle")):
                mm = re.search(rf"\b{key}\s*=\s*{enum}\.(\w+)", a)
                if mm:
                    s[key] = mm.group(1)
            for key in ("neck", "tail"):
                mm = re.search(rf"\b{key}\s*=\s*([\d.]+)f", a)
                if mm:
                    s[key] = float(mm.group(1))
            for key, field in (("marking", "markingColor"), ("nose", "noseColor")):
                mm = re.search(rf"{field}\s*=\s*new Color\(([^)]*)\)", a)
                if mm:
                    s[key] = color(mm.group(1))
        out.append(s)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump({"species": out}, f, indent=1)
    print(f"wrote {len(out)} species to {OUT}")


if __name__ == "__main__":
    main()
