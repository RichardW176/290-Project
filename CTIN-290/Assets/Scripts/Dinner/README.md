# Everything Is At Steak — steak prototype

Open `Assets/Scenes/DinnerPrototype.unity` and press Play. Everything except the kitchen model is built
at runtime by `DinnerGame` (friends, pan, steaks, HUD, walls, lamps, audio), so the scene stays tiny.

## The evening

The evening plays as the storyboard's six shots:

| Shot | What you see | Where |
|---|---|---|
| 1 | Title over the dimmed house: *Everything Is At Steak*, **Start Game** / **Quit Game** (mouse, arrows + Enter; Esc quits) | `DinnerHUD.BuildTitle` |
| 2 | *They're here* — over the two friends' shoulders, across the table | `_tablePose` in `ComputePoses` |
| 3 | The isometric cutaway with the HUD up: round box, evening bar, clock and day | `IntroPose`, `DinnerHUD` top bar |
| 4 | Every round: the fridge opens, your hand takes the steak tray off the middle shelf | `Fridge` in `Shots.cs` |
| 5 | Leaning over the stove: the pan on the front-left burner, your hand on the spatula, salt, mug, butter dish and two tubs on the counter; butter and rosemary go into the pan with `B` | `PanPose`, `DressStove`, `HoldSpatula` |
| 6 | Straight down onto the friend's plate (fork left, knife right, rosemary on the steak) while the rating card is up | `PlatePose` |

The fridge in the FBX is a solid box, so for shot 4 its body is swapped for an open shell with shelves and
food, and the lower door is reparented to a hinge and swung out; everything is put back after the shot.
During shot 6 the friends sit back (`FriendCharacter.SitBack`) so their arms don't cover the plates.

1. **Intro** — shots 1–3.
2. **Cooking, in rounds** (`SteaksToCook`, default 3). Each round opens on the fridge, then you stand at the stove. The steaks cook in real
   time whether you look at them or not; the friends talk (sparsely) in real time whether you listen or not.
   - `H` cycles the burner: **high** builds a crust fast and burns fast, **low** is safe but slow —
     the evening drifts while you wait. Medium is medium.
   - Each steak has two faces. The face on the pan cooks through and builds a crust; `F` flips.
     `S` salts whichever face is up, so both faces need a pass. `B` puts the butter in, then each
     press spoons it over the top (basting). Butter on high heat for too long smokes.
   - `E` plates. A rating card comes up — score out of 100 and what the steak was — the table
     says one thing about it, and the next pair of steaks lands in the pan. The burner stays where
     you left it.
   - `Space` turns you around (stove ⇄ table). Facing the pan, their words arrive as muffled dots;
     facing them, the pan only reaches you as *pop* cues and sizzle. Pressing a pan key while turned
     around only turns you back — the sentence you were listening to is gone.
   - After a missed beat, `Q` asks *"what was that about?"* — the answer is always some version of
     "you had to be there".
3. **Ending** — after the last steak: every score, the average, and which moments you were there
   for. `R` restarts.

## Tuning knobs (on the `DinnerGame` component)

- `SteaksToCook`, `RatingSeconds` — how many rounds, how long the rating card stays up.
- `CookingMinutesPerSecond` — how fast the wall clock moves (the light dims with it).
- `WorldScale` — the kitchen model is ~1.4× life size; friends and camera heights scale with it.
- `PanCenter`, `TableCenter`, `SeatHeight` — found from the model at startup; override if the model changes.
  The pan goes on the **front-left burner** (as you face the stove), found from the grate's bars, and its
  body (not its handle) is centred there; the spatula rests on the front-right burner (`SpareBurner`).
  Line VFX up with the pan at the burner centre, about (-1.025, 1.63, -1.328).
- `SteakCooking.PanSideRate` / `SearRate` — at medium heat a face cooks through in ~45 s and crusts in
  ~25 s; a careful high-heat steak takes about a minute, a low-heat one well over two.
- The rating scores doneness (45%), crust (20%), salt (15%), basting (10%) and evenness (10%).
- Dialogue lives in `ConversationScript.cs` — three short beats, some filler, one-line verdicts.

## HUD

Built in code by `DinnerHUD` after the HUD mockup (1920×1080 reference); palette, generated sprites
(a 9-sliced rounded rect and a circle — no image files) and the shape-built icons live in `UIKit`.

- **Top left — Serve**: click it or press `E`. The round ("Steak 1 of 3") sits under it.
- **Top centre — Raw ▸ Burnt**: overall doneness, filling red → green (in the window, medium rare to
  medium) → gold → brown; the current word ("rare") rides under the end of the fill.
- **Top right**: the clock pill and the day. Shown from the kitchen overview on, whichever way you face.
- **Bottom — dialogue**: a beige box with the speaker on a gold tag over its corner. At the pan the lines
  are muffled dots with no name, and the box narrows to fit between the side cards and the keys.
- **Right — Space**: the friends icon at the pan; a pan icon while you're facing the table.
- **Bottom left — Side A / Side B**: salt, butter and crust for each face. The face in the pan has the
  hotter header and says *in the pan*. Butter reaches the face in the pan while it sits in melted butter,
  and the face up each time you baste (display only; the score still uses overall basting).
- **Bottom right — H F S B**: heat shows its level as three pips; *Butter* becomes *Baste* once it's in.
  Facing the table, these give way to **Q** *What was that?* when there's something to ask about.

Fonts: `DinnerGame.DisplayFont` (headings, keys, Serve, Raw/Burnt) and `TextFont` (labels, subtitles) take
any TMP font asset; empty means TextMesh Pro's LiberationSans (bold for the display role).

## Props from the packs

`DinnerGame.Pack` holds the prefabs (auto-filled from `Assets/Models/...` in the Editor if a slot is
empty; the saved scene has them assigned for builds). `Props.Spawn` fits every prefab to a world size
from its renderer bounds, so the packs' ~10× units never matter.

- **Ultimate Food Pack**: `Steak` (meat mesh split into down/up/side faces so each side browns on its
  own; fat cap tinted separately), `FryingPan`, `Plate2` + `Fork` + `Knife` + `Soda` at every place,
  `Bread`, `Bottle1/2`, `KetchupMustard`, `Jar_Large`, `Plate_Square` + `Lettuce` + `Tomato_Slice`
  salad, `Tomato`, `Pepper_Red`, `Mushroom`, `Broccoli`, `Egg_Whole`; `Fries` + `Broccoli` as sides
  when a round is plated onto a friend's plate.
- **LowPoly Kitchen Starter Pack**: `Spatula` (rests on the next burner, slides under the steaks on a
  flip), `CuttingBoard`, `KitchenKnife`, `MilkCarton`. Its materials were converted from Standard to
  URP Lit once (persisted); `Props.FixMaterialsForUrp` also converts at runtime as a safety net.
- The kitchen FBX's material names are not stable between imports, so `KitchenLayout` finds the
  stove/table/chairs/room by geometry (names are only a first guess) and hides the model's own pan and
  table setting by where they sit rather than by name.

## Notes

- Audio is synthesised at startup (`ProceduralAudio`); there are no sound assets.
- The FBX ships with a camera and three area lights; `KitchenLayout` disables them at runtime.
- `Run In Background` is enabled in Player Settings so Play mode keeps ticking when the Editor
  isn't focused.
