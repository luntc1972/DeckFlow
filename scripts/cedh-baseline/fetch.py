#!/usr/bin/env python3
"""Fetch TopDeck.gg cEDH calibration decks and resolve cards through Scryfall."""

from __future__ import annotations

import argparse
import calendar
import datetime as dt
import email.utils
import hashlib
import http.client
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any


TOPDECK_URL = "https://topdeck.gg/api"
SCRYFALL_COLLECTION_URL = "https://api.scryfall.com/cards/collection"
USER_AGENT = "DeckFlow-cEDH-baseline/1.0 (+https://github.com/luntc1972/DeckFlow)"
COLLECTION_BATCH_SIZE = 75
# Mirror ScryfallThrottle's 200ms pace (~5 req/sec) to leave headroom for 429 burst detection.
COLLECTION_BATCH_DELAY_SECONDS = 0.2
SUPPLEMENT_LOOKBACK_MONTHS = 12
# Allow isolated player typos, but reject widespread resolution loss that could bias the baseline.
MAX_UNRESOLVED_DECK_FRACTION = 0.02
# Commanders too low-play for the size-tiered fetch; pulled by scanning the longer supplement window.
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

def main() -> int:
    args = parse_args()
    api_key = os.environ.get("TOPDECK_API_KEY", "").strip()
    if not api_key:
        raise SystemExit("TOPDECK_API_KEY is required for TopDeck.gg v2 requests.")
    outdir = Path(args.outdir)
    outdir.mkdir(parents=True, exist_ok=True)

    decks = fetch_all_decks(args.since, args.supplement_since, outdir / "topdeck_cache", api_key)
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

    # Drop whole decks so unresolved cards can never under-count their land totals.
    retained_decks = []
    unresolved_names: set[str] = set()
    for deck in decks:
        unresolved = sorted({
            name for name in deck["maindeck"] + deck["commanders"]
            if name not in card_cache
        })
        if unresolved:
            unresolved_names.update(unresolved)
            print(
                f"Dropped deck: {deck['cmdkey']} | {deck['tier']} | "
                f"unresolved: {', '.join(unresolved)}",
                file=sys.stderr,
            )
        else:
            retained_decks.append(deck)

    dropped_count = len(decks) - len(retained_decks)
    write_json(outdir / "decks_all.json", retained_decks)
    print(f"Wrote {outdir / 'decks_all.json'} with {len(retained_decks)} decks.")
    if dropped_count:
        print(f"Dropped {dropped_count} of {len(decks)} decks with unresolved cards.", file=sys.stderr)
        print(f"Distinct unresolved names: {', '.join(sorted(unresolved_names))}", file=sys.stderr)
    if dropped_count > MAX_UNRESOLVED_DECK_FRACTION * len(decks):
        print(
            f"ERROR: Dropped {dropped_count} of {len(decks)} decks with unresolved cards. "
            f"This exceeds the {MAX_UNRESOLVED_DECK_FRACTION:.0%} limit, so refusing to continue.",
            file=sys.stderr,
        )
        return 1

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


class TopDeckClient:
    """Paced TopDeck v2 requests with resumable, per-tournament standings."""

    def __init__(self, api_key: str, cache_dir: Path):
        self.api_key = api_key
        self.cache_dir = cache_dir
        self.next_request = 0.0

    def request(self, path: str, payload: dict[str, Any] | None = None) -> list[dict[str, Any]]:
        request = urllib.request.Request(
            TOPDECK_URL + path,
            data=json.dumps(payload).encode("utf-8") if payload is not None else None,
            headers={
                "Accept": "application/json",
                "Content-Type": "application/json",
                "Authorization": self.api_key,
                "User-Agent": USER_AGENT,
            },
            method="POST" if payload is not None else "GET",
        )
        return request_json_with_retries(request, topdeck=self)

    def standings(self, tid: str) -> list[dict[str, Any]]:
        # Hash IDs so remote tournament names can never become local paths.
        path = self.cache_dir / (hashlib.sha256(tid.encode("utf-8")).hexdigest() + ".json")
        if path.exists():
            rows = json.loads(path.read_text(encoding="utf-8"))
            if not isinstance(rows, list):
                raise RuntimeError(f"Invalid TopDeck standings cache: {path}")
            return rows
        rows = self.request(f"/v2/tournaments/{urllib.parse.quote(tid, safe='')}/standings")
        # Interrupted writes must not leave a cache file that looks complete.
        temporary = path.with_suffix(".tmp")
        write_json(temporary, rows)
        temporary.replace(path)
        return rows


