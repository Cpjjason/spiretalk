"""Watch a Workshop item: subscriber numbers, new comments, new discussion threads.

    python tools/workshop_watch.py                 # poll every 5 minutes, print only what changed
    python tools/workshop_watch.py --once          # one pass and exit
    python tools/workshop_watch.py --id 3748235030 --creator 76561198008025242 --once   # any other item

Everything read here is public (no key, no login). State and a running log live next to this file
in workshop_watch/ so a restart does not repeat old comments. Each printed line is one event.
"""
import argparse, html, json, os, re, sys, time, urllib.parse, urllib.request
from datetime import datetime

HERE = os.path.dirname(os.path.abspath(__file__))
UA = {"User-Agent": "Mozilla/5.0 SpireTalk-watch"}


def get(url, data=None, timeout=30):
    body = urllib.parse.urlencode(data).encode() if data else None
    req = urllib.request.Request(url, body, UA)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode("utf-8", "replace")


def stats(item):
    d = json.loads(get("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                       {"itemcount": 1, "publishedfileids[0]": item}))
    f = d["response"]["publishedfiledetails"][0]
    if f.get("result") != 1:
        return None
    return {k: f.get(k) for k in ("subscriptions", "lifetime_subscriptions", "favorited", "views", "visibility", "time_updated")}


def item_page(item):
    return get(f"https://steamcommunity.com/sharedfiles/filedetails/?id={item}")


def rating(page):
    stars = re.search(r"(\d)-star_large\.png", page) or re.search(r"(not-yet)_large\.png", page)
    votes = re.search(r'class="numRatings">\s*([\d,]+)', page)
    return (stars.group(1) if stars else "?"), (votes.group(1).replace(",", "") if votes else "0")


def gallery(page):
    """Every image on the item page (cover + screenshots) and the ones that no longer load.
    The gallery has gone blank twice without anyone touching it, so this is checked every poll."""
    urls = sorted(set(re.findall(r"https://images\.steamusercontent\.com/ugc/\d+/[0-9A-F]{40}/", page)))
    broken = []
    for u in urls:
        try:
            req = urllib.request.Request(u, headers={**UA, "Range": "bytes=0-0"})
            with urllib.request.urlopen(req, timeout=20) as r:
                if r.status not in (200, 206):
                    broken.append(u)
        except Exception:
            broken.append(u)
    return urls, broken


def comments(creator, item, count=50):
    d = json.loads(get(f"https://steamcommunity.com/comment/PublishedFile_Public/render/{creator}/{item}/?start=0&count={count}"))
    out = []
    for m in re.finditer(r'id="comment_(\d+)".*?<bdi>(.*?)</bdi>.*?data-timestamp="(\d+)".*?commentthread_comment_text"[^>]*>(.*?)</div>',
                         d.get("comments_html", ""), re.S):
        text = re.sub(r"<br\s*/?>", "\n", html.unescape(m.group(4)))
        text = re.sub(r"<[^>]+>", "", text).strip()
        out.append({"id": m.group(1), "author": html.unescape(re.sub(r"<[^>]+>", "", m.group(2))).strip(),
                    "ts": int(m.group(3)), "text": text})
    return d.get("total_count", 0), out


def discussions(item):
    page = get(f"https://steamcommunity.com/sharedfiles/filedetails/discussions/{item}")
    out = []
    for m in re.finditer(r'<a class="forum_topic_overlay" href="([^"]+)".*?forum_topic_name[^>]*>(.*?)</div>.*?forum_topic_reply_count[^>]*>.*?(\d+)', page, re.S):
        out.append({"url": m.group(1), "title": html.unescape(re.sub(r"<[^>]+>|\s+", " ", m.group(2))).strip(), "replies": int(m.group(3))})
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--id", default="3806015083")
    ap.add_argument("--creator", default="76561199129165177")
    ap.add_argument("--every", type=int, default=300)
    ap.add_argument("--once", action="store_true")
    a = ap.parse_args()

    root = os.path.join(HERE, "workshop_watch")
    os.makedirs(root, exist_ok=True)
    state_path = os.path.join(root, f"state_{a.id}.json")
    log_path = os.path.join(root, f"log_{a.id}.txt")
    state = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else {"seen": [], "threads": {}, "stats": None, "rating": None}

    def emit(line):
        stamp = datetime.now().strftime("%m-%d %H:%M")
        print(f"[{stamp}] {line}", flush=True)
        with open(log_path, "a", encoding="utf-8") as f:
            f.write(f"[{stamp}] {line}\n")

    while True:
        try:
            s = stats(a.id)
            if s is None:
                if state.get("stats") != "hidden":
                    emit("item not visible to the public yet (hidden / friends only); stats and comments start once it is public")
                state["stats"] = "hidden"
            else:
                old = state.get("stats") if isinstance(state.get("stats"), dict) else None
                if old != {k: s[k] for k in s if k != "time_updated"}:
                    delta = "" if not old else " (" + ", ".join(f"{k} {s[k] - old.get(k, 0):+d}" for k in ("subscriptions", "favorited", "views") if s[k] != old.get(k)) + ")"
                    emit(f"stats: subscribers {s['subscriptions']} · lifetime {s['lifetime_subscriptions']} · favorites {s['favorited']} · views {s['views']}{delta}")
                    state["stats"] = {k: s[k] for k in s if k != "time_updated"}
                page = item_page(a.id)
                urls, broken = gallery(page)
                g = [len(urls), len(broken)]
                if broken or (state.get("gallery") and len(urls) < state["gallery"][0]):
                    emit(f"GALLERY BROKEN: {len(broken)} of {len(urls)} images fail to load" + (f" (was {state['gallery'][0]} images)" if state.get("gallery") else "") + "; rerun workshop/upload-screenshots.cmd")
                elif state.get("gallery") != g:
                    emit(f"gallery: {len(urls)} images, all load")
                state["gallery"] = g
                r = rating(page)
                if state.get("rating") != list(r):
                    emit(f"rating: {r[0]} stars from {r[1]} votes")
                    state["rating"] = list(r)
                total, cs = comments(a.creator, a.id)
                for c in sorted(cs, key=lambda c: c["ts"]):
                    if c["id"] in state["seen"]:
                        continue
                    state["seen"].append(c["id"])
                    when = datetime.fromtimestamp(c["ts"]).strftime("%m-%d %H:%M")
                    emit(f"comment ({when}) {c['author']}: {c['text'].replace(chr(10), ' / ')}")
                for t in discussions(a.id):
                    before = state["threads"].get(t["url"])
                    if before is None:
                        emit(f"new discussion: {t['title']} ({t['replies']} replies) {t['url']}")
                    elif t["replies"] > before:
                        emit(f"discussion reply: {t['title']} now {t['replies']} replies {t['url']}")
                    state["threads"][t["url"]] = t["replies"]
            json.dump(state, open(state_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        except Exception as e:  # one bad poll should not stop the watch
            emit(f"poll failed: {type(e).__name__}: {e}")
        if a.once:
            return
        time.sleep(a.every)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
