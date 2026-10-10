# CLAUDE.md — "Until the First Snow" (Mini Jam 221: Autumn)

**Game title:** *Until the First Snow* (use this exact title on the title screen, in Player Settings → Product Name, in the WebGL page title and on itch.io). Code namespace stays `Courtyard`.
**Tagline (itch.io short description):** *a cozy lofi story about coffee and love*
**itch.io genre:** Simulation · **Tags:** cozy, lofi, romance, story-rich, relaxing, short, 2d, autumn, coffee, wholesome

Project brief and working rules for Claude Code. Read this whole file before doing any work.

## 0. Jam constraints (hard rules)

- **Jam:** Mini Jam 221: Autumn — https://itch.io/jam/mini-jam-221-autumn
- **Deadline:** submission closes **Mon 12 Oct 2026, 04:00 (itch.io time)**. Internal target: **submit by Sunday 11 Oct, 23:00 Budapest time**.
- **Limitation (mandatory, rated):** *Staying Warm*. Theme *Autumn* is optional flavour.
- **Rated on:** Enjoyment, Concept, Presentation, Use of limitation.
- **NO GENERATIVE AI FOR ART OR AUDIO.** Never generate sprites, textures, images, music or SFX with AI tools. This includes the Unity MCP `generate_image` / `generate_model` tools: **do not call them in this project**. AI-assisted *code* is fine.
- All art and audio must be hand-made by us, or come from the asset packs listed in `ASSETS` (section 9), with a credit in `CREDITS.md`.
- Game must be made during the jam (from 9 Oct 2026).

## 1. Game summary

A short, cozy, story-driven 2D game in **5 levels (days)**. A tired office clerk makes himself a warm drink in his kitchen. Over one autumn he learns to enjoy small rituals and falls in love with **Mira**, the neighbour whose kitchen window faces his across the courtyard of a Budapest apartment building.

- **Core loop per level:** short story text, then 2–5 drink-making minigames, keeping a **warmth meter** above zero, then the drink is served, an ending beat plays, and we move to the next day.
- **Limitation:** the kitchen is cold, so warmth drains constantly and a good drink restores it. From Day 20 the player keeps **two people** warm. On Day 30 the meter no longer drains: they are warm together.
- **Play time:** about 8–12 minutes total. Mouse only. Browser (WebGL) first.
- **Tone:** lofi, soft, quiet. No harsh fail states. A failed level restarts gently with "Too cold… let's try again."

## 2. Tech stack and project rules

- **Engine:** Unity 6 (LTS), **URP 2D Renderer** (needed for 2D lights to shift warm and cold colour per level).
- **Target:** WebGL (primary), macOS standalone (secondary). 1920×1080 reference resolution, 16:9, scaled with Canvas Scaler.
- **Language:** C#. Use the new Input System only if it's already in the project; otherwise use the old Input Manager mouse API. Keep it simple.
- **Text:** TextMeshPro.
- **Animation:** DOTween is allowed if installed. Otherwise write small coroutine tweens. Don't add other packages without asking.
- **Data-driven:** all level content lives in ScriptableObjects. No story strings hard-coded in MonoBehaviours.
- **Folder layout:**
  ```
  Assets/_Game/
    Art/           Sprites/, Backgrounds/, UI/, Particles/
    Audio/         Music/, SFX/, Ambience/
    Data/          Levels/ (LevelDefinition assets), Recipes/
    Prefabs/       Minigames/, UI/, Kitchen/
    Scenes/        Boot.unity, Kitchen.unity, Ending.unity
    Scripts/       Core/, Minigames/, Story/, Kitchen/, UI/, Audio/, Editor/
    Tests/         EditMode/
  ```
- **Code style:** one class per file, `namespace Courtyard.<Area>`, `[SerializeField] private` instead of public fields, no `FindObjectOfType` in Update, no singletons except `GameManager` and `AudioManager`, and events instead of polling where reasonable.
- **Never** commit Library/, Temp/, Logs/, Builds/. Keep a Unity `.gitignore`.

### Unity from the command line (for Claude Code)

Set the editor path once. Check the installed version with `ls /Applications/Unity/Hub/Editor/`:

