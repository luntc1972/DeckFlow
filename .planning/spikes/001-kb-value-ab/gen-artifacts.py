#!/usr/bin/env python3
"""Reconstruct Content KB artifact .md files + rows.json from uat-content-kb.db for the spike."""
import sqlite3, json, os, re

INDEX_DB = "artifacts/content-site-index.db"
CONTENT_DB = "artifacts/uat-content-kb.db"
ART_ROOT = "artifacts"

def secs_to_label(s):
    s = int(s or 0)
    h, rem = divmod(s, 3600)
    m, sec = divmod(rem, 60)
    return f"{h:02d}:{m:02d}:{sec:02d}" if h else f"{m:02d}:{sec:02d}"

def y(s):
    return '"' + (s or "").replace("\\", "\\\\").replace('"', '\\"') + '"'

c = sqlite3.connect(DB)
c.row_factory = sqlite3.Row
rows = c.execute("select * from content_site_index where is_visible=1").fetchall()
print(f"visible index rows: {len(rows)}")

# map youtube_video_id -> content_videos.id (for clips/summaries)
vid_map = {r["youtube_video_id"]: r["id"] for r in c.execute("select id, youtube_video_id from content_videos")}

out_rows = []
written = 0
skipped_existing = 0
no_clips = 0
for r in rows:
    yt = r["natural_key_value"]
    ap = r["artifact_path"]
    full = os.path.join(ART_ROOT, ap)
    # collect summary + clips from DB
    cv_id = vid_map.get(yt)
    summary = ""
    clips = []
    if cv_id is not None:
        sm = c.execute("select body from content_summaries where video_id=? order by id desc limit 1", (cv_id,)).fetchone()
        summary = sm["body"] if sm else ""
        clips = c.execute("select timestamp_s, excerpt from content_clips where video_id=? order by sort_order", (cv_id,)).fetchall()
    arche = json.loads(r["archetype_tags"] or "[]")
    brk = json.loads(r["bracket_tags"] or "[]")
    cardcat = json.loads(r["card_category_tags"] or "[]")
    # build row json for the C# store
    out_rows.append({
        "Id": r["id"],
        "Source": r["source"],
        "Title": r["title"],
        "VideoUrl": r["video_url"],
        "ArtifactPath": ap,
        "PublishedUtc": r["published_utc"],
        "IndexedUtc": r["indexed_utc"] or "2026-06-09T00:00:00Z",
        "IsVisible": True,
        "IsEvergreen": bool(r["is_evergreen"]),
        "ArchetypeTags": arche,
        "BracketTags": brk,
        "CardCategoryTags": cardcat,
        "YoutubeVideoId": yt,
        "RssGuid": None,
    })
    # don't overwrite the 10 real committed artifacts
    if os.path.exists(full):
        skipped_existing += 1
        continue
    if not clips:
        no_clips += 1
    os.makedirs(os.path.dirname(full), exist_ok=True)
    lines = []
    lines.append("---")
    lines.append(f"source: {y(r['source'])}")
    lines.append(f"title: {y(r['title'])}")
    lines.append(f"url: {y(r['video_url'])}")
    lines.append(f"video_id: {y(yt)}")
    lines.append("tags:")
    lines.append(f"  archetype: {json.dumps(arche)}")
    lines.append(f"  bracket: {json.dumps(brk)}")
    lines.append(f"  card_category: {json.dumps(cardcat)}")
    lines.append(f"generated_utc: {y(r['indexed_utc'] or '2026-06-09T00:00:00Z')}")
    lines.append("---")
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(summary)
    lines.append("")
    lines.append("## Key Clips")
    lines.append("")
    for cl in clips:
        ts = secs_to_label(cl["timestamp_s"])
        ex = (cl["excerpt"] or "").replace("\n", " ").strip()
        lines.append(f"- **[{ts}]** {ex}")
    lines.append("")
    lines.append("## Tags")
    lines.append("")
    lines.append(f"**Archetypes/Strategy:** {', '.join(arche)}")
    lines.append(f"**Format/Bracket:** {', '.join(brk)}")
    lines.append(f"**Card Categories:** {', '.join(cardcat)}")
    with open(full, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    written += 1

with open("/tmp/rows.json", "w", encoding="utf-8") as f:
    json.dump(out_rows, f)
print(f"wrote {written} artifacts | skipped {skipped_existing} existing | {no_clips} had no clips | rows.json={len(out_rows)}")
