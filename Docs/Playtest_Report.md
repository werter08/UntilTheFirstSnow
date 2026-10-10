# Playtest report — Until the First Snow

Living document: how the game was tested, bugs found and fixed, what was added, and what is still worth doing.
Last update: 2026-10-10 (evening).

## How it was tested

- **Automated playtests (PlayMode, `Assets/_Game/Tests/PlayMode`, 23 tests)** — the real Kitchen scene is played
  with a virtual mouse (`InputTestFixture`) through the EventSystem, exactly like a player: clicks, drags, holds.
  Time runs at 4×.
  - `DayPlaytests` (5): every day (1, 5, 10, 20, 30) played start to finish, each step's quality checked
    (Day 10 includes the knock, the door scene and taking her cup).
  - `FailPlaytests` (1): Day 20 — time alone doesn't make it colder; a bad drink on low warmth lets the cold win and
    the day restarts.
  - `MisplayPlaytests` (12): clumsy players — quick taps, early releases, forgotten kettle, wrong spoons/spices
    (spoiled drink, day goes on), terrible coffee on Day 1 (can't be lost).
  - `LifePlaytests` (5): clickable details react, Mira waves back (from Day 5), idle nudge, reading a fridge note.
- **EditMode tests (34)**: warmth rules (lock, floor, chill), balance (average player never fails at any pace,
  careless player loses only Day 20, Day 1 can't be lost, Day 30 never fails), level data, recipes.
- **Smoke test of the whole chain** from the title: Day 1 → 5 → 10 → 20 → 30 → Ending, music per day, one audio
  listener, no console errors or warnings.
- Screenshots of every day after each visual change.

Result: **23/23 PlayMode and 34/34 EditMode pass.** Run them from the Test Runner, or
`unity command run_tests --mode PlayMode --filter Courtyard.Tests.PlayMode --filter_type assembly --async_tests true`
(Editor open, ~2.5 min).

## Rules (current)

- **Warmth follows the drinks, not the clock.** No time drain on any day. A step at quality ≥ 0.4 warms; below 0.4 it
  is a cold drink: frost, "Brr… cold.", a shiver and warmth loss. The served drink does the same.
- **Wrong amounts spoil the drink** (spoons on Day 1; two portions of one spice, more than two, or fewer than two):
  quality 0 and the day goes on — no restart.
- **The only way to lose is warmth reaching 0** ("Too cold… let's try again.", the day restarts). Tuned so only a
  careless player on Day 20 can lose. Day 1 has a 15% floor; Day 30 is locked warm.
- Finishing a step in the green zone (≥ 0.7) plays the little success jingle; ≥ 0.9 also shows "Perfect!".

## Minigame by minigame

| Step | Days | Verdict | Notes |
|---|---|---|---|
| KettleBoil | all | ✅ (B16 fixed) | Badge pulses, lid trembles → rattles + steam from 90 °C. Good range 90–108 °C (~1.5 s after the rattle starts). Forgotten kettle boils dry, switches itself off. |
| InstantCoffee | 1 | ✅ | Spoon lies on the counter; carry spoonfuls into the cup (full spoon sprite), then grab the kettle and pour over the cup (water drops). |
| GrindBeans | 5, 10, 20 | ✅ | Crank in circles; the coffee bag opens; grinder sound follows crank speed. |
| PourOver | 5, 10, 20 | ✅ (B17 fixed) | Grab the kettle, pour above the cup in slow circles; nothing changes until the spout is over the cup. The V60 lifts off when done. |
| WaveBack | 5 | ✅ | Mira waves with her drawn arm (swings from the shoulder) until you raise your mug. |
| MiraVisit + TakeHerCup | 10 | ✅ | Knock → black, footsteps → eyes open at the door (team's art) → take her cup → two cups on the counter; milk and spices go into hers. |
| SteamMilk | 10, 20, 30 | ✅ | Frother shakes, foam rises; squeal when overdone. |
| GuessHerDrink / HerDrinkFromMemory | 10 / 30 | ✅ | Carry spices into the cup one portion at a time, then serve. Day 10's fridge note records her usual. |
| ChooseMug | 20 | ✅ | Picked from the wall rack (her cup stayed after Day 10). Hers = 1.0, other = 0.6. |
| Fail / restart | 20 | ✅ | Only via warmth reaching 0. |

## Bugs

| ID | Severity | Area | Bug | Status |
|---|---|---|---|---|
| B1 | High | PourOver | Careful slow circles scored ~0.40: flow band started at 2 units/s, so following the hint was punished. | ✅ Fixed — band 0.12–0.55 |
| B2 | Medium | InstantCoffee | A quick tap on the kettle ended the step with an almost empty mug. | ✅ Fixed — needs 15% fill, otherwise the kettle goes back |
| B3 | Medium | GrindBeans | Letting go at ≥15% ended the grind far below the green zone. | ✅ Fixed — keeps grinding until the zone |
| B4 | Low | SteamMilk | A short tap ended the step with cold milk. | ✅ Fixed — needs 35 °C of heating |
| B5 | Medium | Mug | Coffee dots and fill line floated beside the mug (side gauge). | ✅ Fixed — drink shown inside the mug |
| B6 | Medium | Art pipeline | Prefabs/levels stored sprite IDs before the final import settings → missing sprites (instant jar, drinks). | ✅ Fixed — imports finalised first; all references verified |
| B7 | Low | Kitchen | V60 visible on Day 30 (no pour-over that day). | ✅ Fixed — only on pour-over days |
| B8 | Low | Decor | Pack bulb/flower overwrote the drawn lamp/plant on re-apply. | ✅ Fixed — pack art only as fallback |
| B9 | Low | Story | Clicking a kitchen detail while text is shown also advances the text. | Open — harmless, by design for now |
| B10 | High | Cup | Cups didn't match the art direction: the empty cup showed the bare body, the drink tile was misaligned. | ✅ Fixed — empty cap always on top; drink rises over it; caps aligned to rim and outline (team mockup) |
| B11 | Medium | InstantCoffee | Water was poured by tapping/holding the kettle (it flew to the cup by itself). | ✅ Changed — pick up the kettle and carry it over the cup to pour |
| B12 | Medium | Spices | Toppings were toggled by clicking jars; nothing went into the cup. | ✅ Changed — carry each spice to the cup; portions show on the drink |
| B13 | Low | Milk | Foam splat was huge and covered the frother. | ✅ Fixed — small cap at the rim, behind the frother |
| B14 | Low | Spices | A fast flick could drop a jar just outside the cup and lose the portion. | ✅ Fixed — jar or hand over the cup counts |
| B15 | Medium | Mugs | The same mug appeared twice (counter + rack / sill). | ✅ Fixed — a new mug each day (chipped, his, hers, yellow, green); earlier days' mugs hang on the wall rack |
| B16 | High | KettleBoil | Stopping right when the lid rattled often read as "Brr… cold.": the good range lasted only 0.6 s. | ✅ Fixed — 90–108 °C, gentler falloff |
| B17 | Medium | Pour | The pour (and flow meter) reacted while the kettle wasn't over the cup; a green rectangle stood in for the water. | ✅ Fixed — spout must be over the cup body, above the rim; falling water drops |
| B18 | High | Audio | Playing the Kitchen scene directly was silent (no AudioListener; Boot-only manager). | ✅ Fixed — every scene has a manager; it carries the one listener |
| B19 | High | Audio | Loops started silent: a new AudioSource reports isPlaying (playOnAwake) so the clip was never set. | ✅ Fixed — sources stopped on creation |
| B20 | Medium | Flow | Playing the Kitchen directly ended in the Ending after one day. | ✅ Fixed — direct play continues 1 → 5 → 10 → 20 → 30 |
| B21 | Medium | Builders | Full decor rebuilds moved hand-placed objects (Mira's window, tree) and shrank the drawn coffee bag. | ✅ Fixed — HandPlaced marker respected everywhere; pack art only as fallback; late passes run last |
| B22 | Low | Tests | All playtests failed when the Rendering Debugger's input clashed with the virtual mouse. | ✅ Fixed — debugger runtime UI off in playtests |

## Visual check (WebGL build, 2026-10-10)

Build: `Builds/WebGL` — succeeded, 0 errors, 38 MB (data 27 MB, wasm 13 MB), Gzip + decompression fallback. Browser
playthrough is done by hand by the team. Visual problems seen in the editor captures while preparing the build:

| # | Where | Problem | Suggestion |
|---|---|---|---|
| V1 | Day 30, counter right end | The salt jar sits against the fridge: its "Salt" label is cut by the fridge edge and the hanging scarf overlaps the jar. | Move the spice row ~0.3 left or the scarf a little right. |
| V2 | Window | The tree is mostly hidden behind the left curtain (only ~75 px of crown shows; the bare Day 30 tree barely). | Move the tree right (≤ 30 px keeps it clear of Mira's window) or draw it to peek past the curtain. |
| V3 | Window | The sky never shows: the opposite building's roof edge is above the window opening. | Lower the building or raise the roof in the art if sky is wanted. |
| V4 | Mira's window | Her waving arm and the note are partly crossed by the kitchen window's middle bar. | Fine as framing; or nudge her window a few px. |
| V5 | Day 30 | The warmest palette is very bright; with snow it reads washed out in places. | Lower Day 30's global light intensity slightly if it feels too bright in the browser. |
| V6 | Honey jar | The honey jar is wider than the others (130 px); its strings touch the neighbours. | Space the four spice jars a bit wider. |
| V7 | WebGL only | No audio filters in the browser: when cold, the music ducks instead of muffling. | Expected; nothing to fix. |

## What was added (life & juice)

- **Courtyard through the window** (team's art): the opposite building, Mira's window in five states with a warm
  light (the warm spot in Day 20's night), the chestnut tree through autumn, weather particles (leaf, snow, rain).
- **Mira really waves** with her drawn arm; clicking her lit window from Day 5 makes her wave back.
- **Day 10 at the door**: fade, footsteps, the door scene, take her cup; two cups on the counter.
- **Fridge notes**: a note pops onto the fridge after every day; click any note to read it (handwritten font).
- **Audio**: music per day (title → Day 1 → … → Ending), room tone + ambience per day, every action has a sound,
  pitch/volume follow the kettle temperature and the grinder speed, cold swell on fail; mixer with exposed volumes.
- **Cup** as in the team's mockup: the inside sits behind the mug's rim and fills from the rim up.
- **Shadows** under every prop (team's contact shadows) and behind every wall piece.
- **Hover highlight**: a soft warm light on whatever can be clicked or dragged.
- **Fonts**: Amaranth for all text, Playpen Sans for notes.
- **Clickable details, ambient motion, juice** (from earlier): fidgets, curtains breathing, lamp sway, sparkles,
  frost/mist on cold drinks, steam that follows drink quality, cold breath when warmth is low.

## Suggestions (backlog, highest value first)

1. **WebGL build + browser playthrough** (audio after the first click, ~60 fps, text readable at 1280×720), then a
   draft upload to itch. In the browser the music ducks instead of muffling (no audio filters in WebGL).
2. **itch.io page text** (description, how to play, one line on *Staying Warm*); screenshots and the 630×500 cover
   are in `Docs/itch/`; a short GIF is still missing.
3. **Missing art** (each drops in by file name): `scarf.png`, `blanket.png` (Day 30, hidden for now),
   `jar_honey.png`, `jar_salt.png` (tinted stand-ins now), `note.png` (fridge notes are sticky-note placeholders).
4. **Settings**: mute / volume and fullscreen buttons.
5. **In-game credits** (Ending): add fonts and music credits.