def enumerate_tournaments(
    client: TopDeckClient, min_date: str, now: int,
) -> list[dict[str, Any]]:
    start = int(dt.datetime.combine(
        dt.date.fromisoformat(min_date), dt.time.min, tzinfo=dt.timezone.utc,
    ).timestamp())
    tournaments: dict[str, dict[str, Any]] = {}
    while start < now:
        end = min(start + 7 * 86400, now)
        events = client.request("/v2/tournaments", {
            "game": "Magic: The Gathering", "format": "EDH",
            "start": start, "end": end, "participantMin": 16, "columns": ["id"],
        })
        for event in events:
            event_date = event.get("startDate")
            if event_date is None or not start <= float(event_date) <= end:
                continue
            if not event.get("standings") or len(event["standings"]) < 16:
                continue
            tournaments[str(event["TID"])] = event
        start = end
    return list(tournaments.values())


def tier_for(size: int, standing: int) -> str | None:
    for config in TIER_CONFIGS:
        limits = config["filters"]
        if (limits["minSize"] <= size <= limits.get("maxSize", sys.maxsize)
                and 1 <= standing <= config["maxStanding"]):
            return config["label"]
    return None


def text_deck_sections(decklist: str) -> dict[str, dict[str, dict[str, int]]]:
    sections: dict[str, dict[str, dict[str, int]]] = {}
    section = ""
    for line in decklist.splitlines():
        line = line.strip()
        if line.startswith("~~") and line.endswith("~~"):
            section = line.strip("~").casefold()
        elif section in {"commanders", "mainboard"}:
            match = re.fullmatch(r"(\d+)\s+(.+)", line)
            if match:
                sections.setdefault(section, {})[match[2]] = {"count": int(match[1])}
    return sections


def commander_names(row: dict[str, Any], commanders: dict[str, Any]) -> list[str]:
    names = [name for raw_name in commanders if (name := normalize_name(raw_name))]
    if not names:
        # Structured commander sections are authoritative for partner boundaries.
        names = [name for raw_name in re.split(
            r"\s+(?:/|\+|&)\s+", row.get("leader") or "",
        ) if (name := normalize_name(raw_name))]
    return names


def supplement_commander_key(row: dict[str, Any]) -> str:
    obj = row.get("deckObj") or {}
    commanders = obj.get("Commanders") or {}
    if not commanders and not obj.get("Mainboard"):
        commanders = text_deck_sections(row.get("decklist") or "").get("commanders", {})
    return " / ".join(commander_names(row, commanders))


def map_deck(row: dict[str, Any], size: int, tier: str) -> dict[str, Any] | None:
    obj = row.get("deckObj") or {}
    commanders = obj.get("Commanders") or {}
    mainboard = obj.get("Mainboard") or {}
    if not mainboard:
        sections = text_deck_sections(row.get("decklist") or "")
        commanders = commanders or sections.get("commanders", {})
        mainboard = sections.get("mainboard", {})
    names = commander_names(row, commanders)
    maindeck = [
        name
        for raw_name, card in mainboard.items()
        if (count := int(card.get("count", 1))) > 0
        if (name := normalize_name(raw_name))
        for _ in range(count)
    ]
    standing = row.get("standing")
    if not names or not maindeck or standing is None or int(standing) < 1:
        return None
    return {
        "tier": tier, "size": size, "cmdkey": " / ".join(names),
        "commanders": names, "standing": int(standing), "maindeck": maindeck,
    }


