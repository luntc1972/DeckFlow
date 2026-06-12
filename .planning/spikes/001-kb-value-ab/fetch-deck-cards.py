#!/usr/bin/env python3
"""Fetch real Scryfall data for the spike deck, emit C# ScryfallCard initializers."""
import json
import os
import re
import sys
import time
import urllib.request

def parse_deck(path):
    entries = []
    names = []  # unique, order-preserving
    seen = set()
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line in ("Commander", "Mainboard"):
                continue
            m = re.match(r"^(\d+)\s+(.*)$", line)
            if not m:
                continue
            quantity = int(m.group(1))
            name = m.group(2).strip()
            entries.append((quantity, name))
            if name not in seen:
                seen.add(name)
                names.append(name)
    return entries, names

def front_face_name(name):
    return name.split(" // ")[0].strip()

def fetch(identifiers):
    payload = json.dumps({"identifiers": [{"name": n} for n in identifiers]}).encode()
    req = urllib.request.Request(
        "https://api.scryfall.com/cards/collection",
        data=payload,
        headers={"Content-Type": "application/json", "User-Agent": "deckflow-spike/1.0", "Accept": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read())

def cs_str(s):
    if s is None:
        return "null"
    s = s.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "").replace("\n", "\\n")
    return f'"{s}"'

def cs_list(items):
    if not items:
        return "[]"
    return "[" + ", ".join(cs_str(x) for x in items) + "]"

def main():
    if len(sys.argv) != 4:
        print(f"usage: {os.path.basename(sys.argv[0])} <deck.txt> <cards_cs.txt> <decklist.txt>", file=sys.stderr)
        sys.exit(2)

    deck_path, cards_output_path, decklist_output_path = sys.argv[1:4]
    entries, names = parse_deck(deck_path)
    lookup_names = []
    seen_lookup = set()
    for name in names:
        lookup_name = front_face_name(name)
        if lookup_name not in seen_lookup:
            seen_lookup.add(lookup_name)
            lookup_names.append(lookup_name)
    print(f"// parsed {len(names)} unique names", file=sys.stderr)
    cards = {}
    not_found = []
    for i in range(0, len(lookup_names), 75):
        batch = lookup_names[i:i+75]
        data = fetch(batch)
        for c in data.get("data", []):
            cards[c["name"]] = c
        for nf in data.get("not_found", []):
            not_found.append(nf.get("name", str(nf)))
        time.sleep(0.15)
    if not_found:
        print(f"// NOT FOUND ({len(not_found)}): {not_found}", file=sys.stderr)
    unmatched = []
    # Emit C# initializers in deck order
    lines = []
    # index by front-face name too (DFC canonical names are "A // B")
    by_front = {}
    for cn, c in cards.items():
        by_front.setdefault(cn.split(" // ")[0], c)
    for n in names:
        c = cards.get(n) or by_front.get(n) or by_front.get(front_face_name(n))
        if c is None:
            print(f"// UNMATCHED: {n}", file=sys.stderr)
            unmatched.append(n)
            continue
        name = c["name"]
        mana = c.get("mana_cost")
        type_line = c.get("type_line", "")
        oracle = c.get("oracle_text")
        power = c.get("power")
        tough = c.get("toughness")
        keywords = c.get("keywords") or []
        ci = c.get("color_identity") or []
        setc = c.get("set")
        setn = c.get("set_name")
        coll = c.get("collector_number")
        # DFC: oracle_text empty at top -> join faces
        if (oracle is None or oracle == "") and "card_faces" in c:
            faces = c["card_faces"]
            oracle = "\n//\n".join(f.get("oracle_text", "") for f in faces)
            if not mana:
                mana = faces[0].get("mana_cost")
            if not type_line:
                type_line = faces[0].get("type_line", "")
        lines.append(
            "        new(" + ", ".join([
                cs_str(name), cs_str(mana), cs_str(type_line), cs_str(oracle),
                cs_str(power), cs_str(tough), cs_list(keywords), cs_list(ci),
                cs_str(setc), cs_str(setn), cs_str(coll),
            ]) + "),"
        )
    print(f"// emitted {len(lines)} cards", file=sys.stderr)
    with open(cards_output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    with open(decklist_output_path, "w", encoding="utf-8") as f:
        f.write("Commander\n")
        for quantity, name in entries:
            canonical = (cards.get(name) or by_front.get(name) or by_front.get(front_face_name(name)) or {"name": name})["name"]
            f.write(f"{quantity} {canonical}\n")
    print(f"// wrote {cards_output_path}", file=sys.stderr)
    print(f"// wrote {decklist_output_path}", file=sys.stderr)
    if not_found or unmatched:
        raise SystemExit(1)

if __name__ == "__main__":
    main()
