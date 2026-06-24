#!/usr/bin/env python3
"""Harvest N recent real Commander decks from Archidekt for the manabase flag baseline.

Scrapes recent deck IDs from the search endpoint, fetches each deck's JSON, keeps only
proper ~99-card singleton Commander (format 3) decks, and writes their decklists to
archidekt-baseline-decks.json in the form [{"name":..., "list":"1 Commander\\n1 Card\\n..."}]
which the ManabaseFlagBaselineHarness consumes (resolving names through Scryfall).
"""
import json
import sys
import time
import urllib.request

UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/135.0.0.0 Safari/537.36"
WANT = int(sys.argv[1]) if len(sys.argv) > 1 else 5
SKIP_CATEGORIES = {"Sideboard", "Maybeboard", "Considering"}


def get(url):
    req = urllib.request.Request(url, headers={
        "User-Agent": UA,
        "Accept": "application/json, text/html;q=0.9,*/*;q=0.8",
        "Referer": "https://archidekt.com/",
    })
    with urllib.request.urlopen(req, timeout=30) as r:
        return r.read().decode("utf-8", "replace")


def recent_ids():
    import re
    seen = []
    for page in (1, 2, 3):
        html = get(f"https://websockets.archidekt.com/search/decks?name=&orderBy=-updatedAt&page={page}")
        for m in re.findall(r"/decks/(\d+)", html):
            if m not in seen:
                seen.append(m)
        time.sleep(0.3)
    return seen


def deck_to_list(d):
    """Return (name, list_str) if d is a proper ~99 singleton Commander deck, else None."""
    if d.get("deckFormat") != 3:
        return None
    cards = d.get("cards", [])
    commander_lines, main_lines = [], []
    total = 0
    max_nonbasic_qty = 0
    basics = {"Plains", "Island", "Swamp", "Mountain", "Forest", "Wastes", "Snow-Covered Plains",
              "Snow-Covered Island", "Snow-Covered Swamp", "Snow-Covered Mountain", "Snow-Covered Forest"}
    for c in cards:
        cats = c.get("categories") or []
        if any(cat in SKIP_CATEGORIES for cat in cats):
            continue
        oc = (c.get("card") or {}).get("oracleCard") or {}
        name = oc.get("name")
        if not name:
            continue
        name = name.split(" // ")[0]
        qty = int(c.get("quantity") or 1)
        total += qty
        is_cmdr = "Commander" in cats
        if name not in basics and qty > max_nonbasic_qty:
            max_nonbasic_qty = qty
        (commander_lines if is_cmdr else main_lines).append(f"{qty} {name}")
    # Proper Commander deck: ~100 cards, has 1-2 commanders, singleton (no >1 nonbasic).
    if not commander_lines or not (95 <= total <= 102) or max_nonbasic_qty > 1:
        return None
    return d.get("name", "Unknown"), "\n".join(commander_lines + main_lines)


def main():
    ids = recent_ids()
    out = []
    for did in ids:
        if len(out) >= WANT:
            break
        try:
            d = json.loads(get(f"https://archidekt.com/api/decks/{did}/"))
        except Exception as e:
            print(f"  skip {did}: {e}", file=sys.stderr)
            continue
        res = deck_to_list(d)
        if res:
            name, lst = res
            out.append({"id": did, "name": name, "list": lst})
            print(f"  kept {did}: {name} ({len(lst.splitlines())} lines)", file=sys.stderr)
        else:
            print(f"  reject {did}: {d.get('name','?')[:30]} fmt={d.get('deckFormat')}", file=sys.stderr)
        time.sleep(0.4)

    with open("archidekt-baseline-decks.json", "w") as f:
        json.dump(out, f, indent=1)
    print(f"wrote {len(out)} decks to archidekt-baseline-decks.json")


if __name__ == "__main__":
    main()