def fetch_all_decks(
    min_date: str, supplement_min_date: str, cache_dir: Path, api_key: str,
) -> list[dict[str, Any]]:
    client = TopDeckClient(api_key, cache_dir)
    now = int(dt.datetime.now(dt.timezone.utc).timestamp())
    scan_since = min(min_date, supplement_min_date)
    tournaments = enumerate_tournaments(client, scan_since, now)
    tier_decks: dict[str, list[dict[str, Any]]] = {c["label"]: [] for c in TIER_CONFIGS}
    supplements: list[dict[str, Any]] = []
    wanted = {normalize_name(name) for name in SUPPLEMENT_COMMANDERS}
    mismatch_count = 0
    mismatch_examples: list[str] = []
    for event in tournaments:
        size = len(event["standings"])
        rows = client.standings(str(event["TID"]))
        if len(rows) != size:
            mismatch_count += 1
            if len(mismatch_examples) < 5:
                mismatch_examples.append(str(event["TID"]))
        event_date = dt.datetime.fromtimestamp(
            float(event["startDate"]), dt.timezone.utc,
        ).date().isoformat()
        for row in rows:
            standing = row.get("standing")
            if standing is None or int(standing) < 1:
                continue
            tier = tier_for(size, int(standing))
            keep_tier = tier is not None and event_date >= min_date
            if not keep_tier and (
                event_date < supplement_min_date or supplement_commander_key(row) not in wanted
            ):
                continue
            deck = map_deck(row, size, "commander-supplement")
            if deck is None:
                continue
            if keep_tier:
                tier_decks[tier].append(dict(deck, tier=tier))
            if event_date >= supplement_min_date and deck["cmdkey"] in wanted:
                supplements.append(deck)
    examples = f"; examples: {', '.join(mismatch_examples)}" if mismatch_examples else ""
    print(
        f"{mismatch_count} tournaments had bulk!=standings player counts{examples}",
        file=sys.stderr,
    )
    decks = [deck for config in TIER_CONFIGS for deck in tier_decks[config["label"]]]
    seen = {deck_identity(deck["cmdkey"], deck["maindeck"]) for deck in decks}
    for deck in supplements:
        identity = deck_identity(deck["cmdkey"], deck["maindeck"])
        if identity not in seen:
            seen.add(identity)
            decks.append(deck)
    return decks


def deck_identity(cmdkey: str, maindeck: list[str]) -> tuple[str, tuple[str, ...]]:
    return cmdkey, tuple(sorted(maindeck))


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
    return request_json_with_retries(request)


def request_json_with_retries(
    request: urllib.request.Request, *, topdeck: TopDeckClient | None = None,
) -> Any:
    network_errors = (urllib.error.URLError,)
    if topdeck is not None:
        network_errors += (ConnectionError, TimeoutError, http.client.HTTPException, json.JSONDecodeError)
    for attempt in range(4):
        if topdeck is not None:
            time.sleep(max(0.0, topdeck.next_request - time.monotonic()))
            topdeck.next_request = time.monotonic() + 1.05
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                result = json.loads(response.read().decode("utf-8"))
            if topdeck is not None and not isinstance(result, list):
                raise RuntimeError("TopDeck returned an unexpected response; expected a list.")
            return result
        except urllib.error.HTTPError as exc:
            if exc.code in {429, 500, 502, 503, 504} and attempt < 3:
                time.sleep(http_retry_delay_seconds(exc, attempt, topdeck=topdeck is not None))
                continue
            if topdeck is not None:
                raise RuntimeError(f"TopDeck request failed (HTTP {exc.code}).") from None
            raise
        # TopDeck also retries reads and truncated JSON after the connection opens.
        except network_errors:
            if attempt < 3:
                time.sleep(1.0 + attempt)
                continue
            if topdeck is not None:
                raise RuntimeError("TopDeck request failed after four attempts.") from None
            raise
    raise RuntimeError("TopDeck request exhausted retries.")


def http_retry_delay_seconds(
    exc: urllib.error.HTTPError, attempt: int, *, topdeck: bool = False,
) -> float:
    retry_after = exc.headers.get("Retry-After")
    if topdeck:
        delay = float(2 ** attempt)
        if retry_after:
            try:
                delay = max(delay, float(retry_after))
            except ValueError:
                try:
                    cooldown = email.utils.parsedate_to_datetime(retry_after)
                    delay = max(delay, cooldown.timestamp() - time.time())
                except (TypeError, ValueError, OverflowError):
                    pass
        return delay
    if retry_after is not None:
        try:
            # Honor upstream cooldowns when provided, but keep the bounded retry loop paced.
            return max(COLLECTION_BATCH_DELAY_SECONDS, min(float(retry_after), 30.0))
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
    return list(dict.fromkeys(
        name
        for deck in decks
        for name in (*deck["commanders"], *deck["maindeck"])
        if name
    ))


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
                retried, _ = resolve_collection_batch(list(retry_map.values()))
                for original_name, front_face in retry_map.items():
                    if front_face in retried:
                        card_cache[original_name] = retried[front_face]
                unresolved = [
                    name
                    for name in unresolved
                    if name not in card_cache and retry_map.get(name) not in retried
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
    # TopDeck varies underscore counts; Scryfall uses five for _____ Goblin.
    return re.sub(r"_{3,}", "_____", (name or "").strip())


if __name__ == "__main__":
    raise SystemExit(main())
