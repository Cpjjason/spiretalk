"""Build the SpireTalk release zip and its manifest.

    python -X utf8 release/make_release.py <BaseLib folder>

Run after `dotnet publish -c Release -p:ModsPath=<dir>/` has put SpireTalk.dll/.json/.pck,
Jint.dll and Acornima.dll into <dir>/SpireTalk; pass that <dir> as --mods (default:
release/out/mods). The BaseLib folder holds BaseLib.dll/.json/.pck and its LICENSE.

Writes release/out/SpireTalk-<ver>.zip (mods/SpireTalk, mods/BaseLib, 点我安装.bat, install.ps1,
安装说明.txt) and release/out/manifest.json, both uploaded to the GitHub release. The mod's
one-click update and feedback.yumina.io/v1/spiretalk/release read the manifest: version, notes
(release/notes_v<ver>.txt), and the zip's size and sha256.
"""
import argparse
import hashlib
import json
import os
import re
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)

ap = argparse.ArgumentParser()
ap.add_argument("baselib")
ap.add_argument("--mods", default=os.path.join(HERE, "out", "mods"))
args = ap.parse_args()

ver = re.search(r'Version = "([\d.]+)"', open(os.path.join(REPO, "SpireTalkCode", "MainFile.cs"), encoding="utf-8").read()).group(1)
manifest_ver = json.load(open(os.path.join(REPO, "SpireTalk.json"), encoding="utf-8"))["version"]
assert ver == manifest_ver, f"MainFile.Version {ver} != SpireTalk.json {manifest_ver}"

ours = os.path.join(args.mods, "SpireTalk")
need = ["SpireTalk.dll", "SpireTalk.json", "SpireTalk.pck", "Jint.dll", "Acornima.dll"]
missing = [f for f in need if not os.path.exists(os.path.join(ours, f))]
assert not missing, f"missing in {ours}: {missing}"
base_files = ["BaseLib.dll", "BaseLib.json", "BaseLib.pck", "LICENSE"]
missing = [f for f in base_files if not os.path.exists(os.path.join(args.baselib, f))]
assert not missing, f"missing in {args.baselib}: {missing}"

out_dir = os.path.join(HERE, "out")
os.makedirs(out_dir, exist_ok=True)
name = f"SpireTalk-{ver}.zip"
out = os.path.join(out_dir, name)

with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for f in need:
        z.write(os.path.join(ours, f), f"mods/SpireTalk/{f}")
    for f in base_files:
        z.write(os.path.join(args.baselib, f), f"mods/BaseLib/{f}")
    z.write(os.path.join(HERE, "点我安装.bat"), "点我安装.bat")
    z.write(os.path.join(HERE, "install.ps1"), "install.ps1")
    readme = open(os.path.join(HERE, "安装说明.txt"), encoding="utf-8").read().replace("{ver}", ver)
    z.writestr("安装说明 INSTALL.txt", "﻿" + readme.replace("\n", "\r\n"))

with zipfile.ZipFile(out) as z:
    for i in z.infolist():
        print(f"{i.file_size:>10}  {i.filename}")


def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


notes = os.path.join(HERE, f"notes_v{ver}.txt")
manifest = {
    "version": ver,
    "notes": open(notes, encoding="utf-8").read().strip() if os.path.exists(notes) else "",
    "assets": [{"kind": "main", "name": name, "tag": f"v{ver}", "size": os.path.getsize(out), "sha256": sha(out)}],
}
with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as f:
    json.dump(manifest, f, ensure_ascii=False, indent=1)
print(json.dumps(manifest, ensure_ascii=False, indent=1))
