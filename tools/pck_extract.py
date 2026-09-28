"""Extract files from a Godot 4 .pck by path prefix (the game's pck stores localization JSON uncompressed).

    python tools/pck_extract.py <SlayTheSpire2.pck> <out dir> [prefix ...]
    e.g. python tools/pck_extract.py ".../SlayTheSpire2.pck" vanilla_loc localization/

Used to pull the game's own translations (NPC names, keywords) as a glossary for the mod's locales.
"""
import os, struct, sys


def entries(f):
    magic = f.read(4)
    if magic != b"GDPC":
        raise SystemExit("not a Godot pck")
    version, _maj, _min, _pat = struct.unpack("<4I", f.read(16))
    flags = 0
    file_base = 0
    if version >= 2:
        flags, file_base = struct.unpack("<IQ", f.read(12))
    if version >= 3:
        dir_offset = struct.unpack("<Q", f.read(8))[0]
        f.seek(dir_offset)
    else:
        f.read(16 * 4)
    if flags & 1:
        raise SystemExit("encrypted directory")
    count = struct.unpack("<I", f.read(4))[0]
    for _ in range(count):
        n = struct.unpack("<I", f.read(4))[0]
        path = f.read(n).rstrip(b"\0").decode("utf-8")
        offset, size = struct.unpack("<QQ", f.read(16))
        f.read(16)  # md5
        fflags = struct.unpack("<I", f.read(4))[0] if version >= 2 else 0
        yield path, file_base + offset, size, fflags


def main():
    pck, out, *prefixes = sys.argv[1:]
    prefixes = [p.removeprefix("res://") for p in prefixes] or [""]
    n = 0
    with open(pck, "rb") as f:
        listing = list(entries(f))
        for path, off, size, fflags in listing:
            if not any(path.removeprefix("res://").startswith(p) for p in prefixes):
                continue
            if fflags & 1:
                print("skip encrypted", path)
                continue
            f.seek(off)
            data = f.read(size)
            dest = os.path.join(out, path.removeprefix("res://"))
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            open(dest, "wb").write(data)
            n += 1
    print(f"{n} file(s) of {len(listing)} → {out}")


if __name__ == "__main__":
    main()
