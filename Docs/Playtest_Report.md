# Playtest report — Until the First Snow

Living document: how the game was tested, bugs found and fixed, what was added, and what is still worth doing.
Last update: 2026-10-10.

## How it was tested

- **Automated playtests (PlayMode, `Assets/_Game/Tests/PlayMode`, 17 tests)** — the real Kitchen scene is played
  with a virtual mouse (`InputTestFixture`) through the EventSystem, exactly like a player: clicks, drags, holds.
  Time runs at 4×.
  - `DayPlaytests` (5): every day (1, 5, 10, 20, 30) played start to finish, each step's quality checked.
  - `FailPlaytests` (1): idle on Day 20 → gentle fail → the day restarts.
  - `MisplayPlaytests` (7): clumsy/curious players — quick taps, early releases, wrong answers, forgotten kettle.
  - `LifePlaytests` (4): clickable details react, Mira waves back (only from Day 5), idle nudge.
- **EditMode tests (27)**: warmth rules, balance simulation (average / good / very slow player), level data, recipes.
- **Code review** of every minigame and of the level flow; screenshots of every day.

Result: **22/22 PlayMode and 32/32 EditMode pass.**

**Rule: the cold wins on bad coffee.** A step below quality 0.4 chills instead of warming (frost, "Brr… cold.",
camera shiver, weak steam); a bad served drink chills 35. A careless player now freezes on Days 1–20 at a normal pace
(`Day01_TerribleCoffee_TheColdWins`, `TerribleBarista_FreezesOnEveryDayButTheLast`). Day 30 never fails.

**Rule (from the team, updated):** warmth never drains with time; wrong amounts **spoil the drink (quality 0, "Brr… cold.", warmth loss) and the day goes on**. Only warmth reaching 0 ends a day (in practice Day 20 for a careless player); Day 1 can't be lost. Previously: wrong amounts ruined the drink and restarted the day — too many/few coffee spoonfuls on Day 1,
two portions of one spice, more than two spices, or serving with fewer than two. Run them from the Test Runner, or `unity command run_tests
--mode PlayMode --filter Courtyard.Tests.PlayMode --filter_type assembly --async_tests true` (Editor open, ~12 min).

## Minigame by minigame

| Step | Days | Verdict | Notes |
|---|---|---|---|
| KettleBoil | all | ✅ | No gauge: badge pulses, lid trembles → rattles + steam at the boil. Perfect stop = 1.0. Forgotten kettle boils dry, switches itself off (poor score). |
| InstantCoffee | 1 | ✅ (B2 fixed) | Spoon jar → mug (coffee puff, grounds inside the mug); kettle flies over the mug and pours with splashes. |
| GrindBeans | 5, 10, 20 | ✅ (B3 fixed) | Circular drag on the crank; dust + small shake while grinding; let go in the green zone. |
| PourOver | 5, 10, 20 | ✅ (B1 fixed) | Hand-held kettle follows the pointer; flow band now matches "pour in slow circles"; splashes. |
| WaveBack | 5 | ✅ | Raise-mug hand lifts the mug; Mira waves. |
| MiraVisit | 10 | ✅ | Knock, her window goes dark, she's at the door (placeholder art). |
| SteamMilk | 10, 20, 30 | ✅ (B4 fixed) | Frother shakes, foam rises, spills when overdone; foam puff on the mug. |
| GuessHerDrink / HerDrinkFromMemory | 10 / 30 | ✅ | Toggle two jars (sprinkle), serve. Wrong → gentle "It's… nice", day continues. |
| ChooseMug | 20 | ✅ | Picked from the real wall rack (no copies). Hers = 1.0, other = 0.6 (no hard fail). |
| Fail / restart | 20 | ✅ | "Too cold… let's try again." and the day restarts. |

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

## What was added (life & juice)

- **Clickable details (fidgets):** lamp (swings on its cord), calendar, curtains, plant (leaves puff), clock, picture,
  shelf, rack mugs, coffee bag, fridge notes. **Mira waves back** from her lit window when clicked — only from Day 5.
- **Ambient motion:** curtains breathe (stronger on rainy/windy days), lamp sways, plant sways, Mira's silhouette moves,
  neighbours' windows across the courtyard switch their lights on and off.
- **Particles (new):** warm glow puffs on good steps (≥0.7), frost crystals + cold mist on bad ones, mug steam
  strength follows drink quality, dust motes drifting in the lamp light, his breath puffs from the bottom of the view
  when warmth drops below 35%.
- **Juice:** sparkles on "Perfect!", grinder dust + shake, pour splashes, spoon puffs, topping sprinkles, foam puff,
  smooth warmth meter that bounces on gains, soft round snow and steam.
- **No-text guidance:** after 7 s idle the right tool bounces (never reveals the ChooseMug / topping answers).
- **Story moments:** Day 20 ends with his and her mugs walking across the dark; Day 30's camera drifts to the two
  mugs on the snowy sill; title screen with a steaming mug and falling leaves; ending with both mugs in the snow + credits.

## Suggestions (backlog, highest value first)

1. **Audio (biggest gap).** Every action has a sound hook (`Data/Audio/SfxLibrary.asset`, 16 slots) but no clips — the
   game is silent. Import the packs listed in CLAUDE.md §9 (Kenney UI, Polarsound / MorphCore foley, Helton Yan rain,
   Purrple Cat music) and fill the slots; add music + ambience per day.
2. **Remaining placeholder art:** courtyard + tree, Mira's window and silhouette, Mira at the door (Day 10), fridge
   notes, honey and salt jars, mug rack, scarf and blanket (Day 30), weather particles (rain/leaves are squares).
3. **Hover highlight:** tools could lift slightly under the cursor (the open hand already appears).
4. ~~"Click" hint in the story box is small and static~~ — done: it now pulses once the line is typed.
5. **Settings:** mute / volume toggle and a fullscreen button on the itch page.
6. **Optional minigame variety** (if the steps still feel alike after audio): stir the drink with a spoon (circular
   drag with a "smoothness" meter), pour milk art on Day 30 (draw a heart).
