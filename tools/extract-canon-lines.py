#!/usr/bin/env python3
"""Pull every line the eight Ancients and the Merchant actually say in Slay the Spire 2, in
English and in the official Simplified Chinese translation, into SpireTalk/canon_lines.json.
build-cards.mjs writes them into each NPC's persona as examples of their real voice.

The input is a folder of the game's localization tables exported as <lang>_<table>.json
(Godot headless + list_loc.gd against SlayTheSpire2.pck):

    python tools/extract-canon-lines.py <loc-dir>
"""
import json
import re
import sys
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ANCIENTS = ["NEOW", "OROBAS", "NONUPEIPE", "PAEL", "DARV", "TANX", "TEZCATARA", "VAKUU"]
CLIMBERS = ["IRONCLAD", "SILENT", "DEFECT", "NECROBINDER", "REGENT"]
# Situations the Merchant speaks in, in the order the card lists them.
MERCHANT_SITUATIONS = OrderedDict([
    ("purchaseSuccess", "after a purchase"),
    ("purchaseFailureGold", "when they cannot afford something"),
    ("purchaseFailureSpace", "when their potion belt is full"),
    ("purchaseFailureForbidden", "when a purchase is not allowed"),
    ("foulPotion", "when a climber throws a Foul Potion at you (you love it)"),
    ("playerDead", "when a dead climber tries to shop"),
])

TAG = re.compile(r"\[/?[a-z_]+(?:[= ][^\]]*)?\]")


def clean(text: str) -> str:
    text = TAG.sub("", text).replace("PLACEHOLDER: ", "")
    text = text.replace("{Visits:plural:time|times}", "times").replace("{Visits}", "N")
    return " / ".join(part.strip() for part in text.split("\n") if part.strip())


def load(loc: Path, lang: str, table: str) -> dict:
    return json.loads((loc / f"{lang}_{table}.json").read_text(encoding="utf-8"))


def ancient_scenes(npc: str, en: dict, zh: dict) -> list:
    # Keys look like NEOW.talk.IRONCLAD.2-1.ancient: <addressee>.<scene>-<step>[r].<speaker>.
    pattern = re.compile(rf"^{npc}\.talk\.([A-Za-z]+)\.(\d+)-(\d+)(r?)\.(ancient|char)$")
    scenes = OrderedDict()
    for key in en:
        m = pattern.match(key)
        if not m:
            continue
        to, scene, step, repeat, speaker = m.groups()
        sid = (to, int(scene))
        scenes.setdefault(sid, {"to": to, "repeats": bool(repeat), "lines": []})
        who = "you" if speaker == "ancient" else "climber"
        # "Vakuu howls with laughter." is stage narration written on the Ancient's line.
        # So is "The Ironclad fights Tanx." written on the climber's line.
        if who == "you" and clean(en[key]).startswith(npc.title()):
            who = "narration"
        elif who == "climber" and clean(en[key]).startswith(("The " + to.title(), "The Necrobinder and")):
            who = "narration"
        scenes[sid]["lines"].append((int(step), {
            "who": who,
            "en": clean(en[key]),
            "zh": clean(zh.get(key, "")),
        }))
    order = ["firstVisitEver", "ANY", *CLIMBERS]
    out = []
    for (to, scene), data in sorted(scenes.items(), key=lambda kv: (order.index(kv[0][0]) if kv[0][0] in order else 99, kv[0][1])):
        data["lines"] = [line for _, line in sorted(data["lines"], key=lambda t: t[0])]
        out.append(data)
    return out


def merchant_scenes(en: dict, zh: dict, ftue_en: dict, ftue_zh: dict) -> list:
    out = []
    greeting = "MERCHANT_FTUE_DESCRIPTION"
    if greeting in ftue_en:
        quote = re.compile(r"“([^”]+)”")
        en_q, zh_q = quote.search(clean(ftue_en[greeting])), quote.search(clean(ftue_zh.get(greeting, "")))
        if en_q:
            out.append({"to": "greeting", "lines": [{"who": "you", "en": en_q.group(1), "zh": zh_q.group(1) if zh_q else ""}]})
    for situation, label in MERCHANT_SITUATIONS.items():
        keys = sorted(k for k in en if k.startswith(f"MERCHANT.talk.{situation}."))
        if keys:
            out.append({"to": situation, "label": label,
                        "lines": [{"who": "you", "en": clean(en[k]), "zh": clean(zh.get(k, ""))} for k in keys]})
    return out


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    loc = Path(sys.argv[1])
    en, zh = load(loc, "eng", "ancients"), load(loc, "zhs", "ancients")
    result = OrderedDict()
    for npc in ANCIENTS:
        result[npc] = ancient_scenes(npc, en, zh)
    result["MERCHANT"] = merchant_scenes(load(loc, "eng", "merchant_room"), load(loc, "zhs", "merchant_room"),
                                         load(loc, "eng", "ftues"), load(loc, "zhs", "ftues"))
    target = ROOT / "SpireTalk" / "canon_lines.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    for npc, scenes in result.items():
        print(f"{npc:10} {sum(len(s['lines']) for s in scenes):3} lines in {len(scenes)} scenes")


if __name__ == "__main__":
    main()
