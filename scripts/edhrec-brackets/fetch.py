#!/usr/bin/env python3
"""Fetch trimmed EDHREC average-deck bracket cells for solo commanders."""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import hashlib
import json
import re
import sys
import time
import unicodedata
import urllib.error
import urllib.request
from email.utils import parsedate_to_datetime
from pathlib import Path


AVERAGES_DEFAULT = "artifacts/edhrec/averages-jul26-m5o50xfj/averages.csv"
OUTDIR_DEFAULT = "_edhrec-brackets"
DEFAULT_BRACKETS = ["exhibition", "core", "upgraded", "optimized", "cedh"]
BRACKET_INDEX = {
    "exhibition": 1,
    "core": 2,
    "upgraded": 3,
    "optimized": 4,
    "cedh": 5,
}
USER_AGENT = "DeckFlow-EDHREC-brackets/1.0 (+https://github.com/luntc1972/DeckFlow/issues)"
REQUEST_TIMEOUT_SECONDS = 60
MAX_TRANSIENT_ATTEMPTS = 3
MAX_BACKOFF_SECONDS = 120
COMMANDER_CARD_KEYS = [
    "num_decks",
    "rank",
    "cmc",
    "color_identity",
    "salt",
    "name",
    "sanitized",
    "primary_type",
]


