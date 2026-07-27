#!/usr/bin/env python3
"""Fetch EDHTop16 cEDH calibration decks and resolve cards through Scryfall."""

from __future__ import annotations

import argparse
import calendar
import datetime as dt
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any


EDHTOP16_URL = "https://edhtop16.com/api/graphql"
SCRYFALL_COLLECTION_URL = "https://api.scryfall.com/cards/collection"
USER_AGENT = "DeckFlow-cEDH-baseline/1.0 (+https://github.com/luntc1972/DeckFlow)"
COLLECTION_BATCH_SIZE = 75
# Mirror ScryfallThrottle's 200ms pace (~5 req/sec) to leave headroom for 429 burst detection.
COLLECTION_BATCH_DELAY_SECONDS = 0.2
PAGE_SIZE = 100
SUPPLEMENT_LOOKBACK_MONTHS = 12
# Commanders too low-play for the size-tiered fetch; pulled via commander-specific search.
SUPPLEMENT_COMMANDERS = ["Plagon, Lord of the Beach"]

TIER_CONFIGS = [
    {
        "label": "16-32 winner",
        "filters": {"minSize": 16, "maxSize": 32},
        "maxStanding": 1,
    },
    {
        "label": "33-63 top4",
        "filters": {"minSize": 33, "maxSize": 63},
        "maxStanding": 4,
    },
    {
        "label": "64+ top16",
        "filters": {"minSize": 64},
        "maxStanding": 16,
    },
]

TOURNAMENTS_QUERY = """
query Tournaments($first: Int!, $after: String, $minDate: String!, $minSize: Int, $maxSize: Int, $maxStanding: Int) {
  tournaments(
    first: $first
    after: $after
    filters: { minDate: $minDate, minSize: $minSize, maxSize: $maxSize }
    sortBy: DATE
  ) {
    pageInfo {
      hasNextPage
      endCursor
    }
    edges {
      node {
        size
        tournamentDate
        entries(maxStanding: $maxStanding) {
          standing
          commander { name }
          maindeck { name }
        }
      }
    }
  }
}
""".strip()

COMMANDER_ENTRIES_QUERY = """
query($name:String!, $first:Int!, $after:String){
  commander(name:$name){ name
    entries(first:$first, after:$after){
      edges{ node{ standing maindeck{ name } tournament{ tournamentDate size } } }
      pageInfo{ hasNextPage endCursor } } } }
""".strip()


def main() -> int:
    args = parse_args()
    outdir = Path(args.outdir)
    outdir.mkdir(parents=True, exist_ok=True)

    decks = fetch_all_decks(args.since, args.supplement_since)
    write_json(outdir / "decks_all.json", decks)
    print(f"Wrote {outdir / 'decks_all.json'} with {len(decks)} decks.")

    cards_path = outdir / "cards_full.json"
    card_cache = load_card_cache(cards_path)
    all_names = distinct_card_names(decks)
    missing_names = [name for name in all_names if name not in card_cache]
    print(
        f"Card cache has {len(card_cache)} cards; resolving {len(missing_names)} missing names.",
        file=sys.stderr,
    )
    resolve_missing_cards(missing_names, card_cache, cards_path)
    print(f"Wrote {cards_path} with {len(card_cache)} cached cards.")

    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--since",
        default=default_since(),
        help="Minimum tournament date in YYYY-MM-DD format (default: today minus 6 months).",
    )
    parser.add_argument(
        "--outdir",
        default="_calib",
        help="Output/cache directory (default: _calib).",
    )
    parser.add_argument(
        "--supplement-since",
        default=default_supplement_since(),
        help="Minimum tournament date for commander supplements in YYYY-MM-DD format (default: today minus 12 months).",
    )
    args = parser.parse_args()
    try:
        dt.date.fromisoformat(args.since)
    except ValueError as exc:
        raise SystemExit(f"--since must be YYYY-MM-DD: {exc}") from exc
    try:
        dt.date.fromisoformat(args.supplement_since)
    except ValueError as exc:
        raise SystemExit(f"--supplement-since must be YYYY-MM-DD: {exc}") from exc
    return args


def default_since() -> str:
    # 6-month window: wide enough to give lower-play cEDH commanders a usable N>=10 sample while
    # staying recent enough to track the current meta (land counts are among the more stable attrs).
    return months_ago(6)


def default_supplement_since() -> str:
    return months_ago(SUPPLEMENT_LOOKBACK_MONTHS)


def months_ago(months: int) -> str:
    today = dt.date.today()
    year = today.year
    month = today.month - months
    while month <= 0:
        month += 12
        year -= 1
    day = min(today.day, calendar.monthrange(year, month)[1])
    return dt.date(year, month, day).isoformat()


