# Encoding an MTG Creator's Deck-Building Style into an LLM System: Prompt-Generator vs. Tool-Using Agent

*A cited research report for a lightweight ASP.NET 10 + Razor Commander/cEDH deck-analysis app (512 MB web tier, Render Postgres). Researched by Fable 5, 2026-07-04.*

## Executive summary

The best-supported design for your app is **not** an autonomous agent. It is a **deterministic prompt-artifact pipeline backed by a small, precomputed style profile and hybrid retrieval** — with a *narrow* server-side "critique" call as an optional second tier. This follows directly from three converging authorities: Anthropic tells builders to "find the simplest solution possible, and only increas[e] complexity when needed," noting that for many applications "optimizing single LLM calls with retrieval and in-context examples is usually enough" [1]; OpenAI says to build an agent only when you have genuinely complex decision-making, unmaintainable rule sprawl, or heavy unstructured-data dependence, and "otherwise, a deterministic solution may suffice" [2]; and Anthropic's retrieval guidance notes that a corpus under ~200k tokens can simply be placed in the prompt "with no need for RAG or similar methods" [8]. Your creator style profile and per-commander synergy digest fit inside that budget.

The style profile itself should be built by **fusing distilled stated rules with measured decklist statistics, and explicitly resolving say-vs-do conflicts in favor of demonstrated behavior** — because the stated-vs-revealed gap is robust in humans [47] and measured in LLM personas [48][49], and because when retrieved rules conflict with a model's priors, models bias toward their own parametric memory [50]. Card-name hallucination is the dominant failure mode and must be handled by a hard validation layer against Scryfall, not by prompting alone [54][55][63][64].

The rest of this report substantiates each technique with sources, then gives a tradeoff table and a concrete recommended architecture.

---

## 1. Distilling qualitative creator philosophy into measurable rules

**The good news: Commander already has codified, numeric templates you can anchor to.** The Command Zone's widely-used template prescribes explicit counts — the 2021 "new" version is commonly reported as **38 lands, 12 ramp, 12 card advantage, 12 targeted disruption, 6 mass disruption**, with the remainder as strategy cards [74][75]. Its earlier form was roughly **37 lands / 10 ramp / 10 card draw / 5 targeted removal / 5 board wipes**, so the notable drift across versions is that **targeted removal nearly doubled (5→12)** [75][76]. A 2025 "New Era" revision (ep. 658) keeps ~38 lands and formalizes that a single card can **count in multiple categories** [77] — a subtlety your tagger must mirror. Parallel codifications exist: **8×8 theory** (commander + 35 lands + 8 effect-types × 8 cards = 64 spells, explicitly "an initial jumping-off point," not rules) [84]; **Quadrant Theory** (evaluate each card across Developing / Parity / Winning / Losing game-states — qualitative, "no formalized mathematical rules") [85]; and Wizards' own **Commander Brackets** power framework, which quantifies deck power via measurable ingredients (count of "Game Changers," presence of 2-card infinite combos, mass land denial) across five brackets [86][87].

**Frank Karsten's mana math gives you objective, testable consistency targets.** His colored-source framework targets ~90% castability-on-curve and prescribes exact source counts (e.g., a single-pip turn-1 spell wants ~14 sources; CCC wants ~23), with a 2022 update adding 99-card Commander tables that *reduce* requirements by ~3–4 sources for 1–2 mana spells to account for Commander's free mulligan and command-zone access [78][79]. His land-count regression (from 95,143 tournament decks) yields `lands ≈ 31.42 + 3.13×(avg MV) − 0.28×(cheap draw/ramp)` for 99-card decks, landing near 40–41 lands at avg MV 3 [80]. These are ideal because they are *falsifiable* — you can score any submitted deck against them.

**Recommended distillation pipeline (transcripts → structured rules).** The mature engineering pattern is:
1. **Map-reduce / hierarchical chunking** over the transcript corpus — process chunks in parallel, then combine — which is faster than sequential refinement and is the standard for over-context documents [35]. Book-length summarization research (BooookScore) confirms hierarchical merging vs. incremental updating as the two viable strategies and catalogs the coherence errors to guard against [36].
2. **Claimify-style Select → Disambiguate → Decompose**: keep only verifiable statements, drop irreducibly ambiguous ones, and rewrite each into a standalone atomic "rule." Microsoft's version achieves 99% of extracted claims entailed by their source sentence [37] — this maps almost one-to-one onto "turn a rambling deck-tech video into discrete, attributable style rules."
3. **Schema-guaranteed structured output**: emit each rule under a strict JSON schema via constrained decoding (OpenAI Structured Outputs [6] or Anthropic strict tool use [34]), so every rule carries fields like `{category, target_metric, target_value, comparator, source_clip, confidence}`. Constrained decoding "guarantee[s] schema-compliant responses" [34], eliminating a class of downstream parsing errors.