def main() -> int:
    args = parse_args()
    averages_path = Path(args.averages)
    commanders = load_selected_commanders(averages_path, args.min_decks, args.limit)
    if not commanders:
        raise SystemExit(
            f"No solo commanders met --min-decks {args.min_decks} in {averages_path}."
        )

    cells = build_cells(commanders, args.brackets)
    if args.dry_run:
        print_dry_run(averages_path, commanders, args.brackets, cells)
        return 0

    csv_size, csv_sha256 = file_metadata(averages_path)
    started_utc = utc_now()
    stats = {
        "cells_attempted": 0,
        "cells_written": 0,
        "cells_skipped_existing": 0,
        "cells_404": 0,
        "cells_failed": 0,
        "request_attempts_total": 0,
    }
    failed_cells: list[dict[str, object]] = []
    missing_cells: list[dict[str, object]] = []
    commander_statuses: dict[str, list[str]] = {
        commander["slug"]: [] for commander in commanders
    }
    gate = RequestGate(args.throttle)

    for cell in cells:
        cell_path = cell_output_path(Path(args.outdir), cell["slug"], cell["bracket"])
        if is_valid_json(cell_path):
            stats["cells_skipped_existing"] += 1
            commander_statuses[cell["slug"]].append("present")
            print(f"skip existing {cell['slug']} [{cell['bracket']}]", file=sys.stderr)
            continue

        stats["cells_attempted"] += 1
        fetch_result = fetch_cell(cell, cell_path, args.user_agent, gate)
        stats["request_attempts_total"] += fetch_result["request_attempts"]

        if fetch_result["status"] == "written":
            stats["cells_written"] += 1
            commander_statuses[cell["slug"]].append("present")
            continue

        if fetch_result["status"] == "missing":
            stats["cells_404"] += 1
            commander_statuses[cell["slug"]].append("404")
            missing_cells.append(cell_audit_record(cell, fetch_result["detail"]))
            continue

        stats["cells_failed"] += 1
        commander_statuses[cell["slug"]].append("failed")
        failed_cells.append(cell_audit_record(cell, fetch_result["detail"]))

    unresolved = unresolved_commanders(commanders, commander_statuses, len(args.brackets))
    ended_utc = utc_now()

    manifest = {
        "fetch_started_utc": started_utc,
        "fetch_ended_utc": ended_utc,
        "user_agent": args.user_agent,
        "averages_csv": {
            "path": str(averages_path),
            "byte_size": csv_size,
            "sha256": csv_sha256,
        },
        "min_decks": args.min_decks,
        "brackets": args.brackets,
        "commanders_selected": len(commanders),
        "selected_commanders": [
            {
                "commander": commander["commander"],
                "slug": commander["slug"],
                "oracle_id": commander["oracle_id"],
            }
            for commander in commanders
        ],
        "cells_planned": len(cells),
        "cells_attempted": stats["cells_attempted"],
        "cells_written": stats["cells_written"],
        "cells_skipped_existing": stats["cells_skipped_existing"],
        "cells_404": stats["cells_404"],
        "cells_failed": stats["cells_failed"],
        "request_attempts_total": stats["request_attempts_total"],
        "failed_cells": failed_cells,
        "missing_cells": missing_cells,
        "unresolved_slug_count": len(unresolved),
    }
    write_json(Path(args.outdir) / "manifest.json", manifest)
    write_unresolved(Path(args.outdir) / "unresolved-slugs.txt", unresolved)

    print(
        "Summary: "
        f"selected={len(commanders)} cells={len(cells)} attempted={stats['cells_attempted']} "
        f"written={stats['cells_written']} skipped-existing={stats['cells_skipped_existing']} "
        f"404={stats['cells_404']} failed={stats['cells_failed']} unresolved={len(unresolved)}",
        file=sys.stderr,
    )

    if stats["cells_attempted"] > 0 and stats["cells_failed"] / stats["cells_attempted"] > 0.25:
        return 4
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--averages",
        default=AVERAGES_DEFAULT,
        help=(
            "Path to the EDHREC averages.csv input "
            f"(default: {AVERAGES_DEFAULT})."
        ),
    )
    parser.add_argument(
        "--outdir",
        default=OUTDIR_DEFAULT,
        help=f"Output/cache directory (default: {OUTDIR_DEFAULT}).",
    )
    parser.add_argument(
        "--min-decks",
        type=int,
        default=8000,
        help="Minimum number_decks required to fetch a commander (default: 8000).",
    )
    parser.add_argument(
        "--brackets",
        default=",".join(DEFAULT_BRACKETS),
        help=(
            "Comma-separated bracket slugs to fetch "
            f"(default: {','.join(DEFAULT_BRACKETS)})."
        ),
    )
    parser.add_argument(
        "--throttle",
        type=float,
        default=1.3,
        help="Seconds to wait between every request attempt (default: 1.3).",
    )
    parser.add_argument(
        "--limit",
        type=int,
        help="Optional cap on selected commanders for smoke runs.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Resolve and print the commander/slug/cell plan, make zero network calls, then exit.",
    )
    parser.add_argument(
        "--user-agent",
        default=USER_AGENT,
        help=f"Override the default HTTP User-Agent string (default: {USER_AGENT}).",
    )
    args = parser.parse_args()
    if args.min_decks < 0:
        raise SystemExit("--min-decks must be >= 0.")
    if args.throttle < 0:
        raise SystemExit("--throttle must be >= 0.")
    if args.limit is not None and args.limit <= 0:
        raise SystemExit("--limit must be > 0 when provided.")
    args.brackets = parse_brackets(args.brackets)
    return args


def parse_brackets(value: str) -> list[str]:
    seen: dict[str, None] = {}
    for raw in value.split(","):
        bracket = raw.strip().lower()
        if not bracket:
            continue
        if bracket not in BRACKET_INDEX:
            raise SystemExit(
                f"Unknown bracket '{raw}'. Expected one of: {', '.join(DEFAULT_BRACKETS)}."
            )
        seen.setdefault(bracket, None)
    brackets = list(seen)
    if not brackets:
        raise SystemExit("--brackets must name at least one bracket.")
    return brackets