def fetch_all_decks(min_date: str, supplement_min_date: str) -> list[dict[str, Any]]:
    decks: list[dict[str, Any]] = []
    for config in TIER_CONFIGS:
        tier_decks = fetch_tier(config, min_date)
        print(f"Fetched {len(tier_decks)} decks for {config['label']}.", file=sys.stderr)
        decks.extend(tier_decks)
    supplement_decks = fetch_supplement_decks(supplement_min_date, decks)
    if supplement_decks:
        print(f"Fetched {len(supplement_decks)} supplement decks.", file=sys.stderr)
        decks.extend(supplement_decks)
    return decks


def fetch_tier(config: dict[str, Any], min_date: str) -> list[dict[str, Any]]:
    after: str | None = None
    decks: list[dict[str, Any]] = []
    while True:
        variables = {
            "first": PAGE_SIZE,
            "after": after,
            "minDate": min_date,
            "minSize": config["filters"].get("minSize"),
            "maxSize": config["filters"].get("maxSize"),
            "maxStanding": config["maxStanding"],
        }
        payload = graphql_request(TOURNAMENTS_QUERY, variables)
        tournaments = payload["data"]["tournaments"]
        for edge in tournaments["edges"]:
            node = edge["node"]
            for entry in node.get("entries", []):
                commander_name = normalize_name(entry["commander"]["name"])
                if not commander_name:
                    continue
                commanders = [name for name in commander_name.split(" / ") if name]
                maindeck = [
                    name
                    for name in (normalize_name(card["name"]) for card in entry.get("maindeck", []))
                    if name
                ]
                decks.append(
                    {
                        "tier": config["label"],
                        "size": node["size"],
                        "cmdkey": " / ".join(commanders),
                        "commanders": commanders,
                        "standing": entry["standing"],
                        "maindeck": maindeck,
                    }
                )

        page_info = tournaments["pageInfo"]
        if not page_info["hasNextPage"]:
            break
        after = page_info["endCursor"]
    return decks


def fetch_supplement_decks(min_date: str, existing_decks: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if not SUPPLEMENT_COMMANDERS:
        return []

    min_date_value = dt.date.fromisoformat(min_date)
    seen = {
        deck_identity(deck["cmdkey"], deck["maindeck"])
        for deck in existing_decks
    }
    decks: list[dict[str, Any]] = []
    for commander_name in SUPPLEMENT_COMMANDERS:
        commander_decks = fetch_commander_entries(commander_name, min_date_value, seen)
        print(
            f"Fetched {len(commander_decks)} supplement decks for {commander_name}.",
            file=sys.stderr,
        )
        decks.extend(commander_decks)
    return decks


def fetch_commander_entries(
    commander_name: str,
    min_date: dt.date,
    seen: set[tuple[str, tuple[str, ...]]],
) -> list[dict[str, Any]]:
    after: str | None = None
    decks: list[dict[str, Any]] = []
    normalized_commander = normalize_name(commander_name)
    if not normalized_commander:
        return decks

    while True:
        payload = graphql_request(
            COMMANDER_ENTRIES_QUERY,
            {"name": normalized_commander, "first": PAGE_SIZE, "after": after},
        )
        commander = payload["data"].get("commander")
        if commander is None:
            break

        entries = commander["entries"]
        for edge in entries["edges"]:
            node = edge["node"]
            tournament = node.get("tournament") or {}
            tournament_date_raw = tournament.get("tournamentDate")
            if not tournament_date_raw:
                continue
            tournament_date = parse_api_date(tournament_date_raw)
            if tournament_date < min_date:
                continue

            maindeck = [
                name
                for name in (normalize_name(card["name"]) for card in node.get("maindeck", []))
                if name
            ]
            deck = {
                "tier": "commander-supplement",
                "size": tournament.get("size"),
                "cmdkey": normalized_commander,
                "commanders": [normalized_commander],
                "standing": node["standing"],
                "maindeck": maindeck,
            }
            identity = deck_identity(deck["cmdkey"], deck["maindeck"])
            if identity in seen:
                continue
            seen.add(identity)
            decks.append(deck)

        page_info = entries["pageInfo"]
        if not page_info["hasNextPage"]:
            break
        after = page_info["endCursor"]

    return decks


def deck_identity(cmdkey: str, maindeck: list[str]) -> tuple[str, tuple[str, ...]]:
    return cmdkey, tuple(sorted(maindeck))


def parse_api_date(value: str) -> dt.date:
    return dt.date.fromisoformat(value.split("T", 1)[0])


def graphql_request(query: str, variables: dict[str, Any]) -> dict[str, Any]:
    body = {"query": query, "variables": variables}
    payload = json_request(EDHTOP16_URL, body)
    if payload.get("errors"):
        raise RuntimeError(f"GraphQL request failed: {payload['errors']}")
    return payload


def json_request(url: str, payload: dict[str, Any]) -> dict[str, Any]:
    request = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Accept": "application/json",
            "Content-Type": "application/json",
            "User-Agent": USER_AGENT,
        },
        method="POST",
    )
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                return json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            if exc.code in {429, 500, 502, 503, 504} and attempt < 3:
                time.sleep(http_retry_delay_seconds(exc, attempt))
                continue
            raise
        except urllib.error.URLError:
            if attempt < 3:
                time.sleep(1.0 + attempt)
                continue
            raise