```bash
export UNITY="/Applications/Unity/Hub/Editor/<VERSION>/Unity.app/Contents/MacOS/Unity"
export PROJ="$(pwd)"
```

- **WebGL build (batch mode):**
  ```bash
  "$UNITY" -batchmode -quit -projectPath "$PROJ" \
    -executeMethod Courtyard.Editor.BuildScript.BuildWebGL \
    -logFile - | tee Logs/build-webgl.log
  ```
- **macOS build:** same, with `-executeMethod Courtyard.Editor.BuildScript.BuildMac`.
- **EditMode tests:**
  ```bash
  "$UNITY" -batchmode -projectPath "$PROJ" -runTests -testPlatform EditMode \
    -testResults Logs/editmode-results.xml -logFile -
  ```
  (No `-quit` with `-runTests`.)
- Batch mode fails if the project is open in the Editor. Close the Editor first, or use Unity MCP for in-editor work (scenes, prefabs, components).
- `Scripts/Editor/BuildScript.cs` must exist with `BuildWebGL()` → `Builds/WebGL/` and `BuildMac()` → `Builds/Mac/`, using the scenes Boot, Kitchen, Ending, and exit with a non-zero code on failure (`EditorApplication.Exit(1)`).

**WebGL settings for itch.io:**
- Publishing Settings → Compression Format **Gzip** with **Decompression Fallback ON** (or Compression **Disabled**). Avoids server-header errors on itch.io.
- Template: `Assets/WebGLTemplates/Courtyard` (copy of Minimal; page title = Product Name). Canvas 1280×720 on itch.io, with a fullscreen button.
- Browsers block audio until the first click, so the Boot scene shows a **"click to start"** title screen before any audio plays.

## 3. The kitchen (single scene, reused for all 5 days)

**Camera:** orthographic, front view. The player stands at the kitchen counter, looking at the window. We never see the clerk's face; we only see his **hands** when he interacts.

**Layers, back to front (sorting layers in this order):**

1. **Sky:** gradient sprite, swapped per time of day.
2. **Courtyard (outside, seen through the window):**
   - The opposite wall of a Budapest courtyard building: old yellow plaster and a **gallery walkway with an iron railing** (typical *függőfolyosó*).
   - **Mira's window:** directly across, slightly higher. States: `Dark`, `LitWarm` (yellow light, curtain), `LitWarmSilhouette` (her silhouette holding a mug), `Waving` (2-frame wave), `NoteOnGlass` (paper note sprite on her window), `Empty` (Day 30, because she's at his place).
   - **A chestnut tree** in the courtyard, with a crown sprite per day: green-yellow, orange, golden, half-bare, bare with snow.
   - **Weather particles** in front of the courtyard but behind the glass: rain, falling leaves, wind-blown leaves, snow.
3. **Kitchen wall:**
   - Light tiled backsplash above the counter, with a big **window** in the centre (wooden frame, window sill).
   - **Old cast-iron radiator** under the window. Purely visual: cold blue tint on cold days, slightly warmer later.
   - Left wall: a **shelf** with jars (instant coffee jar on Day 1; real coffee beans appear from Day 5; a cinnamon jar appears from Day 10), a **wall clock** showing the level's time, and a **paper calendar** with the day number circled (Day 1, 5, 10, 20, 30). The calendar is the level indicator.
   - Right side: the edge of a **fridge** with magnets. Mira's paper notes accumulate here from Day 10 onward (narrative progress you can see).
   - A **ceiling lamp** at the top: cold fluorescent on Day 1, warm bulb from Day 10, and a warm lamp plus fairy lights on Day 30.
4. **Counter (the play area, left → right):**
   - **Kettle zone:** electric kettle on its base.
   - **Grinder zone:** manual hand grinder (from Day 5).
   - **Brew zone (centre):** the mug spot. A V60 dripper sits on top of it on pour-over days.
   - **Milk zone:** milk jug and steam wand or frother (from Day 10).
   - **Mug rack:** 1 chipped mug on Day 1, more mugs later, and her mug from Day 20.
   - **Window sill:** a small plant (wilted on Day 1, recovering, blooming by Day 30), and two mugs side by side on Day 30.
5. **Foreground:** **hands** sprite (appears while dragging a tool) and a steam particle above the active drink.
6. **UI:** warmth meter (top-left, a thermometer or a mug with a steam bar), story text box (bottom centre, typewriter effect), and a small step hint (top centre, e.g. "Pour slowly in circles").