def load_selected_commanders(
    averages_path: Path,
    min_decks: int,
    limit: int | None,
) -> list[dict[str, str]]:
    if not averages_path.exists():
        raise SystemExit(f"Missing averages CSV: {averages_path}")

    selected: list[dict[str, str]] = []
    try:
        with averages_path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            for row in reader:
                commander = (row.get("commander") or "").strip()
                commander2 = (row.get("commander2") or "").strip()
                number_decks = parse_int(row.get("number_decks"))
                if not commander or commander2 or number_decks < min_decks:
                    continue
                selected.append(
                    {
                        "commander": commander,
                        "slug": slugify(commander),
                        "oracle_id": (row.get("oracle_id") or "").strip() or None,
                    }
                )
                if limit is not None and len(selected) >= limit:
                    break
    except OSError as exc:
        raise SystemExit(f"Could not read averages CSV {averages_path}: {exc}") from exc
    except csv.Error as exc:
        raise SystemExit(f"Could not parse averages CSV {averages_path}: {exc}") from exc

    return selected


def build_cells(commanders: list[dict[str, str]], brackets: list[str]) -> list[dict[str, object]]:
    cells: list[dict[str, object]] = []
    for commander in commanders:
        for bracket in brackets:
            cells.append(
                {
                    "commander": commander["commander"],
                    "slug": commander["slug"],
                    "oracle_id": commander["oracle_id"],
                    "bracket": bracket,
                    "bracket_index": BRACKET_INDEX[bracket],
                }
            )
    return cells


def print_dry_run(
    averages_path: Path,
    commanders: list[dict[str, str]],
    brackets: list[str],
    cells: list[dict[str, object]],
) -> None:
    print(f"averages={averages_path}")
    print(f"commanders_selected={len(commanders)}")
    print(f"brackets={','.join(brackets)}")
    print(f"total_requests={len(cells)}")
    for commander in commanders:
        print(
            f"{commander['commander']} -> {commander['slug']} "
            f"[{','.join(brackets)}]"
        )


def fetch_cell(
    cell: dict[str, object],
    cell_path: Path,
    user_agent: str,
    gate: "RequestGate",
) -> dict[str, object]:
    url = cell_url(cell["slug"], cell["bracket"])
    attempts = 0
    for attempt in range(1, MAX_TRANSIENT_ATTEMPTS + 1):
        attempts += 1
        gate.wait_before_request()
        print(f"fetch {cell['slug']} [{cell['bracket']}] attempt {attempt}", file=sys.stderr)
        try:
            payload = read_json_url(url, user_agent)
        except urllib.error.HTTPError as exc:
            if exc.code == 404:
                print(f"missing {cell['slug']} [{cell['bracket']}] 404", file=sys.stderr)
                return {"status": "missing", "detail": "HTTP 404", "request_attempts": attempts}

            if exc.code == 429:
                if attempt >= MAX_TRANSIENT_ATTEMPTS:
                    detail = http_error_detail(exc)
                    print(
                        f"failed {cell['slug']} [{cell['bracket']}] after 429 retries: {detail}",
                        file=sys.stderr,
                    )
                    return {"status": "failed", "detail": detail, "request_attempts": attempts}
                wait_seconds = retry_after_seconds(exc.headers.get("Retry-After"), attempt)
                print(
                    f"429 {cell['slug']} [{cell['bracket']}]; wait {wait_seconds:.1f}s then retry",
                    file=sys.stderr,
                )
                time.sleep(wait_seconds)
                continue

            if 500 <= exc.code <= 599:
                if attempt >= MAX_TRANSIENT_ATTEMPTS:
                    detail = http_error_detail(exc)
                    print(
                        f"failed {cell['slug']} [{cell['bracket']}] after {attempt} attempts: {detail}",
                        file=sys.stderr,
                    )
                    return {"status": "failed", "detail": detail, "request_attempts": attempts}
                wait_seconds = linear_backoff_seconds(attempt)
                print(
                    f"{exc.code} {cell['slug']} [{cell['bracket']}]; wait {wait_seconds:.1f}s then retry",
                    file=sys.stderr,
                )
                time.sleep(wait_seconds)
                continue

            detail = http_error_detail(exc)
            print(f"failed {cell['slug']} [{cell['bracket']}] {detail}", file=sys.stderr)
            return {"status": "failed", "detail": detail, "request_attempts": attempts}
        except urllib.error.URLError as exc:
            if attempt >= MAX_TRANSIENT_ATTEMPTS:
                detail = f"URLError: {exc.reason}"
                print(f"failed {cell['slug']} [{cell['bracket']}] {detail}", file=sys.stderr)
                return {"status": "failed", "detail": detail, "request_attempts": attempts}
            wait_seconds = linear_backoff_seconds(attempt)
            print(
                f"network error {cell['slug']} [{cell['bracket']}]; wait {wait_seconds:.1f}s then retry: {exc.reason}",
                file=sys.stderr,
            )
            time.sleep(wait_seconds)
            continue

        record = trim_payload(cell, payload)
        write_json(cell_path, record)
        print(f"wrote {cell_path}", file=sys.stderr)
        return {"status": "written", "detail": None, "request_attempts": attempts}

    return {
        "status": "failed",
        "detail": "Retry loop exhausted unexpectedly.",
        "request_attempts": attempts,
    }


