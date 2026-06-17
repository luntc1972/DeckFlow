# DeckFlow launch blurbs

Promo copy for recruiting testers / launch. Written to read human, not like a
marketing announcement. Match each to the venue's tone and self-promo rules
(many subs/Discords require a megathread, flair, or mod ok for tools — check first).

Site: https://www.deckflow.gg

---

## Discord — chill / casual

hey so i built a little deckbuilding tool and looking for people to mess with it: deckflow.gg

you paste a moxfield/archidekt link and it makes you a ChatGPT prompt to break down your deck — like what's it actually trying to do, where's it clunky, what could get swapped. also does primers if you're picking up a new deck. free, no account or anything

it's still pretty early so stuff might be janky. mostly just curious what people think + what's missing

### Discord — one-liner

made a free thing for commander deckbuilding — paste a moxfield/archidekt link, get a ChatGPT prompt that breaks your deck down. deckflow.gg. still early, lemme know what you think 🙂

---

## Reddit — r/CompetitiveEDH (precise, skeptical; lead with cEDH features, no hype)

**Title:** I built a free tool that turns a Moxfield/Archidekt list into a ChatGPT prompt for cEDH analysis — looking for people to poke holes in it

Dev here, not selling anything — site's free, no account. Wanted feedback from people who actually play cEDH before I build it out further.

You paste a decklist URL and it generates a structured prompt you drop into ChatGPT. The two pieces most relevant here:
- **cEDH meta-gap** — frames your list against the meta and asks where you're soft (interaction density, fast-mana, wins-through-disruption, etc.)
- **deck comparison** — diffs two builds and asks the model to reason about the tradeoffs.

It also does primers (picking up an unfamiliar list), card/mechanic lookup, and a judge-question handoff.

Caveat I'm aware of: it's only as good as the prompt + whatever model you paste into — it's not a solver, it structures the question so you get a useful answer in one shot instead of hand-writing the setup. Curious whether the meta-gap framing actually holds up against how you'd evaluate a deck, or where it says something dumb.

deckflow.gg — roast it, especially the cEDH side.

---

## Reddit — r/EDH (casual, friendly; lead with primers + general analysis)

**Title:** Made a free tool that turns your Moxfield/Archidekt deck into a ChatGPT prompt for feedback — would love thoughts

Hey all — I built this for my own deckbuilding and figured others might get use out of it. It's free, no signup, not selling anything.

You paste a decklist link and it spits out a prompt you can drop into ChatGPT to get a breakdown of your deck — what it's trying to do, where it's clunky, what you might cut or add. A few things it does:
- **Deck analysis** — general "how's this deck look" feedback
- **Primer** — if you're picking up a new deck and want a rundown of the gameplan and key lines
- **Card / mechanic lookup** and a "how would a judge rule this" helper

Basically it just does the annoying part of writing a good prompt for you, so you get something useful back in one go instead of fiddling with it.

It's still early and rough in spots — mostly posting to find out what's confusing or what people wish it did. deckflow.gg, any feedback welcome 🙂

---

## BlueSky / X — one-liner

Built a free tool for Commander deckbuilding 🧙 paste a Moxfield/Archidekt link → get a ChatGPT-ready prompt that breaks down your deck (analysis, primers, cEDH meta-gap). No signup. deckflow.gg #mtg #EDH #cEDH #Commander

### leaner alt

Free Commander tool: paste a Moxfield/Archidekt link, get a ChatGPT deck-analysis prompt. deckflow.gg #mtg #EDH #cEDH

---

## Where to post (backlinks also help SEO discovery)

- r/CompetitiveEDH, r/EDH, r/magicTCG (broad, strict promo), r/EDHBrews, r/BudgetBrews
- Moxfield + Archidekt Discords (#tools / #third-party — you integrate with both)
- cEDH / Spike Feeders / EDHREC Discords (#tools, #deck-help)
- BlueSky + X with the hashtags above

Reminder: the honest "it's not a solver, it just structures the prompt" line preempts the top skeptical comment — keep it in the cEDH post.

---

## Pre-launch checklist (do before sharing widely)

- [x] SEO tags + robots.txt + sitemap.xml live on https
- [x] og-image.png renders in link previews
- [ ] Google Search Console: add `deckflow.gg` Domain property → verify via Cloudflare DNS TXT (`@`, `google-site-verification=...`) → submit `sitemap.xml` → URL-inspect homepage → Request indexing
- [ ] Bing Webmaster Tools: import from GSC + submit same sitemap
- [ ] Force preview re-scrape: FB Sharing Debugger + Twitter Card Validator on the URL
