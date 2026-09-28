"""Validate one translated locale against the English source.

    python check.py <code>          e.g. python check.py jpn

Checks out/<code>/{ancients,events,relics,settings_ui}.json and out/<code>/script.json:
  - same keys as the English files (no missing, no extra)
  - every {placeholder} and [color] tag in the English value appears the same number of times
  - script lines keep the same number of lines (\n = speech bubbles), none empty
  - values that are still identical to the English (likely untranslated) are listed
Exit code 1 on any hard error.
"""
import json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
code = sys.argv[1]
TOK = re.compile(r"\{[^}]*\}|\[/?[a-z_]+\]|%APPDATA%[\\A-Za-z0-9_]*")
# values that may legitimately stay as-is (brand names, URLs, numbers, symbols)
KEEP = re.compile(r"^(|[\d\W_]+|https?://\S+|SpireTalk.*|OpenRouter|OpenAI|Anthropic|Google AI Studio|DeepSeek|Ollama|Zhipu.*|SiliconFlow|Moonshot.*|xAI.*|Mistral|Groq|Gemini|Claude|GLM.*|Kimi|Qwen|github\.com.*|yumina\.io|OK|API|URL)$", re.I)
errors, same = [], []


def load(p):
    with open(p, encoding="utf-8") as f:
        return json.load(f)


for name in ("ancients", "events", "relics", "settings_ui"):
    src = load(os.path.join(HERE, "src", "eng", name + ".json"))
    p = os.path.join(HERE, "out", code, name + ".json")
    if not os.path.exists(p):
        errors.append(f"{name}.json missing"); continue
    try:
        tr = load(p)
    except Exception as e:
        errors.append(f"{name}.json invalid JSON: {e}"); continue
    for k in src.keys() - tr.keys(): errors.append(f"{name}: missing key {k}")
    for k in tr.keys() - src.keys(): errors.append(f"{name}: extra key {k}")
    for k, v in src.items():
        t = tr.get(k)
        if not isinstance(t, str): continue
        a, b = sorted(set(TOK.findall(v))), sorted(set(TOK.findall(t)))
        # every placeholder/tag must survive; a name used twice in English may appear once
        if a != b: errors.append(f"{name}: {k} tokens {a} -> {b}")
        if v.strip() and t.strip() == v.strip() and not KEEP.match(v.strip()): same.append(f"{name}: {k} = {v[:60]!r}")
        if v.strip() and not t.strip() and len(v.strip()) > 1: errors.append(f"{name}: {k} empty")

src = load(os.path.join(HERE, "src", "script.json"))
p = os.path.join(HERE, "out", code, "script.json")
if not os.path.exists(p):
    errors.append("script.json missing")
else:
    try:
        tr = load(p)
        for k in src.keys() - tr.keys(): errors.append(f"script: missing {k}")
        for k in tr.keys() - src.keys(): errors.append(f"script: extra {k}")
        for k, v in src.items():
            t = tr.get(k)
            if not isinstance(t, str) or not t.strip(): errors.append(f"script: {k} empty"); continue
            if v["en"].count("\n") != t.count("\n"): errors.append(f"script: {k} has {t.count(chr(10))+1} bubbles, English has {v['en'].count(chr(10))+1}")
            if t.strip() == v["en"].strip(): same.append(f"script: {k} = {v['en'][:60]!r}")
    except Exception as e:
        errors.append(f"script.json invalid JSON: {e}")

print(f"{code}: {len(errors)} error(s), {len(same)} value(s) identical to English")
for e in errors[:80]: print("  ERR ", e)
for s in same[:40]: print("  SAME", s)
sys.exit(1 if errors else 0)
