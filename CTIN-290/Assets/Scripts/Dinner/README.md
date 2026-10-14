# You Had to Be There — steak prototype

Open `Assets/Scenes/DinnerPrototype.unity` and press Play. Everything except the kitchen model is built
at runtime by `DinnerGame` (friends, pan, steaks, HUD, walls, lamps, audio), so the scene stays tiny.

## The evening

1. **Intro** — an establishing shot; the friends sit down behind you.
2. **Cooking, in rounds** (`SteaksToCook`, default 3). You stand at the stove. The steaks cook in real
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
- `PanCenter`, `TableCenter`, `SeatHeight` — found from the model's material names at startup
  ("gas stove2", "Yellow Tartan", "Tauari wood.001" chairs); override if the model changes.
- `SteakCooking.PanSideRate` / `SearRate` — at medium heat a face cooks through in ~45 s and crusts in
  ~25 s; a careful high-heat steak takes about a minute, a low-heat one well over two.
- The rating scores doneness (45%), crust (20%), salt (15%), basting (10%) and evenness (10%).
- Dialogue lives in `ConversationScript.cs` — three short beats, some filler, one-line verdicts.

## Notes

- Audio is synthesised at startup (`ProceduralAudio`); there are no sound assets.
- The FBX ships with a camera and three area lights; `KitchenLayout` disables them at runtime.
- `Run In Background` is enabled in Player Settings so Play mode keeps ticking when the Editor
  isn't focused.