The output is a machine-readable **stated-rules ledger**, each rule tied back to a transcript clip (which your existing distill pipeline already produces).

---

## 2. Inferring style from decklist data (feature extraction, clustering, tagging, EDHREC-style stats)

**Feature extraction.** Encode each of the creator's decks as a vector. The Hearthstone data-mining literature is the closest precedent: 500,000+ decks encoded as card-count vectors (0/1/2), then K-Means to cluster decks into archetypes and agglomerative clustering on the *transposed* matrix to cluster cards — run per class because card pools don't overlap [20]. For MTG specifically, `magic_deck_classification_multi_dir` treats "card frequencies like allele frequencies" and hits 86% archetype classification, noting accuracy is best for archetypes sharing few cards with others [24]. The cEDH-specific `KonradHoeffner/cedh` repo agglomeratively clusters binary card vectors from the cEDH Decklist Database — and, critically, **strips common lands and mana rocks before clustering**, because staples otherwise dominate the distance metric [25]. That preprocessing choice is the single most important lesson for style inference: *ubiquitous staples carry no style signal and actively drown it out.*

**Beyond counts, engineer the style-bearing features** documented in prior art: mana-curve shape/smoothness, color-pip ratios, card-type ratios, keyword/mechanic synergy counts, commander-popularity, and deck-similarity [30]. For richer semantics, **card2vec** trains word2vec over decks-as-documents and recovers meaningful structure (e.g., red-removal − Mountain + Swamp ≈ black-removal), using only names and co-occurrence [21]; a peer-reviewed generalization learns card representations that transfer to *unseen* cards (55% draft-pick accuracy on brand-new cards) [22].

**EDHREC-style statistics — and their exact definitions.** EDHREC's classic **synergy score = (% of this commander's decks containing the card) − (% of same-color-identity decks containing it)**, ranging −100% to +100% [18]. EDHREC is now migrating to a **lift** metric — `Pr(A∩B) / [Pr(A)·Pr(B)]` on a log display scale — *specifically because* ubiquitous staples (Sol Ring, Command Tower, in >50% of decks) produced misleading strong-*negative* synergy with almost everything [19]. Their data comes from Archidekt, Moxfield, and Scryfall, refreshed ~daily, with illegal decks excluded [18]. For association-style co-occurrence, this is the same family of technique as rule-mining used to predict deck contents in Netrunner [23]. **Note EDHREC uses collaborative filtering, not LLMs** — v1 user-based CF, v2 an offline Jaccard/Tanimoto card-affinity matrix [69] — a useful reminder that the *statistical* half of your style profile needs no model at all.

**Category tagging (ramp / removal / draw / wincon).** Two canonical sources:
- **Scryfall Tagger** provides crowd-sourced *oracle tags* that "describe the functional role of a card, such as removal, ramp, or draw," exposed via a Tags API and daily bulk files; tags have stable UUIDs, a parent/child hierarchy (traverse children, since parent tags have no direct taggings), and per-tagging weights (very_strong…weak) [26]. Scryfall warns it "cannot guarantee that tag data is 100% free from intentional errors or abuse" and recommends a way to disable individual tags [26].
- **Scryfall bulk data** is your canonical card database — daily JSONL exports (Oracle Cards ~171 MB), with the guidance that gameplay data changes slowly ("once per week or right after set releases would most likely be sufficient") [27].
- **Commander Spellbook** is the open (MIT) combo catalog powering EDHREC's combo feature; its `find-my-combos` endpoint accepts a card list and returns combos present/near-missing — ideal for measuring **combo density** [28][29]. (Your codebase already calls its backend.)

**Output: a measured-style profile** — per-creator distributions for land/ramp/draw/removal/wipe counts, avg MV, pip balance, wincon type, combo density, and characteristic high-synergy cards.

---

## 3. Fusing qualitative + quantitative signals — and resolving say-vs-do conflicts

This is the intellectual core of the system, and the literature is unusually clear about the hazard.

**The say-vs-do gap is real and must be designed for.** The value-action / attitude-behavior gap is pervasive in humans — e.g., 54% of Americans call environmental protection a priority while eco-labeled goods hold <1% market share [47]. In LLMs specifically, ValueActionLens finds "the alignment between LLMs' stated values and actions is sub-optimal, varying significantly across scenarios and models" [48], and a 2026 study shows the *measured* stated-vs-revealed gap is highly sensitive to elicitation protocol, and that "steering system prompts with stated preferences did not reliably strengthen alignment" [49]. Translation: a creator's *stated* template is an unreliable predictor of their *built* decks, and simply pasting stated rules into a system prompt will not reliably make the model behave consistently with them.

