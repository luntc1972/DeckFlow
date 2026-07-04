---
source: "Salubrious Snail"
title: "The Hidden Lore of an EDHRec Page"
url: "https://www.youtube.com/watch?v=o-VR3Du6Z5Y\u0026list=UUOYkwObFKjxko7oj56gVDag"
video_id: "o-VR3Du6Z5Y"
tags:
  archetype: ["combo","control","tribal","value-engine","lands","midrange"]
  bracket: ["cEDH","Optimized","Exhibition"]
  card_category: ["win-cons","removal","counter","ramp"]
generated_utc: "2026-06-08T18:50:52Z"
---

## Summary

This video argues EDHRec's percent-inclusion lists flatten which decks cards actually belong to, and teaches a manual technique to recover that information. The method: pick "indicator cards"—cards disproportionately run that signal a specific archetype—then use EDHRec's filter feature plus arithmetic to build Venn diagrams estimating overlap between deck types. Worked examples: Francisco/Malcolm splits into a ~58% Thassa's Oracle/Demonic Consultation combo cluster (Thoracle/Ramirez indicators) versus a ~36% midrange pirate cluster, with ~7% running both and ~13% neither. Skeleton Ship reveals four low-power memes—commander-ability builds, proliferate/counters decks, skeleton tribal, and ship-themed jank (indicators: Freed from the Real, Thrummingbird, Reassembling Skeleton). Archelos splits into tap/untap synergy (~20%), turtle tribal (~15%), and gates (Baldur's Gate, Nine-Fingers Keene, Maze's End). The practical payoff: this parsing prevents the common mistake of adding individually strong cards without asking which deck they belong to, which produces disjointed, clunky "frankenstein" builds. Treat EDHRec as data, not magic—keep in mind multiple distinct decks coexist under one commander, and identify which archetype a card serves before including it.

## Key Clips

- Displaying cards by percent inclusion in decks flattens a lot of information about the decks they're played in. If there are three cards each played in a third of decks, it's difficult to tell whether they're all played in the same third of decks, if they're all played in different decks, or something in between.
- Looking at the 10 high synergy cards—the ones disproportionately run in this commander pair compared to the color as a whole—offers a clear picture. Almost 60% of Francisco-Malcolm decks run Thassa's Oracle and Demonic Consultation, an instant win combo requiring a mere two cards and three mana.
- I'm going to call these two cards, along with Thoracle, 'indicator cards'—cards that give you a rough idea on the upper bound of players playing a particular type of deck. If I use the indicator cards of Thoracle and Ramirez, I can start to construct a Venn diagram with a combo circle containing 58% of decks, and a midrange circle containing 36% of decks.
- We can now make a triple Venn diagram, and fill it out by simply plugging in every combination of these three cards in EDHRec's filter tool. And upon doing this, the first thing I noticed was that cares-about-commander-ability and cares-about-counters seem almost like one very diverse super-category, whereas Skeletons were definitely more distinct.
- In my opinion as a professional gates enjoyer, Baldur's Gate is a much worse card than these other two. For early-game purposes it's a wastes in a three color deck, and the limitation to three colors means that even if you run every single in-color gate, only a third of your lands will be gates.
- One of the biggest pitfalls I've seen EDHRec users fall into is where they add cards that look good without thinking about what deck those cards belong in or how many of a single card type belong in a deck. This results in decks which have some amount of synergy in theory, but which in practice feel disjointed and clunky, containing a frankenstein-like mishmash of components from various decks.
- For this reason, I'd advocate spending an extra moment trying to parse what specific types of deck are actually being built with the different cards. Even just keeping in mind that there are different decks doing different things within a given commander will likely be a great help to newer players.
- EDHREC is a good tool, but it's not one you can go on autopilot with. You should try to keep in mind where the cards you're looking at are coming from, and that, no matter how snappy the UI is, the stuff you're seeing isn't magical, it's just data slurry.

## Tags

**Archetypes/Strategy:** combo, control, tribal, value-engine, lands, midrange
**Format/Bracket:** cEDH, Optimized, Exhibition
**Card Categories:** win-cons, removal, counter, ramp