def read_json_url(url: str, user_agent: str) -> dict[str, object]:
    request = urllib.request.Request(
        url,
        headers={
            "Accept": "application/json",
            "User-Agent": user_agent,
        },
        method="GET",
    )
    with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
        return json.loads(response.read().decode("utf-8"))


def trim_payload(cell: dict[str, object], payload: dict[str, object]) -> dict[str, object]:
    bracket_index = str(cell["bracket_index"])
    bracket_counts = payload.get("bracket_counts")
    card = dig(payload, "container", "json_dict", "card")
    commander_card = {
        key: card.get(key) if isinstance(card, dict) else None
        for key in COMMANDER_CARD_KEYS
    }
    return {
        "commander": cell["commander"],
        "slug": cell["slug"],
        "bracket": cell["bracket"],
        "bracket_index": cell["bracket_index"],
        "n_decks": bracket_counts.get(bracket_index) if isinstance(bracket_counts, dict) else None,
        "deck": payload.get("deck") if isinstance(payload.get("deck"), list) else [],
        "land": payload.get("land"),
        "basic": payload.get("basic"),
        "nonbasic": payload.get("nonbasic"),
        "creature": payload.get("creature"),
        "instant": payload.get("instant"),
        "sorcery": payload.get("sorcery"),
        "artifact": payload.get("artifact"),
        "enchantment": payload.get("enchantment"),
        "battle": payload.get("battle"),
        "planeswalker": payload.get("planeswalker"),
        "mana_curve": normalize_mapping(dig(payload, "panels", "mana_curve")),
        "piechart": normalize_list(dig(payload, "panels", "piechart", "content")),
        "tag_counts": normalize_mapping(payload.get("tag_counts")),
        "budget_counts": normalize_mapping(payload.get("budget_counts")),
        "similar": normalize_list(payload.get("similar")),
        "commander_card": commander_card,
        "savedate_summary": summarize_savedates(payload.get("savedate_counts")),
        "fetched_utc": utc_now(),
    }


def summarize_savedates(value: object) -> dict[str, object]:
    if not isinstance(value, dict) or not value:
        return {
            "min_date": None,
            "max_date": None,
            "total": 0,
            "distinct_days": 0,
        }

    dates = sorted(str(key) for key in value)
    total = 0
    for count in value.values():
        total += parse_int(count)
    return {
        "min_date": dates[0],
        "max_date": dates[-1],
        "total": total,
        "distinct_days": len(value),
    }


def normalize_mapping(value: object) -> dict[str, object]:
    return value if isinstance(value, dict) else {}


def normalize_list(value: object) -> list[object]:
    return value if isinstance(value, list) else []


def dig(value: object, *keys: str) -> object:
    current = value
    for key in keys:
        if not isinstance(current, dict):
            return None
        current = current.get(key)
    return current


def cell_url(slug: object, bracket: object) -> str:
    return f"https://json.edhrec.com/pages/average-decks/{slug}/{bracket}.json"


def cell_output_path(outdir: Path, slug: object, bracket: object) -> Path:
    return outdir / "cells" / f"{slug}__{bracket}.json"