**Lighting per level:** a URP **Global Light 2D** colour and intensity, plus one **Point Light 2D** for the ceiling lamp and one for Mira's window. Colours come from `LevelDefinition.palette`.

## 4. Levels (data in `Data/Levels/Day_XX.asset`)

| Day | Time and weather | Minigames (in order) | Warmth | Mira | Mood and palette |
|---|---|---|---|---|---|
| 1 | Grey morning, light rain | KettleBoil → InstantCoffee | slow drain (tutorial) | Her window `LitWarmSilhouette`. No hint; it's for the attentive player. | Desaturated blue-grey, fluorescent light |
| 5 | Rainy evening, heavy rain | KettleBoil → GrindBeans → PourOver | medium drain | She **waves**; the player clicks a "raise mug" prompt to wave back | Grey with first orange leaves |
| 10 | Sunny morning, falling leaves | KettleBoil → GrindBeans → PourOver → MiraVisit → TakeHerCup → SteamMilk → GuessHerDrink | medium | **Knock at the door** ("took-took"): fade to black, footsteps, eyes open at the front door (Art/Scenes/door_mira.png) — take her cup. Back in the kitchen two cups stand on the counter: his finished coffee and her cup, which milk and spices then fill (from a hint) | Golden, warm sunlight |
| 20 | Late night, cold, windy | KettleBoil → GrindBeans → PourOver → SteamMilk → ChooseMug (hers) | **fast drain, 2 meters** (him + her) | **Note on her window**: "Heating broke 🥶". He replies "Hold on." The level ends with him carrying two mugs across the courtyard (fade to black, footsteps on leaves) | Dark blue night, warm lamp pocket |
| 30 | Last evening of autumn, first snow | KettleBoil → SteamMilk → HerDrinkFromMemory | **no drain** (meter full and glowing) | She's **in his kitchen**: scarf on the chair, second mug, blanket. Her window across is `Empty`/dark | Warmest palette, snow outside |

**Fridge notes:** at the end of every day a paper note pops onto the fridge (paper sound, sparkle, hint "A new note on the fridge. Click it to read."). Clicking a note opens it as a card in the middle of the screen; any click puts it back. If not opened within ~5 s the day moves on; all earlier notes stay on the fridge and can be read any time. Texts live in `Data/Story/FridgeNotes.asset` (`afterDay`, `from`, `text`); Day 10's note from Mira records her usual (cinnamon + honey) for Day 30.

Then the **Ending scene**: two mugs on the window sill, snow falling, music fades, final lines, credits.

## 5. Story text (Data/Levels, `storyBeats`)

Each beat has a `trigger` (`LevelStart`, `AfterStep(n)`, `LevelEnd`), a `style` (`Thought` in italics, `Note` on a paper card, `Dialogue` with a speaker name, `Phone` as a chat bubble) and `text`.

**Day 1**
- LevelStart / Thought: "6:40. Again."
- AfterStep(0) / Thought: "Instant coffee. It tastes like Monday."
- LevelEnd / Thought: "…Someone across the courtyard is already awake."

**Day 5**
- LevelStart / Thought: "Bought real beans today. No idea why."
- AfterStep(1) / Thought: "Grind. Pour slowly. Breathe."
- LevelEnd / Thought: "She waved. I waved back with my mug. Smooth."

**Day 10**
- LevelStart / Thought: "Sunday. The light is different today."
- AfterStep(2) / Dialogue (Mira): "Hi… I'm Mira, from across. I ran out of coffee. Could I borrow some?"
- AfterStep(3) / Dialogue (Mira): "Something sweet… with cinnamon? Surprise me."
- LevelEnd (correct) / Dialogue (Mira): "Okay. That's the best cup I've had this month."
- LevelEnd (wrong) / Dialogue (Mira): "It's… nice. Thank you." (still continues)
- LevelEnd / Thought: "I think I'm smiling. Weird feeling."

**Day 20**
- LevelStart / Note (on her window): "Heating broke 🥶"
- AfterStep(0) / Note (his reply): "Hold on."
- AfterStep(3) / Thought: "Two cups. Hers first."
- LevelEnd / Thought: "Funny. A month ago I couldn't keep myself warm."