**Resolution principle: prefer demonstrated behavior; treat stated rules as priors, not ground truth.** Concretely:
- Compute both a **stated profile** (§1) and a **measured profile** (§2).
- For each metric, **flag conflicts** where measured behavior falls outside the stated rule's band by a threshold (e.g., creator *says* "12 removal" but their decks average 7). Surface the conflict in the profile with both numbers.
- **Weight toward measured** for anything empirically observable (counts, curve, ratios), and toward **stated** only for un-measurable philosophy (e.g., "I value resilience over speed"). This mirrors the guidance from digital-twin research that twins should be built from behavioral corpora, and that RAG over the person's actual materials is the standard retrieval method [46].
- Because **retrieved context that conflicts with the model's priors tends to lose** — models bias toward parametric memory, popular entities, and majority-of-documents answers [50] — encode the fused profile as **explicit, weighted numeric targets**, not prose the model can quietly override.

**Style capture from the corpus: use exemplars + RAG, not fine-tuning.** Few-shot exemplars deliver large style gains cheaply — one 2025 study found up to 23.5× higher style-matching from few-shot vs zero-shot, and that "prompting strategy matters more than model size" [43] — but they **plateau on subtle, informal voices** (95–97% authorship-match on formal text collapses to 19–66% on blogs/forums, and 2→10 examples barely helps) [44]. Fine-tuning to a specific voice from <10 demonstrations (DITTO) is the strongest low-data alternative and beats few-shot GPT-4 [45], and OpenAI's SFT guidance says ~50–100 curated examples is the practical sweet spot [73] — **but** persona fine-tunes on tiny sets amplify **caricature** (see §5). For your app, the recommendation is **exemplar-augmented prompting + retrieved style profile**, reserving fine-tuning as a later experiment only if voice fidelity proves insufficient.

---

## 4. Prompt-template engineering vs. agent architectures for deck analysis

**Definitions (Anthropic):** *workflows* orchestrate LLMs and tools "through predefined code paths"; *agents* "dynamically direct their own processes and tool usage" [1]. Routing (LLM classifies, then a fixed handler runs) is a *workflow* pattern, not an agent [3].

**Why an agent is the wrong default here.** The costs are documented with numbers:
- **Token/cost multiplication:** agents use ~4× the tokens of a chat interaction, and multi-agent systems ~15× [4]. Your per-analysis call is roughly 6k in / 1.5k out ≈ **$0.0018 on gpt-4o-mini** or ~$0.0135 on Claude Haiku 4.5 [16][17]; an agentic version multiplies that, and per-step error compounding multiplies retries.
- **Compounding error:** end-to-end success is p^n across steps — even at 95%/step, 10 steps ≈ 59% success, 20 steps ≈ 35% [5]. Deck critique is decomposable into a *fixed* sequence (fetch → tag → compute stats → retrieve → prompt), so there is no unpredictability for an agent to earn its keep against.
- **Debuggability:** agents are "non-deterministic between runs, even with identical prompts," and one bad step "can cause agents to explore entirely different trajectories" [4] — bad for a single-server app you must support.
- **Memory:** an in-process vector store is a real RAM risk. 1536-dim float32 vectors for ~20k card/synergy texts ≈ **118 MB** of raw vectors — nearly a quarter of your 512 MB tier before index overhead. Keeping vectors in Postgres (server-side) means the app ships only a query vector per request.

**When tool use *is* warranted — narrowly.** Function-calling with **Structured Outputs** guarantees schema adherence so the model can't "hallucinat[e] an invalid enum value" [6], and **forced tool choice** (`tool_choice`) plus strict schemas let you validate `tool_use.input` exactly [1]. The right use of tool-calling here is **RAG-as-a-tool and card-validation-as-a-tool**, invoked by deterministic code — not an open-ended agent loop. MCP is the emerging standard for exposing such tools, but it is optional for a single app [7].