def http_retry_delay_seconds(exc: urllib.error.HTTPError, attempt: int) -> float:
    retry_after = exc.headers.get("Retry-After")
    if retry_after is not None:
        try:
            # Honor upstream cooldowns when provided, but keep a single retry bounded.
            return min(float(retry_after), 30.0)
        except ValueError:
            pass

    # Exponential backoff gives Scryfall and Cloudflare more time to clear rate-limit windows.
    return min(float(2 ** attempt), 30.0)


def load_card_cache(cards_path: Path) -> dict[str, dict[str, Any]]:
    if not cards_path.exists():
        return {}
    with cards_path.open("r", encoding="utf-8") as handle:
        loaded = json.load(handle)
    if not isinstance(loaded, dict):
        raise SystemExit(f"{cards_path} must contain a JSON object.")
    return loaded


def distinct_card_names(decks: list[dict[str, Any]]) -> list[str]:
    names: dict[str, None] = {}
    for deck in decks:
        for name in deck["commanders"]:
            if name:
                names.setdefault(name, None)
        for name in deck["maindeck"]:
            if name:
                names.setdefault(name, None)
    return list(names)


def resolve_missing_cards(
    missing_names: list[str],
    card_cache: dict[str, dict[str, Any]],
    cards_path: Path,
) -> None:
    for offset in range(0, len(missing_names), COLLECTION_BATCH_SIZE):
        if offset > 0:
            time.sleep(COLLECTION_BATCH_DELAY_SECONDS)

        batch = missing_names[offset : offset + COLLECTION_BATCH_SIZE]
        resolved, unresolved = resolve_collection_batch(batch)
        card_cache.update(resolved)

        if unresolved:
            retry_map = {
                name: name.split(" // ", 1)[0]
                for name in unresolved
                if " // " in name
            }
            if retry_map:
                time.sleep(COLLECTION_BATCH_DELAY_SECONDS)
                retried, still_missing = resolve_collection_batch(list(retry_map.values()))
                for original_name, front_face in retry_map.items():
                    if front_face in retried:
                        card_cache[original_name] = retried[front_face]
                unresolved = [
                    name
                    for name in unresolved
                    if name not in card_cache and retry_map.get(name) not in retried
                ]
                if still_missing:
                    unresolved = [
                        name
                        for name in unresolved
                        if retry_map.get(name) not in still_missing
                    ] + [
                        name
                        for name, front_face in retry_map.items()
                        if front_face in still_missing and name not in unresolved
                    ]

        write_json(cards_path, card_cache)
        if unresolved:
            print(
                f"Warning: Scryfall could not resolve {len(unresolved)} names in this batch.",
                file=sys.stderr,
            )


def resolve_collection_batch(names: list[str]) -> tuple[dict[str, dict[str, Any]], list[str]]:
    payload = {
        "identifiers": [{"name": name} for name in names],
    }
    response = json_request(SCRYFALL_COLLECTION_URL, payload)
    resolved: dict[str, dict[str, Any]] = {}
    for card in response.get("data", []):
        resolved[card["name"]] = card
        # Scryfall echoes the full DFC name even when the query used only the front face.
        front_face = card["name"].split(" // ", 1)[0]
        if front_face != card["name"]:
            resolved.setdefault(front_face, card)

    unresolved = []
    for missing in response.get("not_found", []):
        name = missing.get("name")
        if name:
            unresolved.append(name)
    return resolved, unresolved


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(data, handle, ensure_ascii=False, indent=2, sort_keys=isinstance(data, dict))
        handle.write("\n")


def normalize_name(name: str | None) -> str:
    return (name or "").strip()


if __name__ == "__main__":
    raise SystemExit(main())