**Day 30**
- LevelStart / Dialogue (Mira): "It's snowing! Make me the usual?"
- AfterStep(2) / Thought: "I don't need the recipe anymore."
- LevelEnd / Thought: "Autumn's over." → "For the first time in a long while — I'm warm."

Writing rules: max ~12 words per line, max 5 lines per beat, English only.

## 6. Minigames (`Scripts/Minigames`)

Base class:

```csharp
public abstract class MinigameStep : MonoBehaviour {
    public event Action<float> Completed;            // quality 0..1
    public abstract string HintText { get; }
    public abstract void Begin(MinigameContext ctx);  // ctx: recipe, difficulty, audio
    protected void Complete(float quality) => Completed?.Invoke(Mathf.Clamp01(quality));
}
```

| Step | Interaction | Quality is based on |
|---|---|---|
| **KettleBoil** | Click the kettle switch; a temperature needle rises; click again to stop inside the green band (90–96 °C) | distance from the band centre |
| **InstantCoffee** | Carry spoonfuls from the jar into the cup one by one (the cup visibly fills), then pick up the kettle, carry it over the cup and pour to the line | pour accuracy; **wrong number of spoons spoils the cup (quality 0: "Brr… cold." + warmth loss); the day goes on** |
| **GrindBeans** | Drag the grinder handle in circles; the grind meter fills; stop in the "medium" zone | final grind level vs. target |
| **PourOver** | Hold the mouse to pour from the kettle and move it in a circle over the V60; the flow-rate bar must stay in a band; fill to the line | % of time in band + final fill accuracy |
| **SteamMilk** | Hold to steam; foam level and temperature rise; release in the zone (too long gives a squeal SFX) | release timing |
| **ChooseMug** | Click a mug on the wall rack (one new mug per day; earlier days' mugs hang there, never duplicated); hers (maple leaf) was used on Day 10 | correct = 1, wrong = 0.6 (no hard fail) |
| **GuessHerDrink** | Carry spices (cinnamon, honey, cocoa, salt) to the cup one portion at a time, then click the cup | matches recipe `Mira_Usual`; **too much (2× one spice, >2 total) or too little spoils it (quality 0, cold drink); the day goes on** |
| **HerDrinkFromMemory** | Same as GuessHerDrink but with no hint; uses `Mira_Usual` | matches recipe |

**Juice requirements for every step:** an SFX on each action, steam particles, a small squash/scale tween on the drink, and a "perfect!" popup at quality ≥ 0.9. Everything is readable without instructions after 3 seconds; show `HintText` at the top.

## 7. Warmth system (`Scripts/Core/WarmthMeter.cs`)

- `value` 0..100, starts at the level's `startWarmth`.
- **No time drain** (`drainPerSecond` = 0 on every day): warmth moves only with how well the drinks are made — a good step warms, a bad or spoiled one chills.
- After each step: `value += step.quality * level.warmthPerStep`. **A badly made step (quality < `badResultThreshold`, 0.4) is a cold drink: it takes warmth away instead** (`badStepChill` 20 at quality 0, half that just under the threshold) with frost, "Brr… cold." and a shiver.
- Final drink served: `value += finalQuality * level.drinkWarmthBonus`; a bad final drink (average < 0.4) chills by `badDrinkChill` (35). If that empties a meter the day restarts with `coldDrinkText`. So careless play loses to the cold even at a normal pace (`BalanceTests.TerribleBarista_FreezesOnEveryDayButTheLast`). Day 30 never fails.
- `value <= 0` → gentle fail: fade to blue, show "Too cold… let's try again.", restart the level. This is the **only** way to lose a day; in practice only Day 20 (a careless player). Day 1 (tutorial) has a floor of 15% (`minWarmth`) and can't be lost.
- Day 20: two meters (`Him`, `Mira`). The serve step for her mug restores only her meter. Both must stay > 0.
- Day 30: `warmthLocked`, meter full with a glow animation.
- Visuals: below 30 %, a frost vignette on the screen edges and the music low-pass filter closes slightly. Above 70 %, a warm vignette.

**Tuning:** all numbers live in `LevelDefinition`. No magic numbers in code. startWarmth 70, warmthPerStep 15, drinkWarmthBonus 25, drain 0. Cold per bad step / bad served drink: Day 1 20/35 (floor 15%), Day 5 12/20, Day 10 8/20, Day 20 20/35, Day 30 locked. `BalanceTests` checks: an average player never fails at any pace, a careless player (quality 0.1) loses only Day 20, Day 1 can't be lost, Day 30 never fails.

**Counter layout:** all of the day's tools stand on the counter at once (kettle → grinder → mug + V60 → milk jug → topping jars). They live in `CounterStation` in the Kitchen scene; each minigame step activates its tool and shows only its own gauge next to it. The mug is shared, so the drink visibly builds up across steps.

## 8. Data model

```csharp
[CreateAssetMenu(menuName = "Courtyard/Level")]
public class LevelDefinition : ScriptableObject {
    public int dayNumber;                 // 1,5,10,20,30
    public string clockTime;              // "06:40"
    public TimeOfDay timeOfDay;           // Morning, Evening, Night
    public Weather weather;               // LightRain, HeavyRain, Leaves, Wind, Snow
    public LevelPalette palette;          // global light colour/intensity, lamp colour, vignette tint
    public TreeStage treeStage;
    public MiraWindowState miraWindow;
    public KitchenProps props;            // flags: beans, grinder, milk, herMug, scarf, blanket, plantStage
    public List<MinigameStep> steps;      // prefab references, in order
    public List<StoryBeat> storyBeats;
    public bool twoMeters;
    public float startWarmth, drainPerSecond, coldMultiplier, warmthPerStep, drinkWarmthBonus;
    public AudioClip ambience;            // rain/wind/room tone
}
```

`GameManager` loads levels in order `[1,5,10,20,30]` → Ending. `KitchenDirector` applies a `LevelDefinition` to the scene (sprites, lights, particles, props). `StoryPlayer` shows beats. `LevelRunner` sequences steps and the warmth meter.

## 9. ASSETS (all checked: free, and the page says no generative AI unless noted)

Before importing, re-check each page's licence. Add every used pack to `CREDITS.md`.

**Art style decision:** flat, soft, hand-drawn 2D at 1920×1080. **The kitchen background, counter and the main interactive props (kettle, mug, V60, grinder, hands) are drawn by us** as simple flat shapes (Figma, Aseprite or Krita). No pack matches a close-up front-view counter. Packs are for decor and particles.

| Use | Pack | Licence notes |
|---|---|---|
| Decor (window, shelf, hanging light, coffee machine, rug), hand-drawn | ODDBLOT — FREE Basic Coffee Shop: https://oddblotstudios.itch.io/basic-coffee-shop-pack | Free, commercial OK, credit appreciated, no AI |
| Mugs (64 mugs, full/empty) | Pop Shop Packs — Mugs: https://pop-shop-packs.itch.io/mugs-free-2d-pixel-asset-pack | Pixel art: only if we go pixel; no credit required |
| Coffee tools (V60, moka, cezve), free sample | yanin — 16x16 Specialty Coffee (free sample): https://yaninyunus.itch.io/16x16-specialtycoffee | Free sample = 48 sprites; full pack is paid |
| Coffee machines / cups (pixel) | karsiori — Bar & Cafe Items: https://karsiori.itch.io/free-pixel-art-bar-and-cafe-items-pack | CC0 |
| Weather particles (rain, snow, autumn leaf) | EMI EMI GAMES — Weather particle sprites: https://emiemigames.itch.io/weather-particle-sprites | Free, credit optional |
| **Music (lofi)** | Purrple Cat — Just Relax (tracks "Foggy Window", "Quiet Night"): https://purrplecat.itch.io/just-relax | **CC BY-SA 4.0, credit "Purrple Cat"** |
| Music alt. (loopable WAV) | Josh Lim — cozy cats lofi loops: https://joshjameslim.itch.io/cozy-cats-lofi-beat-loops-to-game-to | CC BY 4.0, credit |
| Music alt. | Kashdan Music — Cozy Music Pack: https://kashdanmusic.itch.io/cozy-tunes-music-pack | **Non-commercial only**: fine for a free jam game |
| Rain / wind ambience | Helton Yan — Weather Elements (use the Freebie zip, 44 MB): https://heltonyan.itch.io/weatherelements | CC BY 4.0, credit |
| Household foley (kettle, cabinets, doors, switches) | Polarsound — Household Interior Foley: https://polarsoundproductions.itch.io/household-interior-foley-free-pack | Free, no AI, no redistribution |
| Cooking foley (pouring liquid, boiling) | MorphCore — Cooking Sound Library Lite: https://morphcore-sound.itch.io/morphcore-cooking-sound-library | Free, no AI |
| Kitchen foley (500+ sounds) | Amadeus Herrera — Sounds of the Kitchen: https://amadeusherrera.itch.io/the-sounds-of-the-kitchen | Free, don't resell; 146 MB |
| UI clicks / pops | Kenney — Interface Sounds: https://kenney.nl/assets/interface-sounds and UI Audio: https://kenney.nl/assets/ui-audio | CC0 |
| Footsteps (Day 20 walk) | Nicola Fabro — Footsteps: https://nicola-fabro.itch.io/footsteps-sound-effects-pack | check page |
| Specific SFX (grinder, door knock, mug clink, paper note) | Freesound, filter licence = **Creative Commons 0**: https://freesound.org/search/?q=coffee+grinder · https://freesound.org/search/?q=door+knock · https://freesound.org/search/?q=mug+ceramic · https://freesound.org/search/?q=pour+water · https://freesound.org/search/?q=paper+note | CC0 preferred; CC-BY needs credit |

**Do NOT use (tagged "AI Assisted" on itch.io, which breaks jam rules):**
- The Outlander — Free Kitchen Interior Backgrounds
- Outta This World Audio — Free Kitchen Sounds
- RUSTED MUSIC STUDIO — Ambient LoFi Jazz

## 10. Milestones (build in this order; each must be playable)

1. **M1 — Skeleton (Fri):** Boot (click to start) → Kitchen → Ending flow; `LevelDefinition`, `GameManager`, `LevelRunner`, `WarmthMeter`, `StoryPlayer`; placeholder grey boxes; Day 1 fully playable (KettleBoil + InstantCoffee). `BuildScript` + one WebGL build that runs on itch.io (upload as **draft**, test early!).
2. **M2 — All content (Sat):** remaining minigames, all 5 `LevelDefinition` assets with story, two-meter Day 20, Day 30 no drain, gentle fail/restart, `KitchenDirector` props and Mira window states.
3. **M3 — Art & sound (Sun day):** final kitchen art, lights per palette, weather particles, tree stages, SFX for every action, music + ambience, vignettes, "perfect!" popups, transitions (calendar page flip between days).
4. **M4 — Ship (Sun evening):** tuning pass (play all 5 days start to finish twice), credits screen, `CREDITS.md`, itch.io page (cover 630×500, 3–5 screenshots, a GIF, how to play, limitation explained in one line, credits), submit to the jam.

**Feature freeze:** Sunday 18:00. After that, only bug fixes and tuning.

## 11. Testing

- **EditMode tests** (`Tests/EditMode`): WarmthMeter drain/restore/clamp; two-meter fail rule; Day 30 no-drain; every `LevelDefinition` has ≥1 step, non-empty story, valid day number; recipe matching for GuessHerDrink.
- **PlayMode playtests** (`Tests/PlayMode`, 22): every day played end to end with a virtual mouse (`InputTestFixture`), the Day 20 fail/restart, clumsy-player cases and kitchen "life". Run all PlayMode tests after gameplay changes. Results and the bug/suggestion backlog live in `Docs/Playtest_Report.md`.
- **Manual checklist before each upload:** full playthrough in the WebGL build (not only the Editor), audio starts after the first click, no console errors, 60 fps in Chrome and Firefox, every text box readable at 1280×720, fail-and-restart works on Day 20.

## 12. How to work with me (Claude Code)

- Work milestone by milestone. At the end of each task, say what changed and how to test it.
- Prefer small commits: `feat(minigame): add PourOver step`.
- If something needs a decision (art, story, balance), propose 1–2 options and ask. Don't silently invent new mechanics.
- Never generate art or audio with AI. Use placeholders (coloured rectangles/circles) until real assets exist.
- Keep everything data-driven so tuning is editing assets, not code.