**Lightweight RAG for .NET on your stack.**
- Anthropic's key simplification: a **corpus under ~200k tokens can go straight in the prompt, no RAG** [8]. Your fused style profile + a single commander's synergy digest easily fit — meaning your **primary path may need no vector DB at all**.
- If you do retrieve, **pgvector on your existing Render Postgres** is the zero-new-infrastructure choice: Render's managed Postgres supports it (`CREATE EXTENSION vector;`) [11], and .NET has first-party plumbing via `Microsoft.Extensions.AI` + `Microsoft.Extensions.VectorData` (GA May 2025) [9] and a preview `SemanticKernel.Connectors.PgVector` [10]. Vectors live in the DB, not the 512 MB web process.
- **Hybrid / keyword search is not optional for a card domain.** Embedding-only retrieval "can miss crucial exact matches" like identifiers and error codes — exactly analogous to exact card names — and Anthropic found adding BM25 cut retrieval failures by 49% [8]. You already ship SQLite (FTS5 gives **BM25 by default**, zero new deps [14]) and Postgres full-text search is built into core [15]. Use keyword/BM25 for exact card-name grounding and reserve embeddings for fuzzy "cards *like* this."
- **Embeddings are effectively free at this scale:** `text-embedding-3-small` is $0.02 / 1M tokens, 1536 dims, shortenable via the `dimensions` param to cut memory 3–6× without losing concept structure [13].

**The pattern you already have is the cheapest of all.** Your existing "user pastes a prompt artifact into ChatGPT" flow costs the operator **$0 in API spend** — inference is paid by the user's own subscription. Any server-side agent/RAG feature is the *first* recurring per-request cost line the app takes on. All the workflow-first guidance [1][2][8] points the same way.

### Tradeoff table: Prompt-generator vs. RAG-workflow vs. autonomous agent