def is_valid_json(path: Path) -> bool:
    if not path.is_file():
        return False
    try:
        with path.open("r", encoding="utf-8") as handle:
            json.load(handle)
        return True
    except (OSError, json.JSONDecodeError):
        return False


def unresolved_commanders(
    commanders: list[dict[str, str]],
    statuses: dict[str, list[str]],
    expected_cells_per_commander: int,
) -> list[dict[str, str]]:
    unresolved: list[dict[str, str]] = []
    for commander in commanders:
        commander_statuses = statuses[commander["slug"]]
        if len(commander_statuses) != expected_cells_per_commander:
            continue
        if all(status == "404" for status in commander_statuses):
            unresolved.append(
                {
                    "commander": commander["commander"],
                    "slug": commander["slug"],
                }
            )
    return unresolved


def write_unresolved(path: Path, unresolved: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        for item in unresolved:
            handle.write(f"{item['commander']}\t{item['slug']}\n")


def file_metadata(path: Path) -> tuple[int, str]:
    digest = hashlib.sha256()
    total = 0
    try:
        with path.open("rb") as handle:
            while True:
                chunk = handle.read(1024 * 1024)
                if not chunk:
                    break
                total += len(chunk)
                digest.update(chunk)
    except OSError as exc:
        raise SystemExit(f"Could not read averages CSV {path}: {exc}") from exc
    return total, digest.hexdigest()


def write_json(path: Path, data: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(data, handle, ensure_ascii=False, indent=2, sort_keys=isinstance(data, dict))
        handle.write("\n")


def retry_after_seconds(header_value: str | None, attempt: int) -> float:
    if header_value:
        parsed = parse_retry_after(header_value)
        if parsed is not None:
            return min(MAX_BACKOFF_SECONDS, parsed)
    return linear_backoff_seconds(attempt)


def parse_retry_after(value: str) -> float | None:
    stripped = value.strip()
    if not stripped:
        return None
    if stripped.isdigit():
        return float(stripped)
    try:
        retry_time = parsedate_to_datetime(stripped)
    except (TypeError, ValueError, IndexError, OverflowError):
        return None
    if retry_time.tzinfo is None:
        retry_time = retry_time.replace(tzinfo=dt.timezone.utc)
    delta = (retry_time - dt.datetime.now(dt.timezone.utc)).total_seconds()
    return max(0.0, delta)


def linear_backoff_seconds(attempt: int) -> float:
    return float(min(MAX_BACKOFF_SECONDS, 60 * attempt))


def http_error_detail(exc: urllib.error.HTTPError) -> str:
    return f"HTTP {exc.code}: {exc.reason}"


def cell_audit_record(cell: dict[str, object], detail: object) -> dict[str, object]:
    return {
        "commander": cell["commander"],
        "slug": cell["slug"],
        "bracket": cell["bracket"],
        "bracket_index": cell["bracket_index"],
        "detail": detail,
    }


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def parse_int(value: object) -> int:
    try:
        return int(str(value).strip())
    except (TypeError, ValueError):
        return 0


def slugify(name: str) -> str:
    normalized = unicodedata.normalize("NFKD", name)
    stripped = "".join(ch for ch in normalized if not unicodedata.combining(ch))
    stripped = stripped.lower()
    stripped = re.sub(r"[,'’‘.`]", "", stripped)
    stripped = re.sub(r"[^a-z0-9]+", " ", stripped)
    return re.sub(r"\s+", "-", stripped.strip())


class RequestGate:
    def __init__(self, throttle_seconds: float) -> None:
        self._throttle_seconds = throttle_seconds
        self._last_request_started: float | None = None

    def wait_before_request(self) -> None:
        if self._last_request_started is not None:
            elapsed = time.monotonic() - self._last_request_started
            remaining = self._throttle_seconds - elapsed
            if remaining > 0:
                time.sleep(remaining)
        # Why: record the start time before issuing the request so every retry path
        # also pays the global inter-request throttle.
        self._last_request_started = time.monotonic()


if __name__ == "__main__":
    raise SystemExit(main())