| Dimension | **Prompt-artifact generator** (deterministic assembly, user pastes into ChatGPT) | **Server-side RAG workflow** (fixed pipeline: fetch→tag→stats→retrieve→1–2 LLM calls) | **Autonomous tool-using agent** (LLM plans its own tool calls in a loop) |
|---|---|---|---|
| Operator API cost | **$0** (user's inference) | Low, bounded: ~$0.002–0.014/analysis [16][17] | High & variable: ~4× tokens [4]; retries compound |
| Latency | Instant (no server LLM call) | Predictable (1–2 calls) | Unpredictable (multi-turn) [4] |
| Reliability / determinism | Highest (pure code) | High (predefined path) [1][3] | Lowest; "non-deterministic between runs" [4] |
| Error behavior | No model errors server-side | Bounded; validate each stage | Compounds: p^n over steps [5] |
| 512 MB RAM fit | Trivial | Good if vectors in Postgres [10][11] | Risky (loop state, larger context) |
| Debuggability | Trivial | Straightforward | Hard [4] |
| Handles card hallucination | Push responsibility to user's ChatGPT (risky) | **Can hard-validate** cards server-side pre-artifact [63][64] | Model may re-introduce bad cards each turn |
| Style fidelity | Good (profile baked into prompt) | Good + retrieval of exemplars | Marginal gain, not worth cost |
| Best when | The one-round-trip paste workflow (your core value) | You want an in-app critique with citations | Genuinely open-ended, unpredictable tasks [2] — **not this** |

---

## 5. Pitfalls & mitigations

**Hallucinated card names and rules — the #1 failure mode.** Firsthand reports show ChatGPT (including premium/o1) generating Commander decks with "non-existent cards, bad syntax, and wrong number of cards" [54]; suggesting *illegal duplicate* singleton cards and misfiling card types [57]; and building decks with uncastable centerpieces (a card with no matching mana sources) [64]. Google's AI Overview gave backwards MTG rulings, prompting "never listen to [it]…for Magic" [55]. Benchmarks confirm the ceiling: ManaBench frontier models hit ~65–71% on deck-completion (vs 68% human) and deliberately supply full rules text to avoid trusting parametric card knowledge [59]; zero-shot GPT-4o drafts at only 43% [60]; fine-tuning on 80k MTG QA pairs improved a model only 10.5% [62].
*Mitigations, in priority order:*
1. **Validate every card name against Scryfall** — the `/cards/named?fuzzy=` endpoint returns exactly one card when unambiguous, a 404 `ambiguous` when many match, and "No cards found" when none — a ready-made validator (verified live) [63]. Reject or auto-correct before any artifact ships.
2. **Constrained selection ("recommend only from this provided, validated list")** — DeepMTG eliminated illegal/uncastable suggestions by having the LLM select from a restricted legal card DB rather than free-generate [64]. Production tools (mtg-agents, mtg-judge) center this: "every card suggestion is a real, playable card," grounded in Scryfall + rules [67][68].
3. **Grounding lifts accuracy a lot** — a retrieval-grounded MTG rules setup hit ~90% vs 65% prompt-only (self-reported, n=45) [61] — **but RAG reduces, not eliminates, hallucination**: even commercial legal-RAG tools hallucinate 17–33% [65][66]. Keep the deterministic validator as the backstop.

**Style overfitting / caricature (small-sample creators).** LLM persona simulations are measurably prone to "flattened caricatures…failing to capture the multidimensionality" of the target, and are "highly susceptible to caricature" on certain topics [72]. Persona prompts also **drift within ~8 conversational turns** as attention to the system prompt decays [42]. And note the sobering baseline: adding personas to system prompts did **not improve task accuracy** across 162 personas [38] — personas change *style*, not correctness. *Mitigations:* keep the persona as **weighted numeric targets + a few real exemplars** rather than adjectives; cap the persona's influence on factual claims; re-anchor the profile mid-conversation to fight drift; and set a **minimum-sample floor** per creator (EDHREC uses a ≥5-deck support floor for card-pair stats [19]; treat single-digit deck counts as "insufficient to infer style" and fall back to stated rules only).

**Corpus bias.** The "**Precon Effect**" is a named, EDHREC-documented bias: players import out-of-box precons, inflating those cards' stats, and re-feeding recommendations "reinforces the bias" [70]. Popular commanders attract more submissions, homogenizing recommendations. *Mitigations:* strip staples before clustering (as `KonradHoeffner/cedh` does [25]); use **lift, not raw synergy**, to demote ubiquitous cards [19]; de-duplicate near-precon lists; and weight the creator's *own* decks far above the global corpus when building their profile.

**Staleness.** Both AI-built decks in one experiment were "a bit outdated" from training cutoffs [58]; game-domain RAG staleness is now a formal research problem (ChronoPlay) because content updates and community focus both shift [71]. *Mitigation:* refresh Scryfall bulk weekly / on set release [27]; timestamp the synergy corpus; and never let the LLM's parametric card memory override the current Scryfall snapshot.

**Sample-size / skill bias in stats.** Even huge corpora have thin per-archetype slices (100k games for one color pair, 18k for another) [32], and skilled-user datasets shift the baseline (17Lands win-rate mode is 56%, not 50%) [32]. *Mitigation:* show confidence/`num_decks` alongside every stat and suppress low-support numbers.

**Rubric-based critique beats freeform.** For the actual deck critique, use an **explicit weighted rubric**, not open-ended prose: G-Eval shows criteria + chain-of-thought + form-filling scoring dramatically improves human alignment [51]; Prometheus shows **rubrics + reference answers let even a 13B model match GPT-4** (Pearson 0.897 vs 0.882) — evidence that *rubrics, not scale, drive agreement* [52]; HealthBench operationalizes this at 48k physician-written criteria [53]. Your fused style profile *is* the rubric.

---

## 6. Recommended architecture for DeckFlow

**Design stance:** deterministic **prompt-artifact generation first**, with an optional **single-shot server-side RAG critique** as a second tier. No autonomous agent. This preserves your core value ("output the user can paste into ChatGPT in one round-trip") while adding creator-style intelligence, and it fits 512 MB.

**A. Offline / batch (your existing distill + crawl pipelines):**
1. **Stated-rules ledger** — run the map-reduce → Claimify → structured-output pipeline [35][37][6] over the creator's transcripts (which your KB pipeline already distills), emitting schema-validated rules tied to clips.
2. **Measured-style profile** — for the creator's own decks: vectorize, strip staples [25], compute land/ramp/draw/removal/wipe counts, avg MV, pip balance, wincon type, combo density (Commander Spellbook `find-my-combos` [29]), and high-synergy cards; tag categories via Scryfall Tagger oracle tags [26].
3. **Fused profile + conflict ledger** — reconcile §1 and §2, flag say-vs-do conflicts, weight toward measured for observable metrics [48][49][50]. Store as compact JSON (well under the 200k-token in-prompt budget [8]).
4. **Synergy/category knowledge base** — mine your crawled deck history into EDHREC-style **lift** stats [19] per commander, stored in Postgres; keep card facts fresh from weekly Scryfall bulk [27].

**B. Online, per user-submission:**
1. **Fetch & normalize** the submitted deck (your existing Archidekt/Moxfield importers).
2. **Deterministic analysis in C#** (no LLM): compute the same metrics as the profile; diff against the fused profile's targets and Karsten's mana math [80]; pull relevant lift stats and combos from Postgres. This is your **rubric scoring** — pure code, cheap, deterministic.
3. **Grounding pass:** validate every card via Scryfall exact/fuzzy [63]; assemble a **whitelist** of legal, real candidate cards for any suggestions (constrained selection [64]).
4. **Retrieval:** hybrid — BM25/FTS5 for exact card lookups [14], optional pgvector for "cards like X" [10][11]. Only if the assembled context would exceed ~200k tokens do you need heavier RAG [8]; usually it won't.
5. **Artifact assembly (deterministic):** build the ChatGPT-ready prompt containing (a) the fused style profile as weighted numeric targets + 2–3 real creator-deck exemplars [43], (b) the deterministic rubric scores, (c) the validated synergy/combo context, and (d) a strict instruction to critique *only* using provided cards. This artifact is the primary deliverable — **$0 operator cost**, user pastes into ChatGPT.
6. **Optional Tier-2 in-app critique:** one server-side call to a small model (gpt-4o-mini ~$0.0018 [16], or Haiku 4.5 [17]) with Structured Outputs [6] returning a schema'd critique, each claim citing a rubric line and a real card. Re-validate card names in the response before display. No loop, no agent.

**Guardrails baked in:** persona expressed as weighted targets + exemplars (not adjectives) to fight caricature [72]; minimum-deck floor before a creator profile is trusted [19]; lift over raw synergy [19]; weekly Scryfall refresh [27]; confidence/`num_decks` shown next to every stat [32].

---

## Uncertainties and honest gaps

- **Command Zone template exact numbers** are corroborated across summarizers but no primary Command Zone artifact was fetched; secondary sources disagree on whether ramp is 10 or 12 and on ep. 379 vs 658 attribution [74][75][77]. Treat the counts as "≈" anchors, not gospel.
- **A published say-vs-do audit of a specific creator does not appear to exist** — six-plus targeted searches found none. The gap is inferred from the general value-action literature [47][48][49] plus the fact that Command Zone episode decks are public on Archidekt (so such an audit is *possible*, just unpublished). Your app could be the first to actually measure it.
- **Karsten's exact 99-card colored-source table cells** remain unverified (TCGplayer body is JS-rendered); the framework and land regression are solid [80].
- Vendor capability claims (mtg-agents, mtg-judge, ManaForge) are **marketing self-descriptions** [67][68] and their eval numbers are self-reported (n=45, LLM-as-judge) [61] — weigh accordingly.
- The "keyword search *equals* embeddings on small corpora" claim was **not** verifiable; the defensible positions are "small corpus → skip RAG" [8] and "exact-match domains need BM25 alongside embeddings" [8].

---

## References

[1] Anthropic, "Building Effective Agents." https://www.anthropic.com/engineering/building-effective-agents
[2] OpenAI, "A Practical Guide to Building Agents" (PDF). https://cdn.openai.com/business-guides-and-resources/a-practical-guide-to-building-agents.pdf
[3] LangChain/LangGraph, "Workflows and Agents." https://docs.langchain.com/oss/python/langgraph/workflows-agents
[4] Anthropic, "How we built our multi-agent research system." https://www.anthropic.com/engineering/multi-agent-research-system
[5] Lens, "The Math of AI Agent Compounding Errors." https://lenshq.io/blog/ai-agent-compounding-errors-math
[6] OpenAI, "Structured Outputs" guide. https://developers.openai.com/api/docs/guides/structured-outputs
[7] Anthropic, "Introducing the Model Context Protocol." https://www.anthropic.com/news/model-context-protocol
[8] Anthropic, "Introducing Contextual Retrieval." https://www.anthropic.com/news/contextual-retrieval
[9] Microsoft, "Microsoft.Extensions.AI + VectorData GA." https://devblogs.microsoft.com/dotnet/ai-vector-data-dotnet-extensions-ga/
[10] Microsoft Learn, "Postgres vector store connector (Semantic Kernel)." https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector
[11] Render, "PostgreSQL Extensions." https://render.com/docs/postgresql-extensions
[12] asg017, "sqlite-vec." https://github.com/asg017/sqlite-vec
[13] OpenAI, "text-embedding-3-small" / embeddings guide. https://developers.openai.com/api/docs/models/text-embedding-3-small
[14] SQLite, "FTS5." https://www.sqlite.org/fts5.html
[15] PostgreSQL, "Full Text Search." https://www.postgresql.org/docs/current/textsearch-intro.html
[16] OpenAI, "Pricing." https://developers.openai.com/api/docs/pricing
[17] Anthropic, "Pricing." https://platform.claude.com/docs/en/about-claude/pricing
[18] EDHREC, "FAQ." https://edhrec.com/faq
[19] EDHREC, "From Synergy to Lift: The Math Behind EDHREC's New Era." https://edhrec.com/articles/from-synergy-to-lift-the-math-behind-edhrecs-new-era
[20] García-Sánchez et al., "Data Mining of Deck Archetypes in Hearthstone" (CoSECivi 2020, CEUR Vol-2719). https://ceur-ws.org/Vol-2719/paper14.pdf
[21] afreefaw, "MTG-card2vec." https://github.com/afreefaw/MTG-card2vec
[22] Bertram, Fürnkranz, Müller, "Learning With Generalised Card Representations for Magic: The Gathering" (arXiv 2407.05879). https://arxiv.org/abs/2407.05879
[23] Sephton et al., "Using Association Rule Mining to Predict Opponent Deck Content in Android: Netrunner" (IEEE CIG 2016). https://eprints.whiterose.ac.uk/104807/1/2016_CIG_RuleMining.pdf
[24] g-tierney, "magic_deck_classification_multi_dir." https://github.com/g-tierney/magic_deck_classification_multi_dir
[25] KonradHoeffner, "cedh" (clustering). https://github.com/KonradHoeffner/cedh
[26] Scryfall, "Tags API." https://scryfall.com/docs/api/tags
[27] Scryfall, "Bulk Data." https://scryfall.com/docs/api/bulk-data
[28] Commander Spellbook, "About." https://commanderspellbook.com/about/
[29] SpaceCowMedia, "commander-spellbook-backend." https://github.com/SpaceCowMedia/commander-spellbook-backend
[30] sjlillian, "MTG_tournament_guesser." https://github.com/sjlillian/MTG_tournament_guesser
[31] 17Lands, "Using Win Rate Data." https://blog.17lands.com/posts/using-win-rate-data/
[32] Joel Nitta, "An Introduction to 17Lands Data." https://www.joelnitta.com/posts/2023-12-31_17lands-intro/
[34] Anthropic, "Structured Outputs." https://platform.claude.com/docs/en/build-with-claude/structured-outputs
[35] Google Cloud, "Long-document summarization with Workflows and Gemini." https://cloud.google.com/blog/products/ai-machine-learning/long-document-summarization-with-workflows-and-gemini-models
[36] Chang et al., "BooookScore" (ICLR 2024, arXiv 2310.00785). https://arxiv.org/abs/2310.00785
[37] Microsoft Research, "Claimify" (arXiv 2502.10855). https://www.microsoft.com/en-us/research/blog/claimify-extracting-high-quality-claims-from-language-model-outputs/
[38] Zheng et al., "When 'A Helpful Assistant' Is Not Really Helpful" (EMNLP 2024 Findings, arXiv 2311.10054). https://arxiv.org/abs/2311.10054
[39] Wang et al., "RoleLLM" (arXiv 2310.00746). https://arxiv.org/abs/2310.00746
[40] Shao et al., "Character-LLM" (arXiv 2310.10158). https://arxiv.org/abs/2310.10158
[41] Tu et al., "CharacterEval" (arXiv 2401.01275). https://arxiv.org/abs/2401.01275
[42] Li et al., "Measuring and Controlling Instruction (In)Stability in LM Dialogs" (arXiv 2402.10962). https://arxiv.org/abs/2402.10962
[43] "How Well Do LLMs Imitate Human Writing Style?" (arXiv 2509.24930). https://arxiv.org/abs/2509.24930
[44] "Catch Me If You Can? Not Yet" (EMNLP 2025 Findings, arXiv 2509.14543). https://arxiv.org/html/2509.14543v1
[45] Shaikh et al., "Show, Don't Tell: Aligning LMs with Demonstrated Feedback (DITTO)" (arXiv 2406.00888). https://arxiv.org/html/2406.00888v1
[46] Nielsen Norman Group, "Digital Twins" (of a person). https://www.nngroup.com/articles/digital-twins/
[47] Wikipedia, "Value-action gap." https://en.wikipedia.org/wiki/Value-action_gap
[48] "Mind the Value-Action Gap: Do LLMs Act in Alignment with Their Values?" (arXiv 2501.15463). https://arxiv.org/abs/2501.15463
[49] "Mind the Gap: How Elicitation Protocols Shape the Stated-Revealed Preference Gap" (arXiv 2601.21975). https://arxiv.org/abs/2601.21975
[50] Xu et al., "Knowledge Conflicts for LLMs: A Survey" (EMNLP 2024, arXiv 2403.08319). https://arxiv.org/abs/2403.08319
[51] Liu et al., "G-Eval" (EMNLP 2023, arXiv 2303.16634). https://arxiv.org/abs/2303.16634
[52] Kim et al., "Prometheus" (ICLR 2024, arXiv 2310.08491). https://arxiv.org/abs/2310.08491
[53] OpenAI, "HealthBench" (arXiv 2505.08775). https://arxiv.org/abs/2505.08775
[54] SlightlyMagic/Forge forum, "ChatGPT decks don't import (non-existent cards)." https://slightlymagic.net/forum/viewtopic.php?f=26&t=32392
[55] MTGRocks, "Players Shocked at Wrong Google AI MTG Rulings." https://mtgrocks.com/players-shocked-at-wrong-google-ai-mtg-rulings/
[56] Manifold Markets, "Will AI accurately answer MTG rules questions?" https://manifold.markets/IsaacKing/will-ai-be-able-to-accurately-answe
[57] Wargamer, "I made an AI Commander deck." https://www.wargamer.com/magic-the-gathering/ai-commander-deck
[58] AetherHub, "AI Showdown: ChatGPT vs Google Bard." https://aetherhub.com/Article/AI-Showdown-ChatGPT-vs-Google-Bard---The-Epic-MTG-Duel-Of-The-Machine-Minds
[59] Jake Boggs, "ManaBench: Evaluating LLM Reasoning with MTG Deck-Building." https://boggs.tech/posts/evaluating-llm-reasoning-with-mtg-deck-building/
[60] "UrzaGPT" (arXiv 2508.08382). https://arxiv.org/abs/2508.08382
[61] Krempl, "Evaluating a Multi-Agent System for MTG Rules Questions." https://medium.com/@fkrempl/evaluating-a-multi-agent-system-for-magic-the-gathering-rules-questions-d206044deef1
[62] Jake Boggs, "Large-Language-Models-for-Magic-the-Gathering." https://github.com/JakeBoggs/Large-Language-Models-for-Magic-the-Gathering
[63] Scryfall API, `/cards/named` (verified live). https://api.scryfall.com/cards/named
[64] Giles Strong, "Making Magic (DeepMTG)." https://gilesstrong.github.io/website/ai/llms/nlp/fun/2025/02/17/Making-Magic.html
[65] Magesh et al., "Hallucination-Free? Assessing Leading AI Legal Research Tools" (arXiv 2405.20362). https://arxiv.org/abs/2405.20362
[66] Huang et al., "A Survey on Hallucination in LLMs" (arXiv 2311.05232). https://arxiv.org/abs/2311.05232
[67] mtg-agents.com. https://mtg-agents.com/
[68] mtg-judge.com. https://mtg-judge.com/
[69] Donald Miner, "edhrec" (collaborative filtering). https://github.com/donaldpminer/edhrec
[70] EDHREC, "Intellectual Offering: Breya" (Precon Effect). https://edhrec.com/articles/intellectual-offering-1-breya-etherium-shaper
[71] "ChronoPlay: Dynamic Game RAG Benchmark" (arXiv 2510.18455). https://arxiv.org/abs/2510.18455
[72] Cheng et al., "CoMPosT: Characterizing Caricature in LLM Simulations" (EMNLP 2023, arXiv 2310.11501). https://arxiv.org/abs/2310.11501
[73] OpenAI, "Supervised Fine-Tuning" guide. https://developers.openai.com/api/docs/guides/supervised-fine-tuning
[74] The Command Zone #379, "The NEW Commander Deck Building Template." https://podcasts.apple.com/us/podcast/the-new-commander-deck-building-template-379/id898023861?i=1000511316766
[75] OlliTapio, "command_zone_template.md." https://github.com/OlliTapio/mtg-deck-optimizer/blob/main/command_zone_template.md
[76] CommanderDeckMaker, "Command Zone Template." https://commanderdeckmaker.com/learn/deckbuilding/command-zone-template
[77] EDHREC/Command Zone #658, "Commander Deckbuilding Template for the New Era." https://edhrec.com/articles/the-command-zone-commander-deckbuilding-template-for-the-new-era-the-command-zone-658-mtg-edh-magic-gathering
[78] ScrollVault, "How Many Lands / colored sources (Karsten)." https://scrollvault.net/guides/how-many-lands.html
[79] Thraben University, "The Math of Mana." https://www.thrabenuniversity.com/construction/the-math-of-mana/
[80] Karsten (Peasant Magic mirror), "How Many Lands Do You Need? An Updated Analysis." https://www.peasant-magic.com/articles/magic-deckbuilding/how-many-lands-do-you-need-in-your-deck-an-updated-analysis
[81] Commander's Herald, "A Beginner's Guide to cEDH." https://commandersherald.com/a-beginners-guide-to-cedh/
[82] Draftsim, "cEDH." https://draftsim.com/cedh-mtg/
[83] EDHREC (Dana Roach), "Superior Numbers: Land Counts." https://edhrec.com/articles/superior-numbers-land-counts
[84] The 8×8 Theory. https://the8x8theory.tumblr.com/what-is-the-8x8-theory
[85] Sutcliffe/Wong, "Quadrant Theory" (WotC). https://magic.wizards.com/en/news/feature/quadrant-theory-2014-08-20
[86] WotC, "Introducing Commander Brackets Beta." https://magic.wizards.com/en/news/announcements/introducing-commander-brackets-beta
[87] WotC, "Commander Brackets Beta Update — February 9, 2026." https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-february-9-2026
